namespace ArchS.Data.FileManager;

/// <summary>
/// Document wraps the needed information for a path, used in FileExplorerService.cs 
/// PathAccessState is set in PathScan.cs which decides wether to display the contents 
/// of the file/folder to the user or display instead an error message if the accessibility fails.
/// </summary>

public enum PathAccessState
{
    Success,
    NotFound,
    PermissionDenied,
    NotReadable,
    NotWritable,
    IsSymlink,
    IsDevice,
    Locked,
    UnknownError
}
public class Document
{
    public readonly string Path;
    public readonly bool IsFolder;
    public bool IsSelected { get; set; }
    public PathAccessState PathAccess { get; set; }

    public Document(string path, bool isFolder, bool isSelected, PathAccessState pathAccess)
    {
        Path = path;
        IsFolder = isFolder;
        IsSelected = isSelected;
        PathAccess = pathAccess;
    }

    public override string ToString()
    {
        return $"{(IsFolder ? "Directory:" : "File:")} {Path} with access ({PathAccess})";
    }
}