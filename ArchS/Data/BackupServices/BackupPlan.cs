using ArchS.Data.FileManager; //BackupFileManager 
using ArchS.Data.ProfileManager;
using Microsoft.AspNetCore.Http.Features;
using System.Collections.Concurrent;
namespace ArchS.Data.BackupServices;

public sealed class BackupPlan 
{
    /// <summary>
    /// Cases:
    /// - KeepStructure is true, which means starting at the common parent path of the selected files/folders and then copy only the intended information 
    /// into the backup file while keeping the internal folders structure.
    ///     - common parent is null when: 
    ///             - case 1: there is a single source file/folder
    ///             - the common parent is the root 
    /// - No keep Structures folder and files will be directly copied to the directory managing name collitions 
    /// This function handles the filePath to copy, but whether there is a renaming of the folder due to collisions or other cases on 
    /// forming the target file path to be created must be handed here
    ///</summary>
    private static ArchiveItem? GetArchiveItem(bool isUpdate, Profile profile, string originalSelectedPath, string filePath, string? commonParent)
    {
        string targetPath;
        if (!profile.KeepStructure)
        {
            if (string.Equals(originalSelectedPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                // file
                targetPath = profile.Mapping!.Item2.ContainsKey(originalSelectedPath) ?
                Path.Combine(profile.TargetPath, profile.Name, profile.Mapping!.Item2[originalSelectedPath]) :
                Path.Combine(profile.TargetPath, profile.Name, Path.GetFileName(originalSelectedPath));
            }
            else
            {
                // Path.GetRelativePath("/a/b", "/a/b/c/d/file.txt") returns "c/d/file.txt"
                var parent = Path.GetDirectoryName(originalSelectedPath)!;
                string relativePath = Path.GetRelativePath(parent, filePath);
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                targetPath = profile.Mapping!.Item1.ContainsKey(originalSelectedPath) ?
                Path.Combine(profile.TargetPath, profile.Name, profile.Mapping!.Item1[originalSelectedPath], string.Join(Path.DirectorySeparatorChar, pathParts.Skip(1))) :
                Path.Combine(profile.TargetPath, profile.Name, relativePath);
            }
        }
        else
        {
            string relativePath;
            if (commonParent == null)
            {
                if (profile.Folders.Count == 0) // a single file was selected, only copying the file 
                {
                    relativePath = Path.GetFileName(filePath);
                }
                else // folders and files could have been selected, all have in common the root
                {
                    relativePath = filePath.TrimStart(Path.DirectorySeparatorChar); ; // without the root slash
                }
            }
            else
            {
                relativePath = Path.GetRelativePath(commonParent, filePath);
            }
            targetPath = Path.Combine(profile.TargetPath, profile.Name, relativePath);
        }
        FileInfo sourceInfo;
        FileInfo targetInfo;
        try
        {
            sourceInfo = new FileInfo(filePath);
            targetInfo = new FileInfo(targetPath);
        }
        catch
        {
            throw;
        }

        if (!targetInfo.Exists ||
        (isUpdate && sourceInfo.LastWriteTimeUtc > targetInfo.LastWriteTimeUtc))
        {
            return new ArchiveItem
            {
                SourcePath = filePath,
                TargetPath = targetPath,
                SizeBytes = sourceInfo.Length
            };
        }
        return null;
    }

    public static (Archive, List<string>) BuildArchive(bool isUpdate, Profile profile, string? commonParent)
    {
        var archive = new Archive();
        var archiveItems = new ConcurrentBag<ArchiveItem>();
        var failedPaths = new ConcurrentBag<string>();
        Parallel.ForEach(profile.Folders, folderPath =>
        {
            var folderState = PathScan.InspectUnixPath(folderPath, isFolder: true, wantRead: true, wantWrite: false, deepCheck: false);
            if (folderState != PathAccessState.Success)
            {
                failedPaths.Add($"[PATH]: {folderPath} [ERROR]: {PathScan.GetFolderStateString(folderState)}"); 
                return; 
            }
            // here is the error, so we want to copy to the backup /last folder name of folderPath and so on in the future .... 
            // but since we use recursion I get lost with this folderPath and so the path is changed and I cannot keep track of it 
            EnumerateSafe(isUpdate, folderPath, folderPath, archiveItems, failedPaths, profile, commonParent);
        });

        var filePaths = profile.Files.ToList();
        Parallel.ForEach(filePaths, filePath =>
        {
            var fileState = PathScan.InspectUnixPath(filePath, isFolder: false, wantRead: true, wantWrite: false, deepCheck: false);
            if (fileState != PathAccessState.Success)
            {
                failedPaths.Add(filePath);
                return;
            }
            try
            {
                ArchiveItem? item = GetArchiveItem(isUpdate, profile, filePath, filePath, commonParent);
                if (item is not null)
                {
                    archiveItems.Add(item); // else it can be un unnecessary update
                }
            }
            catch (Exception ex)
            {
                failedPaths.Add($"[PATH]: {filePath} [ERROR]: {ex.Message}");
            }
        });

        archive.Items = archiveItems.ToList();
        archive.TotalBytes = archive.Items.Sum(i => i.SizeBytes ?? 0);
        return (archive, failedPaths.ToList());
    }

    private static void EnumerateSafe(bool isUpdate, string folderPath, string originalFolder, ConcurrentBag<ArchiveItem> archiveItems,
        ConcurrentBag<string> failedPaths, Profile profile, string? commonParent)
    {
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(folderPath);
        }
        catch (Exception ex)
        {
            failedPaths.Add($"[PATH]: {folderPath} [ERROR]: {ex.Message}");
            return;
        }

        foreach (var sourcePath in entries)
        {
            bool isDir = Directory.Exists(sourcePath);

            var pathState = PathScan.InspectUnixPath(sourcePath, isFolder: isDir, wantRead: true, wantWrite: false, deepCheck: false);
            if (pathState != PathAccessState.Success)
            {
                failedPaths.Add($"[PATH]: {sourcePath} [ERROR]: {PathScan.GetFolderStateString(pathState)}");
                continue;
            }
            if (isDir)
            {
                EnumerateSafe(isUpdate, sourcePath, originalFolder, archiveItems, failedPaths, profile, commonParent);
                continue;
            }
            try
            {
                ArchiveItem? item = GetArchiveItem(isUpdate, profile, originalFolder, sourcePath, commonParent);
                if (item is not null)
                {
                    archiveItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                failedPaths.Add($"[PATH]: {sourcePath} [ERROR]: {ex.Message}");
            }

        }
    }
}