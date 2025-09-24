using ArchS.Data.FileManager;
using ArchS.Data.ProfileManager;
using ArchS.Data.Constants;
namespace ArchS.Data.AppServices;

/// <summary>
/// This class handles the creation of a profile through States. It communicates with the components in Pages/CreateProfile
/// and uses FileExplorerService to display data in the UI, perform actions (go down up, move to parent or selected folder)
/// BackupService receives the created profile.
/// </summary>

public enum State { SelectDocuments, Settings, SelectTarget, Finished, Unused };

public class ProfileCreationService
{
    private FileExplorerService _explorer;
    public State state { get; private set; }
    private List<string> _selectedFolders = new List<string>();
    private List<string> _selectedFiles = new List<string>();
    private string? _targetPath;
    private bool _keepTrackFlag;
    private bool _keepStructure;
    private string? _profileName;


    private void Clear()
    {
        _selectedFolders.Clear();
        _selectedFiles.Clear();
        _targetPath = null;
        _keepTrackFlag = false;
        _keepStructure = false;
        _profileName = null;
    }

    private void SelectDocumentsHandle(string key)
    {
        switch (key)
        {
            case KeysConstants.ARROW_UP:
                _explorer.GoUp();
                break;
            case KeysConstants.ARROW_DOWN:
                _explorer.GoDown();
                break;
            case KeysConstants.ARROW_LEFT:
                _explorer.GoToParent();
                break;
            case KeysConstants.ARROW_RIGHT:
                _explorer.GoToChild();
                break;
            case KeysConstants.ENTER:
                _explorer.SelectDocument();
                break;
            default:
                break;
        }
    }

    public void Disable()
    {
        Clear();
        state = State.Unused;
    }

    public ProfileCreationService()
    {
        state = State.Unused;
        _explorer = new FileExplorerService();
    }

    public Profile? GetNewProfile()
    {
        if (state != State.Finished) { return null; }
        Tuple<Dictionary<string, string>, Dictionary<string, string>>? mapping = null;
        if (!_keepStructure)
        {
            mapping = BackupFileManager.GetProfileMapping(_selectedFolders, _selectedFiles);
        }
        return new Profile(_profileName!, _selectedFolders, _selectedFiles, _targetPath!, _keepStructure, _keepTrackFlag, mapping);
    }

    public void Start()
    {
        Clear();
        _explorer.ClearAndSetState(true);
        state = State.SelectDocuments;
    }

    public void ChangeState()
    {
        switch (state)
        {
            case State.SelectDocuments:
                if (_explorer!.HasSelectedDocuments)
                {   // IReadOnlyList is still a reference to the List, just constant so it is necessary to copy
                    _selectedFolders = new List<string>(_explorer.GetSelectedFolders);
                    _selectedFiles = new List<string>(_explorer.GetSelectedFiles);
                    _explorer.ClearAndSetState(false);
                    state = State.SelectTarget;
                }
                break;
            case State.SelectTarget:
                if (_explorer!.HasSelectedDocuments)
                {
                    _targetPath = _explorer.GetSelectedFolders[0];
                    state = State.Settings;
                }
                break;
            case State.Settings:
                state = State.Finished;
                break;
            case State.Finished:
                Disable();
                break;
            default:
                break;
        }
    }

    public bool IsValidName(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return false;
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (filename.IndexOfAny(invalidChars) >= 0) return false;
        var backupFolderPath = Path.Combine(_targetPath!, filename);
        if (Directory.Exists(backupFolderPath)) return false;
        return true;
    }

    public void HandleKey(string key)
    {
        switch (state)
        {
            case State.SelectDocuments:
                SelectDocumentsHandle(key);
                break;
            case State.Settings:
                break;
            case State.SelectTarget:
                SelectDocumentsHandle(key);
                break;
            default:
                break;
        }
    }

    public void GotoPath(string path) => _explorer.GoToPath(path);

    public void SetSettings(string profileName, bool keepTrackFlag, bool keepStructure)
    {
        _profileName = profileName;
        _keepTrackFlag = keepTrackFlag;
        _keepStructure = keepStructure;
    }


    /* DISPLAY FUNCTIONS */

    private string Truncate(string input)
    {
        return input.Length > UIConstants.MAX_ROW_WIDTH
            ? input.Substring(0, UIConstants.MAX_ROW_WIDTH)
            : input;
    }

    private void PrepareColumnContents(
        int startIndex, int endIndex,
        IReadOnlyList<Document> contents,
        List<string> folders, List<string> files)
    {
        for (int i = startIndex; i < endIndex; ++i)
        {
            var name = Truncate(Path.GetFileName(contents[i].Path));
            if (contents[i].IsFolder)
            {
                folders.Add(name);
            }
            else
            { 
                files.Add(name);
            }
        }
    }

    public Tuple<List<string>, List<string>, List<string>, List<string>, List<bool>, bool, int> SelectDocumentsData()
    {
        var currDocs = _explorer.GetCurrDocuments;

        List<string> currData1 = new List<string>();
        List<string> currData2 = new List<string>();
        List<string> nextData1 = new List<string>();
        List<string> nextData2 = new List<string>();
        List<bool> selected = new List<bool>();

        PrepareColumnContents(0, currDocs.Count, currDocs, currData1, currData2);

        for (int i = 0; i < currDocs.Count; ++i)
        {
            selected.Add(currDocs[i].IsSelected);
        }
        int cursorPos = _explorer.CurrIndex;
        bool isTextDisplay = false;
        
        if (currDocs[_explorer.CurrIndex].PathAccess != PathAccessState.Success)
        {
            nextData1.Add(PathScan.GetFolderStateString(currDocs[_explorer.CurrIndex].PathAccess));
            return Tuple.Create(currData1, currData2, nextData1, nextData2, selected, true, cursorPos);
        }

        if (currDocs[_explorer.CurrIndex].IsFolder)
        {
            var nextDocs = _explorer.GetNextDocuments;
            PrepareColumnContents(0, nextDocs.Count, nextDocs, nextData1, nextData2);
        }
        else 
        {
            foreach (var line in _explorer.GetText)
            {
                nextData1.Add(Truncate(line));
            }
            isTextDisplay = true;
        }
        return Tuple.Create(currData1, currData2, nextData1, nextData2, selected, isTextDisplay, cursorPos);
    }

    public void ClickDocument(int index) => _explorer.SetIndex(index);
    public void ToggleSelection(int index)
    {
        _explorer.SetIndex(index);
        _explorer.SelectDocument();
    }

    public bool HasSelection() => _explorer!.HasSelectedDocuments;
    public bool FolderState(int index) => _explorer.CheckFileState(index);
    public IReadOnlyList<Document> SelectDocumentsDocs => _explorer.GetCurrDocuments;

}