namespace ArchS.Data.BackupServices;

/// <summary>
/// This class records the process of the backup so it can be sent to Notifier and send notifications
/// about the backup progress. Also it records the Errors which later will be written to a hidden file 
/// .Errors with the respective exceptions 
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

