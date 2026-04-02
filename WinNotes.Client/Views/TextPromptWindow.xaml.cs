using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace WinNotes.Client.Views;

public partial class TextPromptWindow : Window, INotifyPropertyChanged
{
    private string _promptMessage = string.Empty;
    private string _inputText = string.Empty;

    public TextPromptWindow(string title, string promptMessage, string initialText = "")
    {
        InitializeComponent();
        DataContext = this;
        Title = title;
        PromptMessage = promptMessage;
        InputText = initialText;
        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PromptMessage
    {
        get => _promptMessage;
        set
        {
            _promptMessage = value;
            OnPropertyChanged();
        }
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            _inputText = value;
            OnPropertyChanged();
        }
    }

    public static string? ShowPrompt(Window owner, string title, string promptMessage, string initialText = "")
    {
        var window = new TextPromptWindow(title, promptMessage, initialText)
        {
            Owner = owner
        };

        var result = window.ShowDialog();
        return result == true ? window.InputText.Trim() : null;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            DialogResult = true;
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
