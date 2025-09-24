using System.Text;
namespace ArchS.Data.ProfileManager;

/// <summary>
/// Wraps data needed for a backup
/// </summary>
public class Profile
{
    public Guid Id { get; init; } = Guid.NewGuid(); // very very small possibility of collision.
    public string Name { get; init; }
    public List<string> Folders { get; init; }
    public List<string> Files { get; init; }
    public string TargetPath { get; init; }
    public bool KeepStructure { get; init; }
    public bool KeepTrackFlag { get; set; }
    public DateTime SavedAt { get; set; }
    public Tuple<Dictionary<string, string>, Dictionary<string, string>>? Mapping { get; init; }

    public Profile(
        string name,
        List<string> folders,
        List<string> files,
        string targetPath,
        bool keepStructure,
        bool keepTrackFlag,
        Tuple<Dictionary<string, string>,
        Dictionary<string, string>>? mapping = null)
    {
        Name = name;
        Folders = new List<string>(folders);
        Folders.Sort();
        Files = new List<string>(files);
        Files.Sort();
        TargetPath = targetPath;
        KeepStructure = keepStructure;
        KeepTrackFlag = keepTrackFlag;
        SavedAt = DateTime.Now;
        Mapping = mapping;
    }

    public override string ToString()
    {
        StringBuilder output = new StringBuilder();
        output.AppendLine($"Profile Name: {Name}");
        foreach (var path in Folders)
        {
            output.AppendLine($"Folder: {path}");
        }
        foreach (var path in Files)
        {
            output.AppendLine($"Files: {path}");
        }
        output.AppendLine($"Keep Track: {KeepTrackFlag}, Keep structure: {KeepStructure}");
        output.AppendLine($"Target: {TargetPath}");

        if (Mapping != null)
        {
            var (folderMapping, filesMapping) = Mapping;
            if (folderMapping.Count > 0)
            {
                output.AppendLine($"Folder Mapping:");
                foreach (var mappingPair in folderMapping)
                {
                    output.AppendLine($"{mappingPair.Key} -> {mappingPair.Value}");
                }
            }
            if (filesMapping.Count > 0)
            {
                output.AppendLine($"Files Mapping:");
                foreach (var mappingPair in filesMapping)
                {
                    output.AppendLine($"{mappingPair.Key} -> {mappingPair.Value}");
                }
            }
        }
        return output.ToString();
    }

    public static bool operator ==(Profile a, Profile b) => a.Id == b.Id;
    public static bool operator !=(Profile a, Profile b) => !(a == b);
    public override bool Equals(object? objProfile) => objProfile is Profile other && this == other;
    public override int GetHashCode() => HashCode.Combine(Name, TargetPath);
}