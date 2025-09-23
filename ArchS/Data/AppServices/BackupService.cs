using ArchS.Data.BackupServices;
using ArchS.Data.ProfileManager;
using ArchS.Data.FileManager;
using ArchS.Data.NotifierServices;
using System.Collections.Concurrent;
using static System.Console;
using System.Runtime.InteropServices;
namespace ArchS.Data.AppServices;


public class BackupService : IDisposable
{
    private List<Profile> _allProfiles;
    private ConcurrentQueue<Profile> _profilesToAdd;
    private Dictionary<Guid, bool> _profileUpdateCache = new Dictionary<Guid, bool>(); // key = profile id, value = bool whether to display bottom update or not 
    public IReadOnlyDictionary<Guid, bool> ProfileUpdateCache => _profileUpdateCache; // accessible from the UI 
    private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1); // at most one thread can enter immediately and only one is allowed during the process
    // SemaphoreSlim works as a mutex but can be used in async methods
    public event Action? ProfileUpdateEvent;
    private HashSet<string> _knownMounts = new HashSet<string>();
    private CancellationTokenSource? _cancelMountToken;
    private CancellationTokenSource? _cancelUpdateToken;
    private BackupExecutor _executor;
    private DesktopNotifier _notifier;
    private BackupOptions _options;

    public BackupService()
    {
        _options = new BackupOptions { }; // keep the default values
        _notifier = DesktopNotifier.Create();
        _executor = new BackupExecutor(_options, _notifier);
        _profilesToAdd = new ConcurrentQueue<Profile>();
        _allProfiles = BackupFileManager.GetProfiles();
    }


    /// <summary>
    /// Detect when a new directory is added into the folder where the hard drives are mounted
    /// Background worker so when a path is a target/source and it is detected it will appear in the list 
    /// </summary>
    public void StartMountWatcher()
    {
        if (_cancelMountToken != null) return; // prevents starting multiple watchers at once
        _cancelMountToken = new CancellationTokenSource();
        Task.Run(() => WatchMountsAsync(_cancelMountToken.Token)); // fire and forget
    }

    public void StopMountWatcher()
    {
        _cancelMountToken?.Cancel();
        _cancelMountToken = null;
    }

    private async Task WatchMountsAsync(CancellationToken token)
    {
        string[] roots;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) // { "/media", "/mnt" } for Linux
        {
            roots = new[] { "/Volumes" };
        }
        else
        {
            return;
        }

        while (!token.IsCancellationRequested)
        {
            try
            {
                foreach (var root in roots)
                {
                    if (!Directory.Exists(root)) continue;

                    foreach (var dir in Directory.EnumerateDirectories(root))
                    {
                        if (_knownMounts.Add(dir)) // returns true if added successfully 
                        {
                            CheckForNewProfiles();
                        }
                    }
                }
            }
            catch { }
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
    }

    public void CheckForNewProfiles()
    {
        List<Profile> profiles = BackupFileManager.CheckMountedPaths();
        _allProfiles.AddRange(profiles);
        if (profiles.Count > 0)
        { 
            ProfileUpdateEvent?.Invoke(); // send UI the notification
        }
    }


    /// <summary>
    /// Background worker checking every 5 seconds if there is an change on source files for the 
    /// profiles who have active KeepTrack Flag
    /// </summary>
    public void StartUpdateWatcher()
    {
        if (_cancelUpdateToken != null) return;
        _cancelUpdateToken = new CancellationTokenSource();
        Task.Run(async () =>
        {
            var token = _cancelUpdateToken.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await RefreshProfileCache();
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker exception: {ex}");
            }
        });
    }

    public void StopUpdateWatcher()
    {
        _cancelUpdateToken?.Cancel();
        _cancelUpdateToken = null;
    }

    private async Task RefreshProfileCache()
    {
        bool notify = false;
        var newCache = new Dictionary<Guid, bool>();
        List<Profile> currProfiles = new List<Profile>(_allProfiles);
        foreach (var profile in currProfiles)
        {
            if (!profile.KeepTrackFlag || !Directory.Exists(profile.TargetPath))
            {
                newCache[profile.Id] = false;
                continue;
            }

            var (archive, _)= ProfileHandler.GetArchiveItemToUpdate(profile);
            bool hasUpdate = archive.Items.Count > 0;
            _profileUpdateCache.TryGetValue(profile.Id, out bool hasUpdate_);
            if (hasUpdate != hasUpdate_)
            {
                notify = true;
            }
            newCache[profile.Id] = hasUpdate;
        }

        await _updateLock.WaitAsync(); // updates of the _profileUpdateCache is still safer and it takes blocks the flow for shorter time
        try
        {
            _profileUpdateCache.Clear();
            foreach (var pair in newCache)
            {
                _profileUpdateCache[pair.Key] = pair.Value;
            }
        }
        finally
        {
            _updateLock.Release();
            if (notify)
            {
                ProfileUpdateEvent?.Invoke();
            }
        }
    }

    private void RunInBackground(Func<Task> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                WriteLine($"Background Error: {ex}");
            }
        });
    }

    public void AddProfile(Profile profile)
    {
        bool exists = _allProfiles.Any(profile_ => profile_ == profile);
        if (exists) return;
        _profilesToAdd.Enqueue(profile);
        RunInBackground(ProcessProfilesAsync); // fire and forget because the errors are all handled in the other functions
        BackupFileManager.SaveProfile(profile);
    }

    public bool HasProfileUpdate(Guid id)
    {
        bool exists = _profileUpdateCache.TryGetValue(id, out var hasUpdate);
        return exists && hasUpdate;
    }


    // Specifically getting the ids from the UI because at any moment _allProfiles could be uddated and not yet displayed in the UI and that 
    // would make selecting the update of a backup not known it was there 
    public async Task UpdateAllAsync(List<Guid> ids)
    {
        for (int i = 0; i < ids.Count; ++i)
        {
            if (HasProfileUpdate(ids[i]))
            {
                await UpdateProfileAsync(ids[i], true); // ConfigureAwait(false)
            }
        }
        ProfileUpdateEvent?.Invoke();
    }

    public async Task UpdateProfileAsync(Guid id, bool updateAllProcess = false)
    {
        int index = _allProfiles.FindIndex(profile => profile.Id == id);
        if (index == -1) return;
        var profile = _allProfiles[index];
        var errors = await ProfileHandler.UpdatesBackupAsync(profile, _executor, _notifier).ConfigureAwait(false);
        DateTime updatedDate = DateTime.UtcNow;
        profile.SavedAt = updatedDate;
        BackupFileManager.UpdateProfileProperty<DateTime>(profile, "SavedAt", updatedDate);
        BackupFileManager.UpdateErroFile(profile, errors);
        if (!updateAllProcess) {
            ProfileUpdateEvent?.Invoke();
        }
    }

    private async Task ProcessProfilesAsync()
    {
        while (_profilesToAdd.TryDequeue(out var profile))
        {
            try
            {
                var errors = await ProfileHandler.StartBackupAsync(profile, _executor, _notifier);
                _allProfiles.Add(profile);
                BackupFileManager.UpdateErroFile(profile, errors);
                _profileUpdateCache[profile.Id] = false;
            }
            catch (Exception) { }
        }
    }



    // IU Functions

    public void DeleteProfileById(Guid profileId, bool deleteBackupFolder)
    {
        var index = _allProfiles.FindIndex(profile => profile.Id == profileId);
        if (index == -1)
        {
            return;
        }
        var profile = _allProfiles[index];
        BackupFileManager.DeleteProfile(profile, deleteBackupFolder);
        _profileUpdateCache.Remove(profile.Id);
        _allProfiles.RemoveAt(index);
    }

    public Profile? GetProfile(Guid id)
    {
        int index = _allProfiles.FindIndex(profile => profile.Id == id);
        if (index == -1)
        {
            return null;
        }
        return _allProfiles[index];
    }

    public void ChangeTrackFlag(Guid id, bool newFlag)
    {
        int index = _allProfiles.FindIndex(profile => profile.Id == id);
        if (index == -1) return; // not found
        var profile = _allProfiles[index];
        if (profile.KeepTrackFlag == newFlag) return; // no change 
        _allProfiles[index].KeepTrackFlag = newFlag;
        BackupFileManager.UpdateProfileProperty<bool>(profile, "KeepTrackFlag", newFlag);
    }

    public List<Tuple<string, string, Guid>> GetProfilesTableData()
    {
        List<Tuple<string, string, Guid>> profilesData = new List<Tuple<string, string, Guid>>();
        foreach (var profile in _allProfiles)
        {
            profilesData.Add(Tuple.Create(profile.Name, profile.SavedAt.ToString(), profile.Id));
        }
        return profilesData;
    }

    public void Dispose()
    {
        StopMountWatcher();
        StopUpdateWatcher();
    }
}