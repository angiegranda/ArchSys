using ArchS.Data.Constants;
using ArchS.Data.BackupServices;
using ArchS.Data.NotifierServices;
namespace ArchS.Data.ProfileManager;

/// <summary>
/// Provides functions to create a new backup, retrive the archive (items info to update), or handles the update af an already existing one 
/// </summary>
public static class ProfileHandler
{
    public static async Task<List<string>> StartBackupAsync(Profile profile, BackupExecutor executor, DesktopNotifier notifier)
    {
        notifier.Notify(profile.Name, BackupProcessConstants.BACKUP_STARTED);

        string targetRoot = Path.Combine(profile.TargetPath, profile.Name);
        Directory.CreateDirectory(targetRoot);

        string? commonParent = null;
        if (profile.KeepStructure)
        {
            commonParent = FindCommonParent(profile.Folders.Concat(profile.Files));
        }
        var (archive, errors1) = BackupPlan.BuildArchive(false, profile, commonParent);
        
        List<string> errors2 = await executor.ExecuteAsync(profile.Name, archive);
        return errors1.Concat(errors2).ToList();
    }

    public static (Archive, List<string>) GetArchiveItemToUpdate(Profile profile)
    {
        string? commonParent = null;
        if (profile.KeepStructure)
        {
            commonParent = FindCommonParent(profile.Folders.Concat(profile.Files));
        }
        return BackupPlan.BuildArchive(true, profile, commonParent);
    }

    public static async Task<List<string>> UpdatesBackupAsync(Profile profile, BackupExecutor executor, DesktopNotifier notifier)
    {
        List<string> errors = new List<string>();
        var (archive, errors1) = GetArchiveItemToUpdate(profile);
        errors.AddRange(errors1);
        if (archive.Items.Count > 0)
        {
            notifier.Notify(profile.Name, BackupProcessConstants.BACKUP_STARTED);
            List<string> errors2 = await executor.ExecuteAsync(profile.Name, archive);
            errors.AddRange(errors2);
        }
        return errors;
    }

    private static string? FindCommonParent(IEnumerable<string> paths)
    {
        var allPathsParts = new List<string[]>();
        foreach (var path in paths)
        {
            var absolutePath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            var pathParts = absolutePath.Split(Path.DirectorySeparatorChar);
            allPathsParts.Add(pathParts);
        }
        if (allPathsParts.Count == 1) return null;

        var first = allPathsParts[0].ToArray();
        for (int i = 1; i < allPathsParts.Count; i++) // i=1 because the root is "" and that is common
        {
            var curr = allPathsParts[i]; // the i-th path parts
            var minLength = Math.Min(first.Length, curr.Length); // if all are folders at the root then it equals to 1 
            var temp = new List<string>();
            for (int j = 0; j < minLength; j++)
            {
                if (!string.Equals(first[j], curr[j], StringComparison.OrdinalIgnoreCase)) break; // mismatch
                temp.Add(first[j]);
            }
            first = temp.ToArray();
        }
        if (first.Length == 1) return null; // the common path is root
        return Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar, first.Skip(1));
    }
}
