namespace WinNotes.Client.Models;

public sealed class AppState
{
    public int Version { get; set; } = 1;

    public AppPreferences Preferences { get; set; } = new();

    public List<NoteFolder> Folders { get; set; } = new();

    public List<NoteItem> Notes { get; set; } = new();
}

public sealed class AppPreferences
{
    public string SelectedSidebarId { get; set; } = "all-notes";

    public string? SelectedNoteId { get; set; }
}
