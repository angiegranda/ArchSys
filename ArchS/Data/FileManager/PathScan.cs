using ArchS.Data.Constants;
namespace ArchS.Data.FileManager;

/*
Cases where Read or Write permissions might not prevent errors when attempting to do so:
    - UnixFileMode can said readable but File.Open() throws FileNotFoundException for files deleted after checking metadata -> tmp files 
    - Files that are a symlink permission bits may look fine but the target might not exist or be inaccessible.
    - Remote mounts metadata shows readable but the server denies access when we actually try
    - File locked by another process caused when attemping to do many backups of the same big source data, IOException on open.
    - Mandatory Access Control like SELinux/AppArmor, metadata says readable, but policy denies the read -> MUST BE EXCPLICITLY CHANGED IN SETTINGS -> PRIVACY -> HARD DISK -> ALLOW TERMINAL
    - Directories such as /tmp, mode bits may show writable, but you can only delete your own files.
    - File has write bit set, but file system is mounted read-only.
*/

public static class PathScan 
{
    public static string GetFolderStateString(PathAccessState state)
    {
        return state switch
        {
            PathAccessState.NotFound => PathAccessStateMessage.NOT_FOUND,
            PathAccessState.PermissionDenied => PathAccessStateMessage.PERMISSION_DENIED,
            PathAccessState.NotReadable => PathAccessStateMessage.NOT_READABLE,
            PathAccessState.NotWritable => PathAccessStateMessage.NOT_WRITABLE,
            PathAccessState.IsSymlink => PathAccessStateMessage.SYMLINK,
            PathAccessState.Locked => PathAccessStateMessage.FILE_LOCKED,
            PathAccessState.UnknownError => PathAccessStateMessage.UNKNOWN,
            _ => "",
        };
    }
    
    private static bool IsDangerousPath(string path, string[] dangerousPaths)
    {
        foreach (var dangerousPath in dangerousPaths)
        {
            if (path.StartsWith(dangerousPath, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static PathAccessState DeepCheckFolder(string path, bool wantRead, bool wantWrite)
    { 
        if (wantRead)
        {
            try
            {
                _ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault(); //generic that can return both files and directories
            }
            catch (UnauthorizedAccessException)
            {
                return PathAccessState.PermissionDenied; 
            }
            catch (IOException)
            {
                return PathAccessState.Locked; // file is locked by another process
            }
        }
        if (wantWrite)
        {
            string temp = Path.Combine(path, ".archs_tmp_" + Guid.NewGuid());
            try
            {
                using (File.Create(temp)) { }
                File.Delete(temp);
            }
            catch (UnauthorizedAccessException)
            {
                return PathAccessState.PermissionDenied;
            }
            catch (IOException)
            {
                return PathAccessState.Locked;
            }
            catch (Exception)
            {
                return PathAccessState.UnknownError;
            }
        }
        return PathAccessState.Success;
    }

    private static PathAccessState DeepCheckFile(string path, bool wantRead, bool wantWrite)
    {
        if (wantRead)
        {
            try
            {
                // FileMode.Open throws exception if the file doesnt exist 
                // FileAccess.Read current access 
                // FileShare to indicate what other process can do with the file concurrently 
                using var s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (s.Length > 0) _ = s.ReadByte();
            }
            catch (UnauthorizedAccessException)
            {
                return PathAccessState.PermissionDenied;
            }
            catch (IOException)
            {
                return PathAccessState.Locked;
            }
            catch (Exception)
            {
                return PathAccessState.UnknownError;
            }
        }
        if (wantWrite)
        {
            try
            {
                using var s = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
            }
            catch (UnauthorizedAccessException)
            {
                return PathAccessState.PermissionDenied;
            }
            catch (IOException)
            {
                return PathAccessState.Locked;
            }
            catch (Exception)
            {
                return PathAccessState.UnknownError;
            }
        } 
        return PathAccessState.Success;
    }

    public static PathAccessState InspectUnixPath(string path, bool isFolder, bool wantRead, bool wantWrite, bool deepCheck)
    {
        if (string.IsNullOrWhiteSpace(path))
            return PathAccessState.NotFound;
        try
        {  
            if ((isFolder && !Directory.Exists(path)) || (!isFolder && !File.Exists(path))) return PathAccessState.NotFound;

            if (isFolder && path.StartsWith("/Volumes/")) // path were the external hard drives are mounted 
            { 
                deepCheck = false;
            }
            if (wantRead && IsDangerousPath(path, UnixDangerousPaths.READ_FORBIDDEN)) return PathAccessState.PermissionDenied;
            if (wantWrite && IsDangerousPath(path, UnixDangerousPaths.WRITE_FORBIDDEN)) return PathAccessState.PermissionDenied;

            FileSystemInfo info = isFolder ? new DirectoryInfo(path) : new FileInfo(path); // FileSystemInfo generic for FileInfo and DirectoryInfo

            if (!string.IsNullOrEmpty(info.LinkTarget)) // symlink check
            { 
                return PathAccessState.IsSymlink;
            }
            try
            {
                var mode = info.UnixFileMode;
                if (wantRead && (mode & (UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead)) == 0) return PathAccessState.NotReadable;
                if (wantWrite && (mode & (UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0) return PathAccessState.NotWritable;
            }
            catch
            {
                return PathAccessState.PermissionDenied;
            }

            if (deepCheck && isFolder) return DeepCheckFolder(path, wantRead, wantWrite);
            if (deepCheck && !isFolder) return DeepCheckFile(path, wantRead, wantWrite);

            return PathAccessState.Success;
        }
        catch (Exception)
        {
            return PathAccessState.UnknownError; // all other possible errors where already handled
        }
    }
}
