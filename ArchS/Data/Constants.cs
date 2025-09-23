namespace ArchS.Data.Constants;

public static class PathAccessStateMessage
{
    public const string NOT_FOUND = "[NOT FOUND]";
    public const string EMPTY_DOCUMENT = "[EMPTY]";
    public const string PERMISSION_DENIED = "[DENIED ACCESS]";
    public const string NOT_READABLE = "[NOT READABLE]";
    public const string NOT_WRITABLE = "[NOT WRITABLE]";
    public const string SYMLINK = "[SYMLINK]";
    public const string DEVICE_FILE = "[DEVICE FILE]";
    public const string FILE_LOCKED = "[FILE LOCKED]";
    public const string UNKNOWN = "[EXCEPTION UNKNOWN]";
    public const string DISKFULL = "[DISK FULL]";

}

public static class HiddenFilesConstants
{
    public const string HIDDEN_PROGRAM_DIRECTORY = ".archs";
    public const string APP_HIDDEN_FILE = ".metadata";
    public const string PROFILE_HIDDEN_DIR = ".backup";
}

public static class UnixDangerousPaths
{
    public static readonly string[] READ_FORBIDDEN =
    {
        // Linux
        "/proc/",       // virtual filesystem with process/kernel info, can block
        "/sys/",        // kernel/hardware interface, can block or crash if written
        "/run/",        // runtime state, PID files, sockets, FIFOs
        "/var/run/",    // same as /run
        "/var/lock/",   // lock files

        // Both Linux and Mac
        "/dev/",        // device nodes, character/block devices, raw I/O
        "/lost+found/", // internal fs recovery, contains corrupted fragments

        // Mac
        "/System/Volumes/" // OS boot/system volumes, VM, snapshots
    };

    public static readonly string[] WRITE_FORBIDDEN =
    {
        // Mac
        "/System/Volumes/", "/System/Library/", "/System/DriverKit/", "/System/Cryptexes/",
        // Both Linux and Mac
        "/var/log/",   // logs, could break auditing
        "/var/cache/"  // system-managed cache
    };
}


public static class UIConstants
{
    public const int FILE_EXPLORER_NUM_ROWS = 17;
    public const int MAX_ROW_WIDTH = 40;
}

public static class KeysConstants
{
    // blazor names for arrow keys, DO NOT TOUCH 
    public const string ARROW_UP = "ArrowUp";
    public const string ARROW_DOWN = "ArrowDown";
    public const string ARROW_RIGHT = "ArrowRight";
    public const string ARROW_LEFT = "ArrowLeft";
    public const string ENTER = "Enter";
}

public static class BackupProcessConstants
{
    public const string BACKUP_STARTED = "Backup update started";
    public const string STAGE_STARTING = "Starting";
    public const string STAGE_RUNNING = "Running";
    public const string STAGE_COMPLETED = "Completed";
    public const string BACKUP_COMPLETED = "Backup finished successfully";
    public const string BACKUP_COMPLETED_WITH_ERRORS = "Backup finished with errors";
    public const int MAX_PARALLELISM = 10;
    public const int FILE_BUFFER_SIZE = 1024 * 1024; // 1MB 

}


// @code {
//     private void StartProfile()
//     {
//         ProfileCreationService.Start();
//         StateHasChanged();  // forces Blazor to render the page
//     }

//     private void Finish()
//     {
//         Profile? profile = ProfileCreationService.GetNewProfile();
//         if (profile is not null)
//         {
//             BackupService.AddProfile((Profile)profile); 
//             ProfileCreationService.ChangeState();
//         }
//         StateHasChanged();
//     }

// private void OnPhaseChanged()
// {
//     StateHasChanged();
// }
// }

// @code {
//     [Parameter] public required ProfileCreationService ProfileCreationService { get; set; }
//     [Parameter] public EventCallback PhaseChanged { get; set; }
//     private string? inputPath;

//     private async Task OnNextPhase()
//     {
//         if (!ProfileCreationService.HasSelection())
//         {
//             return;
//         }
//         ProfileCreationService.ChangeState();
//         await PhaseChanged.InvokeAsync(); 
//     }

//     private async Task OnCancel() 
//     {
//         ProfileCreationService.Disable();
//         await PhaseChanged.InvokeAsync(); 
//     }

//     private void Navigate() {
//         if (!string.IsNullOrWhiteSpace(inputPath)) {
//             ProfileCreationService.GotoPath(inputPath);
//             inputPath = string.Empty;
//         }
//     }

