using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Documents;
using WinNotes.Client.Models;
using WinNotes.Client.Services;

namespace WinNotes.Client.ViewModels;

public sealed class MainViewModel : BindableBase
{
    public const string AllNotesSidebarId = "all-notes";
    public const string PinnedSidebarId = "pinned-notes";

    private readonly StorageService _storageService = new();
    private readonly ObservableCollection<NoteItem> _allNotes = new();
    private CancellationTokenSource? _saveCts;
    private string _selectedSidebarId = AllNotesSidebarId;
    private string _searchQuery = string.Empty;
    private NoteItem? _selectedNote;
    private string _listTitle = "全部备忘录";
    private string _listSubtitle = "按最近编辑时间排序，置顶优先";
    private string _saveStatusMessage = string.Empty;
    private bool _isInitializing;

    public MainViewModel()
    {
        _allNotes.CollectionChanged += AllNotes_CollectionChanged;
    }

    public ObservableCollection<SidebarItem> SmartSidebarItems { get; } = new();

    public ObservableCollection<SidebarItem> FolderSidebarItems { get; } = new();

    public ObservableCollection<NoteFolder> Folders { get; } = new();

    public ObservableCollection<NoteItem> VisibleNotes { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshVisibleNotes();
            }
        }
    }

    public NoteItem? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (SetProperty(ref _selectedNote, value))
            {
                OnPropertyChanged(nameof(HasSelectedNote));
                QueueSave();
            }
        }
    }

    public bool HasSelectedNote => SelectedNote is not null;

    public string ListTitle
    {
        get => _listTitle;
        private set => SetProperty(ref _listTitle, value);
    }

    public string ListSubtitle
    {
        get => _listSubtitle;
        private set => SetProperty(ref _listSubtitle, value);
    }

    public int VisibleNoteCount => VisibleNotes.Count;

    public string SaveStatusMessage
    {
        get => _saveStatusMessage;
        private set => SetProperty(ref _saveStatusMessage, value);
    }

    public string DataFilePath => _storageService.DataFilePath;

    public async Task InitializeAsync()
    {
        _isInitializing = true;
        var state = await _storageService.LoadAsync();

        foreach (var folder in Folders.ToList())
        {
            folder.PropertyChanged -= Folder_PropertyChanged;
        }

        Folders.Clear();
        foreach (var note in _allNotes.ToList())
        {
            DetachNote(note);
        }

        _allNotes.Clear();

        foreach (var folder in state.Folders)
        {
            AttachFolder(folder);
            Folders.Add(folder);
        }

        foreach (var note in state.Notes)
        {
            AttachNote(note);
            _allNotes.Add(note);
        }

        _selectedSidebarId = state.Preferences.SelectedSidebarId;
        UpdateFolderNames();
        RefreshSidebarItems();
        RefreshVisibleNotes(state.Preferences.SelectedNoteId);

        _isInitializing = false;
        SaveStatusMessage = $"自动保存到 {DataFilePath}";
    }

    public async Task FlushSaveAsync()
    {
        _saveCts?.Cancel();
        _saveCts = null;

        try
        {
            await _storageService.SaveAsync(BuildState());
            SaveStatusMessage = $"已保存 {DateTime.Now:HH:mm} · {DataFilePath}";
        }
        catch
        {
            SaveStatusMessage = $"保存失败 · {DataFilePath}";
        }
    }

    public void SelectSidebar(string sidebarId)
    {
        if (string.IsNullOrWhiteSpace(sidebarId) || _selectedSidebarId == sidebarId)
        {
            return;
        }

        _selectedSidebarId = sidebarId;
        RefreshSidebarItems();
        RefreshVisibleNotes();
        QueueSave();
    }

    public string CreateFolder(string name)
    {
        var folder = new NoteFolder
        {
            Id = CreateId("folder"),
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsDefault = false
        };

        AttachFolder(folder);
        Folders.Add(folder);
        UpdateFolderNames();
        RefreshSidebarItems();
        SelectSidebar(folder.Id);
        QueueSave();
        return folder.Id;
    }

    public void CreateNote()
    {
        var targetFolderId = ResolveFolderForNewNote();
        var now = DateTime.UtcNow;
        var note = new NoteItem
        {
            Id = CreateId("note"),
            FolderId = targetFolderId,
            Title = string.Empty,
            ContentXaml = NoteDocumentService.CreateDocumentPayloadFromPlainText(string.Empty),
            PlainText = string.Empty,
            IsPinned = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        note.FolderName = GetFolderById(targetFolderId)?.Name ?? "未分类";

        AttachNote(note);
        _allNotes.Add(note);
        RefreshSidebarItems();
        RefreshVisibleNotes(note.Id);
        QueueSave();
    }

    public bool CanDeleteFolder(string folderId)
    {
        var folder = GetFolderById(folderId);
        return folder is not null && !folder.IsDefault && Folders.Count > 1;
    }

    public NoteFolder? GetFolderById(string folderId)
    {
        return Folders.FirstOrDefault(folder => folder.Id == folderId);
    }

    public NoteFolder? GetFallbackFolder(string excludedFolderId)
    {
        return Folders.FirstOrDefault(folder => folder.Id != excludedFolderId);
    }

    public void DeleteFolder(string folderId)
    {
        var folder = GetFolderById(folderId);
        var fallbackFolder = GetFallbackFolder(folderId);

        if (folder is null || fallbackFolder is null)
        {
            return;
        }

        foreach (var note in _allNotes.Where(note => note.FolderId == folderId))
        {
            note.FolderId = fallbackFolder.Id;
            note.FolderName = fallbackFolder.Name;
            note.UpdatedAt = DateTime.UtcNow;
        }

        folder.PropertyChanged -= Folder_PropertyChanged;
        Folders.Remove(folder);

        if (_selectedSidebarId == folderId)
        {
            _selectedSidebarId = fallbackFolder.Id;
        }

        UpdateFolderNames();
        RefreshSidebarItems();
        RefreshVisibleNotes();
        QueueSave();
    }

    public void ToggleSelectedNotePin()
    {
        if (SelectedNote is null)
        {
            return;
        }

        SelectedNote.IsPinned = !SelectedNote.IsPinned;
        SelectedNote.UpdatedAt = DateTime.UtcNow;
    }

    public void DeleteSelectedNote()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var note = SelectedNote;
        DetachNote(note);
        _allNotes.Remove(note);
        RefreshSidebarItems();
        RefreshVisibleNotes();
        QueueSave();
    }

    public void UpdateSelectedNoteDocument(FlowDocument document)
    {
        if (SelectedNote is null)
        {
            return;
        }

        var payload = NoteDocumentService.Serialize(document);
        var plainText = NoteDocumentService.ExtractPlainText(document);
        var updated = DateTime.UtcNow;

        if (SelectedNote.UpdateContent(payload, plainText, updated))
        {
            RefreshVisibleNotes(SelectedNote.Id);
            QueueSave();
        }
    }

    private AppState BuildState()
    {
        return new AppState
        {
            Version = 1,
            Preferences = new AppPreferences
            {
                SelectedSidebarId = _selectedSidebarId,
                SelectedNoteId = SelectedNote?.Id
            },
            Folders = Folders.Select(folder => new NoteFolder
            {
                Id = folder.Id,
                Name = folder.Name,
                CreatedAt = folder.CreatedAt,
                IsDefault = folder.IsDefault
            }).ToList(),
            Notes = _allNotes.Select(note => new NoteItem
            {
                Id = note.Id,
                FolderId = note.FolderId,
                Title = note.Title,
                ContentXaml = note.ContentXaml,
                PlainText = note.PlainText,
                IsPinned = note.IsPinned,
                CreatedAt = note.CreatedAt,
                UpdatedAt = note.UpdatedAt
            }).ToList()
        };
    }

    private void RefreshVisibleNotes(string? preferredNoteId = null)
    {
        var currentSelectedId = preferredNoteId ?? SelectedNote?.Id;
        var query = SearchQuery.Trim();

        IEnumerable<NoteItem> filtered = _allNotes;

        if (_selectedSidebarId == PinnedSidebarId)
        {
            filtered = filtered.Where(note => note.IsPinned);
        }
        else if (_selectedSidebarId != AllNotesSidebarId)
        {
            filtered = filtered.Where(note => note.FolderId == _selectedSidebarId);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(note =>
                note.DisplayTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                note.PlainText.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        var ordered = filtered
            .OrderByDescending(note => note.IsPinned)
            .ThenByDescending(note => note.UpdatedAt)
            .ToList();

        VisibleNotes.Clear();
        foreach (var note in ordered)
        {
            VisibleNotes.Add(note);
        }

        SelectedNote = ordered.FirstOrDefault(note => note.Id == currentSelectedId) ?? ordered.FirstOrDefault();

        UpdateListHeader(query);
        OnPropertyChanged(nameof(VisibleNoteCount));
    }

    private void RefreshSidebarItems()
    {
        var allCount = _allNotes.Count;
        var pinnedCount = _allNotes.Count(note => note.IsPinned);

        SmartSidebarItems.Clear();
        SmartSidebarItems.Add(new SidebarItem
        {
            Id = AllNotesSidebarId,
            Name = "全部备忘录",
            Hint = "所有文件夹",
            Count = allCount,
            CanDelete = false,
            IsSelected = _selectedSidebarId == AllNotesSidebarId
        });
        SmartSidebarItems.Add(new SidebarItem
        {
            Id = PinnedSidebarId,
            Name = "已置顶",
            Hint = "重要内容",
            Count = pinnedCount,
            CanDelete = false,
            IsSelected = _selectedSidebarId == PinnedSidebarId
        });

        FolderSidebarItems.Clear();
        foreach (var folder in Folders.OrderBy(folder => folder.IsDefault ? 0 : 1).ThenBy(folder => folder.CreatedAt))
        {
            FolderSidebarItems.Add(new SidebarItem
            {
                Id = folder.Id,
                Name = folder.Name,
                Hint = folder.IsDefault ? "默认文件夹" : "自定义文件夹",
                Count = _allNotes.Count(note => note.FolderId == folder.Id),
                CanDelete = !folder.IsDefault,
                IsSelected = _selectedSidebarId == folder.Id
            });
        }
    }

    private void UpdateListHeader(string query)
    {
        ListTitle = _selectedSidebarId switch
        {
            PinnedSidebarId => "已置顶",
            AllNotesSidebarId => "全部备忘录",
            _ => GetFolderById(_selectedSidebarId)?.Name ?? "全部备忘录"
        };

        ListSubtitle = string.IsNullOrWhiteSpace(query)
            ? "按最近编辑时间排序，置顶优先"
            : $"匹配“{query}”的结果";
    }

    private void UpdateFolderNames()
    {
        foreach (var note in _allNotes)
        {
            note.FolderName = GetFolderById(note.FolderId)?.Name ?? "未分类";
        }
    }

    private string ResolveFolderForNewNote()
    {
        if (Folders.Any(folder => folder.Id == _selectedSidebarId))
        {
            return _selectedSidebarId;
        }

        if (SelectedNote is not null && Folders.Any(folder => folder.Id == SelectedNote.FolderId))
        {
            return SelectedNote.FolderId;
        }

        return Folders.First().Id;
    }

    private void QueueSave()
    {
        if (_isInitializing)
        {
            return;
        }

        SaveStatusMessage = $"正在自动保存 · {DataFilePath}";
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        var saveTokenSource = _saveCts;

        _ = SaveDeferredAsync(saveTokenSource);
    }

    private void AttachFolder(NoteFolder folder)
    {
        folder.PropertyChanged += Folder_PropertyChanged;
    }

    private void AttachNote(NoteItem note)
    {
        note.PropertyChanged += Note_PropertyChanged;
    }

    private void DetachNote(NoteItem note)
    {
        note.PropertyChanged -= Note_PropertyChanged;
    }

    private void Folder_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        if (e.PropertyName == nameof(NoteFolder.Name))
        {
            UpdateFolderNames();
            RefreshSidebarItems();
            RefreshVisibleNotes(SelectedNote?.Id);
            QueueSave();
        }
    }

    private void Note_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isInitializing || sender is not NoteItem note)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(NoteItem.Title):
            case nameof(NoteItem.PlainText):
            case nameof(NoteItem.IsPinned):
            case nameof(NoteItem.UpdatedAt):
                RefreshSidebarItems();
                RefreshVisibleNotes(note.Id);
                QueueSave();
                break;
            case nameof(NoteItem.FolderId):
                note.FolderName = GetFolderById(note.FolderId)?.Name ?? "未分类";
                RefreshSidebarItems();
                RefreshVisibleNotes(note.Id);
                QueueSave();
                break;
            case nameof(NoteItem.ContentXaml):
                QueueSave();
                break;
        }
    }

    private void AllNotes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(VisibleNoteCount));
    }

    private static string CreateId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private async Task SaveDeferredAsync(CancellationTokenSource saveTokenSource)
    {
        try
        {
            await Task.Delay(320, saveTokenSource.Token);
            await _storageService.SaveAsync(BuildState(), saveTokenSource.Token);

            if (!saveTokenSource.IsCancellationRequested && ReferenceEquals(_saveCts, saveTokenSource))
            {
                SaveStatusMessage = $"已保存 {DateTime.Now:HH:mm} · {DataFilePath}";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            SaveStatusMessage = $"保存失败 · {DataFilePath}";
        }
    }
}
