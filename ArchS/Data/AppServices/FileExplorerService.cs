using System.IO;
using System.Linq;
using System.Collections.Generic;
using ArchS.Data.FileManager; // PathScan Document 
using ArchS.Data.Constants;
namespace ArchS.Data.AppServices;

public enum ExplorerState { MultipleSelection, SingleSelection };
public class FileExplorerService
{
    private ExplorerState _state;
    private HashSet<string> _selectedFolders = new HashSet<string>();
    private HashSet<string> _selectedFiles = new HashSet<string>();
    private List<Document> _currentDocuments = new List<Document>();
    private List<Document> _nextDocuments = new List<Document>();
    private List<string> _fileContents = new List<string>();
    private string _currentPath;
    private int _currIndex;

    private void checkSelectedDocs(List<Document> documents)
    {
        foreach (var doc in documents)
        {
            if (doc.IsFolder)
            {
                doc.IsSelected = _selectedFolders.Contains(doc.Path);
            }
            else
            {
                doc.IsSelected = _selectedFiles.Contains(doc.Path);
            }
        }
    }

    private void LoadContents(List<Document> documents, Document document)
    {
        Func<IEnumerable<string>, List<Document>, bool, bool> GetCurrDocumentsSafe = (toIterate, output, areFolders) =>
        {
            bool isAnyValid = false;
            foreach (var file in toIterate)
            {
                PathAccessState fileState;

                try
                {
                    fileState = PathScan.InspectUnixPath(file, areFolders, true, _state == ExplorerState.SingleSelection, false);
                }
                catch (UnauthorizedAccessException)
                {
                    fileState = PathAccessState.PermissionDenied;
                }
                catch (IOException)
                {
                    fileState = PathAccessState.Locked;
                }

                output.Add(new Document(file, areFolders, false, fileState));
                if (fileState == PathAccessState.Success)
                    isAnyValid = true;
            }
            return isAnyValid;
        };

        documents.Clear();

        IEnumerable<string> dirs = Enumerable.Empty<string>();
        IEnumerable<string> files = Enumerable.Empty<string>();

        try { dirs = Directory.EnumerateDirectories(document.Path); } catch { }
        try { files = Directory.EnumerateFiles(document.Path); } catch { }

        bool anyValidFolder = GetCurrDocumentsSafe(dirs, documents, true);
        bool anyValidFile = GetCurrDocumentsSafe(files, documents, false);
    }

    private void Update_nextDocuments()
    {
        if (_currentDocuments.Count == 0) return;

        if (_currentDocuments[_currIndex].PathAccess == PathAccessState.Success)
        {
            if (_currentDocuments[_currIndex].IsFolder)
            {
                LoadContents(_nextDocuments, _currentDocuments[_currIndex]);
            }
            else
            {
                var lines = ReadCurrentDocument();
                _fileContents = new List<string>(lines);
            }
        }
        else
        {
            _nextDocuments.Clear();
            _fileContents.Clear();
        }
    }

    private IEnumerable<string> ReadCurrentDocument()
    {
        try
        {
            PathAccessState state = PathScan.InspectUnixPath(_currentDocuments[_currIndex].Path, false, true, false, true);
            if (state != PathAccessState.Success)
            {
                _currentDocuments[_currIndex].PathAccess = state;
                return Enumerable.Empty<string>();
            }
            return File.ReadLines(_currentDocuments[_currIndex].Path)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Take(UIConstants.FILE_EXPLORER_NUM_ROWS);
        }
        catch (Exception)
        {
            return Enumerable.Empty<string>();
        }
    }

    /* PUBLIC FUNCTIONS */

    public FileExplorerService()
    {
        _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        LoadContents(_currentDocuments, new Document(_currentPath, true, false, PathAccessState.Success));
        _currIndex = 0;
        Update_nextDocuments();
        _state = ExplorerState.MultipleSelection;
    }

    public int CurrIndex => _currIndex;
    public IReadOnlyList<Document> GetCurrDocuments => _currentDocuments;
    public IReadOnlyList<Document> GetNextDocuments => _nextDocuments;
    public IReadOnlyList<string> GetText => _fileContents;
    public bool HasSelectedDocuments => (_selectedFiles.Count + _selectedFolders.Count) > 0;
    public IReadOnlyList<string> GetSelectedFolders => _selectedFolders.ToList();
    public IReadOnlyList<string> GetSelectedFiles => _selectedFiles.ToList();

    public bool CheckFileState(int index)
    {
        if (index < 0 || index > _currentDocuments.Count) return false;
        return (_currentDocuments[index].PathAccess == PathAccessState.Success);
    }

