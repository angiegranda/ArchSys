using System.Runtime.InteropServices; //RuntimeInformation 
using ArchS.Data.BackupServices;
namespace ArchS.Data.NotifierServices;

/// <summary>
/// DesktopNotifier is a wrapper class with a static function that allows elegant creation 
/// of the corresponding notifier, can be extended to class inheriting DesktopNotifier
/// for windows and linux. 
/// </summary>
public abstract class DesktopNotifier : IBackupNotifier
{
    public abstract void Notify(string title, string message);
    public abstract void Progress(string profileName, BackupProgress progress);

    public static DesktopNotifier Create(bool debug = false) 
    {
        if (!debug && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) 
        {
            return new MacNotifier();  
        }
        return new ConsoleNotifier();
    }
}
