namespace ArchS.Data.BackupServices;

public sealed class ArchiveItem
{
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public long? SizeBytes { get; set; }
}
public sealed class Archive
{
    public List<ArchiveItem> Items { get; set; } = new List<ArchiveItem>();
    public long TotalBytes { get; set; }
}
