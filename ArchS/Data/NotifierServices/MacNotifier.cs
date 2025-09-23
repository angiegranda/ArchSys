using System.Diagnostics;
using ArchS.Data.BackupServices;
namespace ArchS.Data.NotifierServices; 

internal class MacNotifier : DesktopNotifier
{
    public override void Notify(string title, string message)
    {
        RunScript($"display notification \"{PrepareString(message)}\" with title \"{PrepareString(title)}\"");
    }

    public override void Progress(string profileName, BackupProgress progress)
    {
        string title = $"{profileName} - {progress.PercentItems:0.##}%";
        RunScript($"display notification \"{PrepareString(progress.State)}\" with title \"{PrepareString(title)}\"");
    }

    private static string PrepareString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " "); // escape backlashes, quotes and newlines
    }

    private static void RunScript(string script)
    {
        try
        {
            // writing to a temporary file to run the script
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".applescript");
            File.WriteAllText(path, script);
            Process.Start(new ProcessStartInfo("osascript", path)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch {}
    }
}
