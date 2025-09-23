using System.Reflection;
using System.Text.Json;
using ArchS.Data.Constants;
using ArchS.Data.ProfileManager;
namespace ArchS.Data.FileManager;


/// <summary>
/// This class handles writing the target paths to _globalHiddenFile, and then the metadata of the profile
/// is saved in targetfolder/.backup/profilename.json <- which uses json seriaization, reflexion to modify 
/// currently only the savedat time and the keep update flag.
/// </summary>
public static class BackupFileManager
{
    private static readonly string _globalHiddenFile;
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
    {
        WriteIndented = true
    };
    private static List<string> _targetPathNotFound = new List<string>();
    private static List<Profile> _profilesPathsIncomplete = new List<Profile>();
    static BackupFileManager()
    {
        string appHiddenFolder;
        appHiddenFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            HiddenFilesConstants.HIDDEN_PROGRAM_DIRECTORY);
        Directory.CreateDirectory(appHiddenFolder);
        _globalHiddenFile = Path.Combine(appHiddenFolder, HiddenFilesConstants.APP_HIDDEN_FILE);
        if (!File.Exists(_globalHiddenFile))
        {
            File.WriteAllText(_globalHiddenFile, string.Empty);
        }
    }

    public static bool CheckProfilePathsExistence(Profile profile)
    {
        foreach (var folderPath in profile.Folders)
        {
            if (!Directory.Exists(folderPath))
            {
                return false;
            }
        }
        foreach (var filePath in profile.Files)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }
        }
        if (!Directory.Exists(profile.TargetPath))
        {
            return false;
        }
        return true;
    }

    private static (List<Profile>, List<Profile>, List<string>) ProcessTargetPaths(List<string> targetPaths)
    {
        List<Profile> profiles = new List<Profile>();
        List<Profile> failedProfiles = new List<Profile>();
        List<string> notFoundTargetPaths = new List<string>();

        for (int i = 0; i < targetPaths.Count; ++i)
        {
            string targetHiddenFolder = Path.Combine(targetPaths[i], HiddenFilesConstants.PROFILE_HIDDEN_DIR);
            if (!Directory.Exists(targetHiddenFolder))
            {
                notFoundTargetPaths.Add(targetPaths[i]); // the target path could be a hardrive
                continue;
            }
            foreach (var hiddenFilePath in Directory.GetFiles(targetHiddenFolder, "*.json"))
            {
                try
                {
                    Profile? profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(hiddenFilePath));
                    if (profile is not null)
                    {
                        Profile profile_ = (Profile)profile;
                        if (CheckProfilePathsExistence(profile_))
                        {
                            profiles.Add(profile_);
                        }
                        else
                        {
                            failedProfiles.Add(profile_); // source paths not found, hard drive case again
                        }
                    }
                }
                catch { /* json file, not serialized to profile */}
            }
        }

        return (profiles, failedProfiles, notFoundTargetPaths);
    }

    private static (List<Profile>, List<Profile>) CheckProfilesPathIncomplete()
    {
        List<Profile> profilesSuccessful = new List<Profile>();
        List<Profile> profilesFailure = new List<Profile>();
        for (int i = 0; i < _profilesPathsIncomplete.Count; ++i)
        {
            if (CheckProfilePathsExistence(_profilesPathsIncomplete[i]))
            {
                profilesSuccessful.Add(_profilesPathsIncomplete[i]);
            }
            else
            {
                profilesFailure.Add(_profilesPathsIncomplete[i]);
            }
        }
        return (profilesSuccessful, profilesFailure);
    }

    public static List<Profile> GetProfiles()
    {
        var targetPaths = File.Exists(_globalHiddenFile)
            ? File.ReadAllLines(_globalHiddenFile).Where(line => !string.IsNullOrWhiteSpace(line)).ToList() // each line is a target path
            : new List<string>();

        var (validProfiles, failedProfiles, failedTargetPaths) = ProcessTargetPaths(targetPaths);
        _targetPathNotFound = new List<string>(failedTargetPaths);
        _profilesPathsIncomplete = new List<Profile>(failedProfiles);
        return validProfiles;
    }

    public static List<Profile> CheckMountedPaths()
    {
        var (validProfiles, failedProfiles, failedTargetPaths) = ProcessTargetPaths(_targetPathNotFound);
        _targetPathNotFound = new List<string>(failedTargetPaths);
        _profilesPathsIncomplete.Concat(failedProfiles);
        var (successfulProfile, unsuccessfulProfiles) = CheckProfilesPathIncomplete(); // will be processed here _profilesPathsIncomplete
        _profilesPathsIncomplete = new List<Profile>(unsuccessfulProfiles);
        return successfulProfile.Concat(validProfiles).ToList();
    }

    public static void SaveProfile(Profile profile)
    {
        var targetPaths = File.Exists(_globalHiddenFile)
            ? File.ReadAllLines(_globalHiddenFile).ToHashSet()
            : new HashSet<string>();

        if (!targetPaths.Contains(profile.TargetPath))
        {
            targetPaths.Add(profile.TargetPath);
            File.WriteAllLines(_globalHiddenFile, targetPaths);
        }

        string targetHiddenFolder = Path.Combine(profile.TargetPath, HiddenFilesConstants.PROFILE_HIDDEN_DIR); // .backup
        Directory.CreateDirectory(targetHiddenFolder);

        string fileName = $"{profile.Name}.json";
        string filePath = Path.Combine(targetHiddenFolder, fileName);
        File.WriteAllText(filePath, JsonSerializer.Serialize(profile, _jsonOptions));
    }

    public static void DeleteProfile(Profile profile, bool deleteBackupFolder)
    {
        string hiddenDir = Path.Combine(profile.TargetPath, HiddenFilesConstants.PROFILE_HIDDEN_DIR);
        if (!Directory.Exists(hiddenDir)) return;
        string[] files = Directory.GetFiles(hiddenDir);
        string hiddenFilePath = Path.Combine(profile.TargetPath, HiddenFilesConstants.PROFILE_HIDDEN_DIR, $"{profile.Name}.json");
        if (files.Any(file => file == hiddenFilePath)) // stops at first match
        {
            File.Delete(hiddenFilePath);
        }
        string[] remaning = Directory.GetFiles(hiddenDir);
        if (remaning.Length == 0)
        {
            var targetPaths = File.Exists(_globalHiddenFile)
            ? File.ReadAllLines(_globalHiddenFile).ToHashSet()
            : new HashSet<string>();
            targetPaths.Remove(profile.TargetPath); // returns true if removed and false if it wasnt
            File.WriteAllLines(_globalHiddenFile, targetPaths); // this will overwrite the file
        }
        if (deleteBackupFolder)
        {
            string profilePath = Path.Combine(profile.TargetPath, profile.Name);
            if (!Directory.Exists(profilePath)) return;
            Directory.Delete(profilePath, recursive: true);
        }
    }

    public static void UpdateProfileProperty<T>(Profile profile, string propertyName, T newValue)
    {
        PropertyInfo? propertyInfo = typeof(Profile).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        if (propertyInfo == null)
            return;

        propertyInfo.SetValue(profile, newValue);
        string hiddenFilePath = Path.Combine(profile.TargetPath, HiddenFilesConstants.PROFILE_HIDDEN_DIR, $"{profile.Name}.json");
        File.WriteAllText(hiddenFilePath, string.Empty);
        File.WriteAllText(hiddenFilePath, JsonSerializer.Serialize(profile, _jsonOptions));
    }

    public static Tuple<Dictionary<string, string>, Dictionary<string, string>> GetProfileMapping(List<string> folderPaths, List<string> filePaths)
    {
        Action<Dictionary<string, List<string>>, List<string>> GetDictGroups = (groups, list) =>
        {
            foreach (var path in list)
            {
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fileName)) continue; // root
                if (!groups.ContainsKey(fileName!))
                {
                    groups[fileName!] = new List<string>();
                }
                groups[fileName].Add(path); // list of paths that have the same folder  or file name
            }
        };

        Action<Dictionary<string, List<string>>, Dictionary<string, string>, bool> GetNewPaths = (groups, mapping, isFolder) =>
        {
            foreach (var pair in groups)
            {
                string commonName = pair.Key;
                List<string> paths = pair.Value;
                if (paths.Count == 1) continue;
                int counter = 1;
                foreach (var path in paths)
                {
                    if (isFolder)
                    {
                        mapping[path] = $"{commonName}({counter})";
                    }
                    else
                    {
                        var filename = Path.GetFileNameWithoutExtension(commonName);
                        var extension = Path.GetExtension(commonName);
                        mapping[path] = $"{filename}({counter}){extension}";
                    }
                    ++counter;
                }
            }
        };

        Dictionary<string, string> foldersMapping = new Dictionary<string, string>();
        Dictionary<string, string> filesMapping = new Dictionary<string, string>();
        Dictionary<string, List<string>> groupsFolders = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> groupsFiles = new Dictionary<string, List<string>>();
        GetDictGroups(groupsFolders, folderPaths);
        GetDictGroups(groupsFiles, filePaths);
        GetNewPaths(groupsFolders, foldersMapping, true);
        GetNewPaths(groupsFiles, filesMapping, false);

        return Tuple.Create(foldersMapping, filesMapping);
    }

    public static void UpdateErroFile(Profile profile, List<string> list)
    {
        string errorFilePath = Path.Combine(profile.TargetPath, profile.Name, $".Error");
        if (list.Count != 0)
        {
            File.WriteAllLines(errorFilePath, list);
            return;
        }
        File.Delete(errorFilePath); // no exception is thrown if it doesnt exist
    }
}