    public void ClearAndSetState(bool isMultipleSelection)
    {
        _selectedFolders.Clear();
        _selectedFiles.Clear();
        _currentDocuments.Clear();
        _nextDocuments.Clear();
        _fileContents.Clear();
        _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        LoadContents(_currentDocuments, new Document(_currentPath, true, false, PathAccessState.Success));
        _currIndex = 0;
        Update_nextDocuments();
        _state = isMultipleSelection ? ExplorerState.MultipleSelection : ExplorerState.SingleSelection;
    }

    public void GoToPath(string path) // it shoudl work for root still
    {
        if (File.Exists(path))
        {
            path = Path.GetDirectoryName(path)!; // parent folder
        }
        if (!Directory.Exists(path)) return;
        bool toWrite = (Directory.GetParent(_currentPath) != null) && _state == ExplorerState.SingleSelection; // for root this will be an exception
        PathAccessState pathState = PathScan.InspectUnixPath(path, true, true, toWrite, false);
        if (pathState == PathAccessState.Success)
        {
            _currentPath = path;
            LoadContents(_currentDocuments, new Document(path, true, true, pathState));
            _currIndex = 0;
            Update_nextDocuments();
        }
        else // path exists but is not accessible, backtrack to parent
        {
            _currentPath = path;
            _currentDocuments.Clear();
            GoToParent();
        }
    }

    public void GoToParent()
    {
        var parentPath = Directory.GetParent(_currentPath);
        if (parentPath == null) return; // null is the root directory for Unix/macOS

        var folderName = Path.GetFileName(_currentPath);
        _currentPath = parentPath.FullName; // absolute path
        PathAccessState pathState = PathScan.InspectUnixPath(_currentPath, true, true, _state == ExplorerState.SingleSelection, false);
        if (pathState != PathAccessState.Success) return;

        _nextDocuments = new List<Document>(_currentDocuments); // copy 
        LoadContents(_currentDocuments, new Document(_currentPath, true, false, pathState));
        int curr_i = 0;
        foreach (var doc in _currentDocuments)
        {
            if (Path.GetFileName(doc.Path) == folderName)
            {
                _currIndex = curr_i;
                break;
            }
            ++curr_i;
        }
    }

    public void GoToChild()
    {
        if (!_nextDocuments.Any() ||
            _currentDocuments[_currIndex].IsSelected ||
            !_currentDocuments[_currIndex].IsFolder ||
            (
                _currentDocuments[_currIndex].IsFolder &&
                !(_currentDocuments[_currIndex].PathAccess == PathAccessState.Success)
            ))
        {
            return;
        }

        _currentPath = _currentDocuments[_currIndex].Path;
        _currentDocuments = new List<Document>(_nextDocuments);
        _currIndex = 0;
        Update_nextDocuments();
    }

    public void GoDown()
    {
        _currIndex = (ushort)(_currIndex < _currentDocuments.Count - 1 ? _currIndex + 1 : 0);
        Update_nextDocuments();
    }

    public void SetIndex(int index)
    {
        if (index < 0 || index >= _currentDocuments.Count) return;
        _currIndex = index;
        Update_nextDocuments();
    }

    public void GoUp()
    {
        _currIndex = _currIndex > 0 ? (ushort)(_currIndex - 1) : (ushort)(_currentDocuments.Count - 1);
        Update_nextDocuments();
    }

    public void SelectDocument()
    {
        if (!CheckFileState(_currIndex)) return;

        var doc = _currentDocuments[_currIndex];

        if (_selectedFolders.Contains(doc.Path) ||
            _state == ExplorerState.MultipleSelection && _selectedFiles.Contains(doc.Path))
        {
            doc.IsSelected = false;
            if (doc.IsFolder)
            {
                _selectedFolders.Remove(doc.Path);
            }
            else
            {
                _selectedFiles.Remove(doc.Path);
            }
        }
        else
        {
            if (_state == ExplorerState.SingleSelection && doc.IsFolder)
            {
                foreach (var folder in _selectedFolders.ToList())
                {
                    var match = _currentDocuments.FirstOrDefault(d => d.Path == folder);
                    if (match != null)
                    {
                        match.IsSelected = false;
                    }
                    _selectedFolders.Remove(folder);
                }
                doc.IsSelected = true;
                _selectedFolders.Add(doc.Path);
                return;
            }
            if (doc.IsFolder)
            {
                _selectedFolders.Add(doc.Path);
            }
            else
            {
                _selectedFiles.Add(doc.Path);
            }
            doc.IsSelected = true;
        }
    }
}
