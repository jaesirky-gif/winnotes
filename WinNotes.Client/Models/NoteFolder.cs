using WinNotes.Client.ViewModels;

namespace WinNotes.Client.Models;

public sealed class NoteFolder : BindableBase
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _isDefault;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }
}
