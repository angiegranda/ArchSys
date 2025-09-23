using ArchS.Data.BackupServices;
namespace ArchS.Data.NotifierServices;

// Debug notifier
class ConsoleNotifier : DesktopNotifier
{
    public override void Notify(string title, string message)
    {
        Console.WriteLine($"[Notification] {title}: {message}");
    }

    public override void Progress(string profileName, BackupProgress progress)
    {
        Console.WriteLine($"[Progress] {profileName} {progress.PercentItems:0.##}%: {progress.State}");
    }
}
