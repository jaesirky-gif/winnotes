using System.Globalization;
using System.Text.Json.Serialization;
using WinNotes.Client.ViewModels;

namespace WinNotes.Client.Models;

public sealed class NoteItem : BindableBase
{
    private static readonly CultureInfo ZhCn = CultureInfo.GetCultureInfo("zh-CN");

    private string _id = string.Empty;
    private string _folderId = string.Empty;
    private string _title = string.Empty;
    private string _contentXaml = string.Empty;
    private string _plainText = string.Empty;
    private bool _isPinned;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;
    private string _folderName = string.Empty;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string FolderId
    {
        get => _folderId;
        set => SetProperty(ref _folderId, value);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public string ContentXaml
    {
        get => _contentXaml;
        set => SetProperty(ref _contentXaml, value);
    }

    public string PlainText
    {
        get => _plainText;
        set
        {
            if (SetProperty(ref _plainText, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(Preview));
            }
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set
        {
            if (SetProperty(ref _updatedAt, value))
            {
                OnPropertyChanged(nameof(UpdatedShortText));
                OnPropertyChanged(nameof(UpdatedLongText));
            }
        }
    }

    [JsonIgnore]
    public string FolderName
    {
        get => _folderName;
        set => SetProperty(ref _folderName, value);
    }

    [JsonIgnore]
    public string DisplayTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title))
            {
                return Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(PlainText))
            {
                return CollapseWhitespace(PlainText).Truncate(26);
            }

            return "无标题";
        }
    }

    [JsonIgnore]
    public string Preview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PlainText))
            {
                return "开始记录你的内容...";
            }

            return CollapseWhitespace(PlainText).Truncate(110);
        }
    }

    [JsonIgnore]
    public string UpdatedShortText => UpdatedAt.ToLocalTime().ToString("M月d日", ZhCn);

    [JsonIgnore]
    public string UpdatedLongText => UpdatedAt.ToLocalTime().ToString("M月d日 HH:mm", ZhCn);

    public bool UpdateContent(string contentXaml, string plainText, DateTime updatedAt)
    {
        var changed = false;

        if (_contentXaml != contentXaml)
        {
            _contentXaml = contentXaml;
            OnPropertyChanged(nameof(ContentXaml));
            changed = true;
        }

        if (_plainText != plainText)
        {
            _plainText = plainText;
            OnPropertyChanged(nameof(PlainText));
            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(Preview));
            changed = true;
        }

        if (_updatedAt != updatedAt)
        {
            _updatedAt = updatedAt;
            OnPropertyChanged(nameof(UpdatedAt));
            OnPropertyChanged(nameof(UpdatedShortText));
            OnPropertyChanged(nameof(UpdatedLongText));
            changed = true;
        }

        return changed;
    }

    private static string CollapseWhitespace(string value)
    {
        return string.Join(" ", value.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
