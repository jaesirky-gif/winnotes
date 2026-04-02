using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinNotes.Client.Services;
using WinNotes.Client.ViewModels;
using WinNotes.Client.Views;

namespace WinNotes.Client;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private bool _isSyncingEditor;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        LoadSelectedNoteIntoEditor();
        UpdatePinButtonLabel();
    }

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        await _viewModel.FlushSaveAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedNote))
        {
            LoadSelectedNoteIntoEditor();
            UpdatePinButtonLabel();
        }
    }

    private void SidebarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sidebarId })
        {
            _viewModel.SelectSidebar(sidebarId);
        }
    }

    private void NewFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var name = TextPromptWindow.ShowPrompt(this, "新建文件夹", "输入文件夹名称");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _viewModel.CreateFolder(name);
    }

    private void DeleteFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string folderId })
        {
            return;
        }

        var folder = _viewModel.GetFolderById(folderId);
        var fallbackFolder = _viewModel.GetFallbackFolder(folderId);

        if (folder is null || fallbackFolder is null || !_viewModel.CanDeleteFolder(folderId))
        {
            MessageBox.Show(this, "至少需要保留一个文件夹。", "无法删除", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            this,
            $"删除“{folder.Name}”后，里面的备忘录会移动到“{fallbackFolder.Name}”。是否继续？",
            "删除文件夹",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteFolder(folderId);
        }
    }

    private void NewNoteButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CreateNote();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            TitleTextBox.Focus();
            TitleTextBox.SelectAll();
        }), DispatcherPriority.Input);
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleSelectedNotePin();
        UpdatePinButtonLabel();
    }

    private void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNote is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"删除“{_viewModel.SelectedNote.DisplayTitle}”？",
            "删除备忘录",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteSelectedNote();
        }
    }

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingEditor)
        {
            return;
        }

        _viewModel.UpdateSelectedNoteDocument(EditorBox.Document);
        UpdatePinButtonLabel();
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteEditorCommand(() => EditingCommands.ToggleBold.Execute(null, EditorBox));
    }

    private void ItalicButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteEditorCommand(() => EditingCommands.ToggleItalic.Execute(null, EditorBox));
    }

    private void UnderlineButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteEditorCommand(() => EditingCommands.ToggleUnderline.Execute(null, EditorBox));
    }

    private void BulletsButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteEditorCommand(() => EditingCommands.ToggleBullets.Execute(null, EditorBox));
    }

    private void NumberingButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteEditorCommand(() => EditingCommands.ToggleNumbering.Execute(null, EditorBox));
    }

    private void ClearFormatButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteEditorCommand(() =>
        {
            foreach (var paragraph in GetSelectedParagraphs())
            {
                paragraph.Margin = new Thickness(0, 8, 0, 8);
                paragraph.TextIndent = 0;
                var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
                range.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
                range.ApplyPropertyValue(TextElement.FontSizeProperty, 18.0);
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                range.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F261B")));
            }
        });
    }

    private void HeadingButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteEditorCommand(() =>
        {
            foreach (var paragraph in GetSelectedParagraphs())
            {
                paragraph.Margin = new Thickness(0, 16, 0, 8);
                paragraph.TextIndent = 0;
                var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
                range.ApplyPropertyValue(TextElement.FontSizeProperty, 28.0);
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.SemiBold);
                range.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F261B")));
            }
        });
    }

    private void QuoteButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteEditorCommand(() =>
        {
            foreach (var paragraph in GetSelectedParagraphs())
            {
                paragraph.Margin = new Thickness(18, 10, 0, 10);
                paragraph.TextIndent = 0;
                var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
                range.ApplyPropertyValue(TextElement.FontSizeProperty, 18.0);
                range.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
                range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71573B")));
            }
        });
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var isControlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!isControlPressed)
        {
            return;
        }

        if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            NewFolderButton_Click(sender, e);
            return;
        }

        if (e.Key == Key.N)
        {
            e.Handled = true;
            NewNoteButton_Click(sender, e);
            return;
        }

        if (e.Key == Key.F)
        {
            e.Handled = true;
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }

    private void LoadSelectedNoteIntoEditor()
    {
        _isSyncingEditor = true;
        EditorBox.Document = NoteDocumentService.Deserialize(_viewModel.SelectedNote?.ContentXaml);
        NoteDocumentService.ApplyDocumentDefaults(EditorBox.Document);
        _isSyncingEditor = false;
    }

    private void ExecuteEditorCommand(Action command)
    {
        if (_viewModel.SelectedNote is null)
        {
            return;
        }

        EditorBox.Focus();
        command();
        _viewModel.UpdateSelectedNoteDocument(EditorBox.Document);
    }

    private IEnumerable<Paragraph> GetSelectedParagraphs()
    {
        var selection = EditorBox.Selection;
        var startParagraph = selection.Start.Paragraph ?? EditorBox.Document.Blocks.FirstBlock as Paragraph;
        var endParagraph = selection.End.Paragraph ?? startParagraph;

        if (startParagraph is null || endParagraph is null)
        {
            yield break;
        }

        for (Block? block = startParagraph; block is not null; block = block.NextBlock)
        {
            if (block is Paragraph paragraph)
            {
                yield return paragraph;
            }

            if (ReferenceEquals(block, endParagraph))
            {
                yield break;
            }
        }
    }

    private void UpdatePinButtonLabel()
    {
        PinButton.Content = _viewModel.SelectedNote?.IsPinned == true ? "取消置顶" : "置顶";
    }
}
