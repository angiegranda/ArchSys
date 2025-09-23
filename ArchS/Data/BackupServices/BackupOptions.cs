using ArchS.Data.Constants;
namespace ArchS.Data.BackupServices;

/// <summary>
/// Options to customize the backup process, it could be possible for some profiles to be schedule with a 
/// higher or lower degree of parallelism
/// </summary>
public sealed class BackupOptions
{
    public int ParallelismDegree { get; set; } = BackupProcessConstants.MAX_PARALLELISM;
    public int FileBufferSize { get; set; } = BackupProcessConstants.FILE_BUFFER_SIZE; // 1 MB
}