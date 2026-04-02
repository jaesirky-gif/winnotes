using WinNotes.Client.ViewModels;

namespace WinNotes.Client.Models;

public sealed class SidebarItem : BindableBase
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _hint = string.Empty;
    private int _count;
    private bool _canDelete;
    private bool _isSelected;

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

    public string Hint
    {
        get => _hint;
        set => SetProperty(ref _hint, value);
    }

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public bool CanDelete
    {
        get => _canDelete;
        set => SetProperty(ref _canDelete, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
