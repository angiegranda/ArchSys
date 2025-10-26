# Semiautomatic Archivation System for MacOs (Linux and Windows in progress)

#### Semestral project for Advanced C# course. This app a local desktop application with a browser-based UI for semi-automatically archiving files and directories to another location, such as a local or external drive. It tracks changes in your data and synchronizes only new or modified files. It also allows enable/disable keeping track on the changes. The app targets currently only MacOS but can be extended to function for Linux and Windows.

## Prerequisites & Setup

To run this Blazor Server application, make sure you have installed:  
- .NET 8 SDK 
- Visual Studio Code with the C# Dev Kit extension (Visual Studio 2022 is not supported for MAC) if you want to inspect-work on the code, not needed.  

To use the app follow the commands below:  
- Go to System Settings -> Privacy and Security -> Full Disk Access and enable the access of the terminal.
- Clone the repository: `git@github.com:angiegranda/ArchSys.git`
- Go to folder ./ArchS 
- Run in the terminal `dotnet run`
- Open your browser at `http://YOUR_IP_ADRESS:5124` address:
```
Microsoft.Hosting.Lifetime[14] 
Now listening on: [ADDRESS HERE]
```

## User Documentation

### Creating a Profile

A profile contains: 
- the source files and folders to copy 
- the target folder 
- the name of the backup, it should be unique within that folder, no spaces or fobidden characters, a message "Invalid name" will be displayed if the name is not suitable.  
- Keep track flag: can be enable/disable at any moment.  
- Keep structure: This option can only be set when creating a new profile. When enabled, data will be copied while preserving its original folder structure starting from the common parent path. If disabled, only the selected folders and files will be copied. In this case, renaming may occur if two selected items share the same name.

File explorer: The explorer is customized to provide a clear view of the current navigation path. On the left, you can select a file or folder; on the right, the contents of the selected directory are displayed. If the selected item cannot be accessed due to insufficient permissions or an error, a specific message will appear on the left. For files, the first few lines of their content are shown for preview.

After creation of the profile, there will be desktop notifications that the backup started and the progress when reaching 25% - 50% - 75% - 100% milestones. If the applications is shut down while the process runs then the remaning files can be updated (if the keep track flag is enabled) next time the app is launched. Once the process is completed, the profile will appear in **Managing your profiles**

### Managing your profiles 

There is a list of the profiles which both source files/folders and target folder are found. If the backup data was created from or to a external hard drive then it will only appear in the list when the hard drive is detected. 

If the Keep Track Flag is enabled and there has been a modified/added file in the source data a bottom `Update` will appear, once you clic it the backup of the newly modified/added files will be done. By clicking on the profile the information of the backup (source paths, target path, flags) is visualized and it is possible to perform the following options:   
-   Delete Backup:  Deleted the metadata file of the backup and the backup folder.
-   Delete Metadata: Deleted only the metada file of the backup so the app forgets about the backup and doesnt display it again.
-   Keep Track of Updates: Enable/disable automatic discover of modified/added files in the source files/folders.

The applications is constantly checking for updates and the speed of the detection and displayed as a bottom will vary depending on how much source data it needs to keep track, be patient waiting for it. 

## Developer Documentation 

### Technologies

-   Blazor Server: Web framework (part of ASP.NET Core) that runs UI components on the server, sends updates to the browser over SignalR in real time.  
-   ASP.NET Core: provides the hosting model, middleware pipeline, configuration system (appsettings.json), dependency injection, authentication/authorization, logging. Blazor Server runs inside an ASP.NET Core application.
-   xUnit: Unit testing framework, it allows custom test orderer to control test execution order.

### Features

