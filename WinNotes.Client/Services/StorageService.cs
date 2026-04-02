using System.IO;
using System.Text;
using System.Text.Json;
using WinNotes.Client.Models;

namespace WinNotes.Client.Services;

public sealed class StorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string DataFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinNotes",
        "winnotes-data.json");

    public async Task<AppState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(DataFilePath))
        {
            return CreateDefaultState();
        }

        try
        {
            var json = await File.ReadAllTextAsync(DataFilePath, cancellationToken);
            var state = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
            return NormalizeState(state);
        }
        catch
        {
            return CreateDefaultState();
        }
    }

    public async Task SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeState(state);
        Directory.CreateDirectory(Path.GetDirectoryName(DataFilePath)!);

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        await File.WriteAllTextAsync(DataFilePath, json, Encoding.UTF8, cancellationToken);
    }

    private static AppState NormalizeState(AppState? source)
    {
        var defaults = CreateDefaultState();

        var folders = (source?.Folders ?? new List<NoteFolder>())
            .Where(folder => folder is not null && !string.IsNullOrWhiteSpace(folder.Id))
            .Select(folder => new NoteFolder
            {
                Id = folder.Id,
                Name = string.IsNullOrWhiteSpace(folder.Name) ? "未命名文件夹" : folder.Name.Trim(),
                CreatedAt = NormalizeDate(folder.CreatedAt),
                IsDefault = folder.IsDefault
            })
            .ToList();

        if (folders.Count == 0)
        {
            folders = defaults.Folders;
        }

        var folderIds = folders.Select(folder => folder.Id).ToHashSet(StringComparer.Ordinal);
        var fallbackFolderId = folders[0].Id;

        var notesSource = source?.Notes;
        var notes = (notesSource ?? new List<NoteItem>())
            .Where(note => note is not null && !string.IsNullOrWhiteSpace(note.Id))
            .Select(note =>
            {
                var createdAt = NormalizeDate(note.CreatedAt);
                var updatedAt = NormalizeDate(note.UpdatedAt == default ? createdAt : note.UpdatedAt);
                var plainText = note.PlainText ?? string.Empty;
                var contentXaml = string.IsNullOrWhiteSpace(note.ContentXaml)
                    ? NoteDocumentService.CreateDocumentPayloadFromPlainText(plainText)
                    : note.ContentXaml;

                return new NoteItem
                {
                    Id = note.Id,
                    FolderId = folderIds.Contains(note.FolderId) ? note.FolderId : fallbackFolderId,
                    Title = note.Title ?? string.Empty,
                    ContentXaml = contentXaml,
                    PlainText = plainText,
                    IsPinned = note.IsPinned,
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt
                };
            })
            .ToList();

        if (notesSource is null)
        {
            notes = defaults.Notes;
        }

        var selectedSidebarId = source?.Preferences?.SelectedSidebarId;
        if (selectedSidebarId != "all-notes" &&
            selectedSidebarId != "pinned-notes" &&
            !folderIds.Contains(selectedSidebarId ?? string.Empty))
        {
            selectedSidebarId = "all-notes";
        }

        var selectedNoteId = source?.Preferences?.SelectedNoteId;
        if (!notes.Any(note => note.Id == selectedNoteId))
        {
            selectedNoteId = notes.FirstOrDefault()?.Id;
        }

        return new AppState
        {
            Version = 1,
            Preferences = new AppPreferences
            {
                SelectedSidebarId = selectedSidebarId ?? "all-notes",
                SelectedNoteId = selectedNoteId
            },
            Folders = folders,
            Notes = notes
        };
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        if (value == default)
        {
            return DateTime.UtcNow;
        }

        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private static AppState CreateDefaultState()
    {
        var notesFolderId = CreateId("folder");
        var workFolderId = CreateId("folder");
        var ideasFolderId = CreateId("folder");

        var welcomeNoteId = CreateId("note");
        var reviewNoteId = CreateId("note");
        var journalNoteId = CreateId("note");

        var now = DateTime.UtcNow;

        return new AppState
        {
            Preferences = new AppPreferences
            {
                SelectedSidebarId = "all-notes",
                SelectedNoteId = welcomeNoteId
            },
            Folders = new List<NoteFolder>
            {
                new() { Id = notesFolderId, Name = "备忘录", CreatedAt = now, IsDefault = true },
                new() { Id = workFolderId, Name = "工作", CreatedAt = now, IsDefault = true },
                new() { Id = ideasFolderId, Name = "灵感", CreatedAt = now, IsDefault = true }
            },
            Notes = new List<NoteItem>
            {
                new()
                {
                    Id = welcomeNoteId,
                    FolderId = notesFolderId,
                    Title = "欢迎使用 WinNotes C#",
                    ContentXaml = NoteDocumentService.CreateDocumentPayloadFromPlainText("这是一个使用 C# WPF 实现的桌面备忘录客户端。\n\n你可以创建文件夹、撰写富文本内容、置顶重点笔记，并自动保存到本地。"),
                    PlainText = "这是一个使用 C# WPF 实现的桌面备忘录客户端。你可以创建文件夹、撰写富文本内容、置顶重点笔记，并自动保存到本地。",
                    IsPinned = true,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    Id = reviewNoteId,
                    FolderId = workFolderId,
                    Title = "产品评审清单",
                    ContentXaml = NoteDocumentService.CreateDocumentPayloadFromPlainText("本周重点：\n- 整理发布节奏\n- 确认首页转化数据\n- 讨论视觉稿交付时间"),
                    PlainText = "本周重点：整理发布节奏，确认首页转化数据，讨论视觉稿交付时间。",
                    IsPinned = false,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new()
                {
                    Id = journalNoteId,
                    FolderId = ideasFolderId,
                    Title = "周末随记",
                    ContentXaml = NoteDocumentService.CreateDocumentPayloadFromPlainText("先把想法写下来，再决定哪些值得继续做。\n\n把东西做出来，再慢慢打磨细节。"),
                    PlainText = "先把想法写下来，再决定哪些值得继续做。把东西做出来，再慢慢打磨细节。",
                    IsPinned = false,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            }
        };
    }

    private static string CreateId(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }
}
