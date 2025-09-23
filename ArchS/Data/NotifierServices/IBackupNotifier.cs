using ArchS.Data.BackupServices;
namespace ArchS.Data.NotifierServices; 

/// <summary>
/// Notify - Start-Finish notification of the backup
/// Progress - Progress in percentage of the state of the backup 
/// </summary>
public interface IBackupNotifier
{
    void Notify(string title, string message);
    void Progress(string profileName, BackupProgress progress);
}