- Multithread: Copying source-target paths are done in parallel up to max degree of parallelism. Processing Profiles are also done in parallel.   
- Reflection: Used to serialize modifying an attribute of the profile, such as the SavedAt time and the keep track flag.
UpdateProfileProperty function in BackupFileManager.cs. 
- Asyncronous Task: Mainly in BackupService and BackupExecutor, functions such as CopyFileAsync which uses IO operations 
so it does not block the calling thread from ExecuteAsync but before-after it uses threads from the thread pool.   
- LINQ: efficiently filter out empty lines, check for existence in a list, or take only the first N relevant lines—makes the intent readable and avoids extra loops.
- JSON serialization: Used to save the profile class data in targetfolder/.backup/profilename.json file. This is handled in BackupFileManager.cs. 
- Events: BackupService declares and event that ManageProfiles.razor component subscribes (through a method of type Action) and whenever information about the update of profiles is found it notified the UI to displayed the actualized data.   

### Code structure
```
├── App.razor.            # Contains Router component (it handle navigation between pages)
├── ArchS.csproj.         # Project configuration
├── Data
│   ├── AppServices
│   │   ├── BackupService.cs           # Manages the list of profiles (create, delete, update flag) and saves the metadata and contains two background workers, one for detecting external hardrives and another for discovering data to update.
│   │   ├── FileExplorerService.cs     # Custom File Explorer managed by ProfileCreationService 
│   │   └── ProfileCreationService.cs  # Manages the creation of the profile, the phases and the file explorer actions given the user input
│   ├── BackupServices         
│   │   ├── Archive.cs         # Wraps Source-Target path pairs to be updated and byte size of the source.
│   │   ├── BackupExecutor.cs  # 
│   │   ├── BackupOptions.cs   # Maximum degree of parallelism, copying buffer size and keep structure flag
│   │   ├── BackupPlan.cs      # Creates the archive for a given profile, it is a static class
│   │   └── BackupProgress.cs  # Saves the state of the backup progress and it is sent to the notifier
│   ├── Constants.cs              
│   ├── FileManager 
│   │   ├── BackupFileManager.cs. # Static class that handles the metadata files of the backups
│   │   ├── Document.cs           # Wraper of the necessary data of a path used in File Explorer
│   │   └── PathScan.cs           # Static class, given a path and information whether is a folder, want to read, want to write returns a PathAccessState result, it will help with the visualization of the document
│   ├── NotifierServices
│   │   ├── ConsoleNotifier.cs  # Debug notifier, prints to the terminal 
│   │   ├── DesktopNotifier.cs  # Abstract class that decides depending on the OS which notifier to return
│   │   ├── IBackupNotifier.cs  # Interface for the notifier class
│   │   └── MacNotifier.cs      # MacNotifier inherits from DesktopNotifier
│   └── ProfileManager
│       ├── Profile.cs          # Metadata for a backup 
│       └── ProfileHandler.cs   # Static class, calls BackupPlan to create the archive and uses the executor BackupExecutor given to process the new/updated backup 
├── Pages
│   ├── CreateProfile
│   │   ├── CreateProfile.razor      # Depending on the state of ProfileCreationService controls which page to display
│   │   ├── DocumentSelection.razor  # File Explorer UI display and gathering of source and target folders
│   │   └── SettingSelection.razor   # Form to take the name and flags for the backup, last stage of the profile creation
│   ├── Error.cshtml   
│   ├── Error.cshtml.cs
│   ├── Index.razor
│   ├── ManageProfiles
│   │   ├── ManageProfiles.razor    # Display list of backups and allows the update for those who can 
│   │   └── ProfileIndex.razor      # UI profile data display and delete-update tracking flag functionality
│   └── _Host.cshtml
├── Program.cs
├── Properties
│   └── launchSettings.json         # Defines the port the app runs on, http or https, enviroment settings
├── Shared                          # Default settings given by blazor app, common UI pieces shared across pages
├── _Imports.razor                  # Global using file for Razor components
├── appsettings.Development.json    # Default, general configuration but overrides settings when running in dev environment
├── appsettings.json                # Default, general configuration file for the app
└── wwwroot.                        # Web root folder. Stores all things served directly to the browser
```

