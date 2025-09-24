namespace ArchS.Data.BackupServices;

/// <summary>
/// This class records the process of the backup so it can be sent to Notifier and send notifications
/// about the backup progress.
/// </summary>
public sealed class BackupProgress
{
    public string State { get; set; } = "";
    public long BytesCopied { get; set; }
    public long TotalBytes { get; set; }
    public int TotalItems { get; set; }
    public int ItemsProcessed { get; set; }
    public double PercentItems => TotalItems == 0 ? 0 : (ItemsProcessed * 100.0 / TotalItems);
    public double PercentBytes => TotalBytes == 0 ? 0 : (BytesCopied * 100.0 / TotalBytes);
}