//     private void OnCheckboxChanged(int index)
//     {
//         ProfileCreationService.ToggleSelection(index); 
//         StateHasChanged();
//     }
 
//     private void MoveCursor(int index)
//     {
//         ProfileCreationService.ClickDocument(index);  // move cursor without navigation
//         StateHasChanged();
//     }

//     private void HandleKeyDown(KeyboardEventArgs e)
//     {
//         switch (e.Key)
//         {
//             case "ArrowUp":
//                 ProfileCreationService.HandleKey(KeysConstants.ARROW_UP);
//                 break;
//             case "ArrowDown":
//                 ProfileCreationService.HandleKey(KeysConstants.ARROW_DOWN);
//                 break;
//             case "ArrowLeft":
//                 ProfileCreationService.HandleKey(KeysConstants.ARROW_LEFT);
//                 break;
//             case "ArrowRight":
//                 ProfileCreationService.HandleKey(KeysConstants.ARROW_RIGHT);
//                 break;
//             case "Enter":
//                 ProfileCreationService.HandleKey(KeysConstants.ENTER);
//                 break;
//             default:
//             break;
//         }
//     }
// }

// @code {
//     [Parameter] public required ProfileCreationService ProfileCreationService { get; set; }
//     private string? profileName = null;
//     private bool keepTrackFlag;
//     private bool keepStructure;
//     private bool isValidName = true;

//     [Parameter] public EventCallback PhaseChanged { get; set; }

//     private async Task OnNextPhase()
//     {
//         if (!ProfileCreationService.IsValidName(profileName)) {
//             isValidName = false;
//             return;
//         }
//         ProfileCreationService.SetSettings(profileName!, keepTrackFlag, keepStructure);
//         ProfileCreationService.ChangeState();
//         await PhaseChanged.InvokeAsync(); 
//     }

//     private async Task OnCancel() 
//     {
//         ProfileCreationService.Disable();
//         await PhaseChanged.InvokeAsync();
//     }

// }


// @code {
//     private List<Tuple<string, string, Guid>> Profiles = new List<Tuple<string, string, Guid>>();

//     protected override void OnInitialized()
//     {
//         LoadProfiles();
//         BackupService.ProfileUpdateEvent += UpdateDisplayedData;
//     }

//     private void UpdateDisplayedData()
//     {
//         InvokeAsync(() =>
//         {
//             LoadProfiles();
//             StateHasChanged();
//         });
//     }

//     private void LoadProfiles()
//     {
//         Profiles = BackupService.GetProfilesTableData() ?? new List<Tuple<string, string, System.Guid>>();
//     }

//     private void OpenProfile(Guid id)
//     {
//         Navigation.NavigateTo($"/manage/{id}");
//     }

//     private async Task UpdateBackup(Guid id)
//     {
//         await BackupService.UpdateProfileAsync(id);
//         LoadProfiles();
//         StateHasChanged();
//     }

//     private async Task UpdateAllSafe() 
//     {
//         if (Profiles.Count == 0) return;
//         List<Guid> ids = Profiles.Select(info => info.Item3).ToList();
//         await BackupService.UpdateAllAsync(ids);
//         LoadProfiles();
//         StateHasChanged();
//     }

//     private bool HasProfileUpdate(Guid id)
//     {
//         return BackupService.HasProfileUpdate(id);  
//     }

// }

// @code {
//     [Parameter] public Guid Id { get; set; } 
//     private bool TrackFlag;

//     protected override void OnInitialized() // preapares the data, to render again StateHasChanged() 
//     {
//         TrackFlag = false;
//     }

//     private void MyStateHasChanged()
//     {
//         TrackFlag = false;
//         StateHasChanged(); 
//     }

//     private void GoBack()
//     {
//         Navigation.NavigateTo("/manage");
//     }

//     private void DeleteBackupSafe()
//     {
//         BackupService.DeleteProfileById(Id, true);  
//         MyStateHasChanged();
//     }

//     private void DeleteMetadataSafe()
//     {
//         BackupService.DeleteProfileById(Id, deleteBackupFolder: false);
//         MyStateHasChanged();

//     }

//     private void UpdateChangesSafe()
//     {
//         BackupService.ChangeTrackFlag(Id, TrackFlag);
//         MyStateHasChanged();
     
//     }
// }
