using ArchS.Data.BackupServices;
using ArchS.Data.ProfileManager;
using ArchS.Data.FileManager;
using ArchS.Data.NotifierServices;
using System.Collections.Concurrent;
using static System.Console;
using System.Runtime.InteropServices;
namespace ArchS.Data.AppServices;

/// <summary>
/// This class communicates with the UI and allows to:
///     - Add a profile
///     - Update a specific profile (automatic detection of changes and semi-automatic update)
///     - Update the keep track flag 
///     - Remove the metadata of the profile so that the app forgets about it 
///     with the option to remove also the backup the backup folder
/// Then it has two background workers:
///     - The first, checks every 3 seconds (+ the time needed to explore all data) if there are files 
///     added or modified in the source file-files of the backups who have enable the keep track flag, 
///     if it finds such files, it will send a notification to UI to display the update bottom to those 
///     who enabled it. 
///     - The seconds checks every 5 seconds if there is a new discovered hard drive connected and if 
///     it does, checks if there is any profile that was not displayed yet and contain the path of the 
///     hard drive as source or target, this is done in BackupFileManager.cs
/// Adding the profiles is performed sequencially, processing the files to copy is done in parallel
/// </summary>

public class BackupService : IDisposable
{
    private List<Profile> _allProfiles;
    private ConcurrentQueue<Profile> _profilesToAdd;
    private Dictionary<Guid, bool> _profileUpdateCache = new Dictionary<Guid, bool>(); 
    public IReadOnlyDictionary<Guid, bool> ProfileUpdateCache => _profileUpdateCache; 
    private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);
    // at most one thread can enter immediately and only one is allowed during the process
    // SemaphoreSlim works as a mutex but can be used in async methods because of WaitAsync()
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
    /// Background worker --- Detects newly found paths in the /Volumes directory 
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
    /// Background worker --- Checks constantly for added/modified data in source paths for profiles 
    /// who enables keep track of updates
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

    private void RunInBackground(Func<Task> work) //safe fire and forget
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
        RunInBackground(ProcessProfilesAsync); // Errors are all handled in the other functions
        BackupFileManager.SaveProfile(profile);
    }

    public bool HasProfileUpdate(Guid id)
    {
        bool exists = _profileUpdateCache.TryGetValue(id, out var hasUpdate);
        return exists && hasUpdate;
    }


    /// <summary>
    /// We get the IDs directly from the UI because _allProfiles may be updated in the background 
    // and not yet reflected in the UI. This ensures that selecting a backup to update uses the IDs 
    // that are currently visible and known to the user
    /// </summary>
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
        DateTime updatedDate = DateTime.Now;
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
        if (index == -1) return;
        var profile = _allProfiles[index];
        BackupFileManager.DeleteProfile(profile, deleteBackupFolder);
        _profileUpdateCache.Remove(profile.Id);
        _allProfiles.RemoveAt(index);
    }

    public Profile? GetProfile(Guid id)
    {
        int index = _allProfiles.FindIndex(profile => profile.Id == id);
        if (index == -1) return null;
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