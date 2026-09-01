using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GalaXako.Editor.App.ViewModels;

namespace GalaXako.Editor.App.Views;

public partial class EditorView : UserControl
{
    private bool _syncingText;
    private EditorViewModel? _viewModel;

    public EditorView() => InitializeComponent();

    private void EditorView_Loaded(object sender, RoutedEventArgs e) => AttachViewModel(DataContext as EditorViewModel);

    private void EditorView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => AttachViewModel(e.NewValue as EditorViewModel);

    private void AttachViewModel(EditorViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;
        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = viewModel;
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        SyncEditors();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorViewModel.Text) or nameof(EditorViewModel.PreviewText)) SyncEditors();
    }

    private void SyncEditors()
    {
        if (_viewModel is null) return;
        _syncingText = true;
        try
        {
            if (NormalEditor.Text != _viewModel.Text) NormalEditor.Text = _viewModel.Text;
            if (LargePreviewEditor.Text != _viewModel.PreviewText) LargePreviewEditor.Text = _viewModel.PreviewText;
        }
        finally { _syncingText = false; }
    }

    private void NormalEditor_TextChanged(object? sender, EventArgs e)
    {
        if (!_syncingText && _viewModel is not null) _viewModel.Text = NormalEditor.Text;
        UpdateCaretStatus();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) { if (NormalEditor.CanUndo) NormalEditor.Undo(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { if (NormalEditor.CanRedo) NormalEditor.Redo(); }
    private void WordWrap_Changed(object sender, RoutedEventArgs e) => NormalEditor.WordWrap = WordWrapCheckBox.IsChecked == true;

    private void Whitespace_Changed(object sender, RoutedEventArgs e)
    {
        var show = WhitespaceCheckBox.IsChecked == true;
        NormalEditor.Options.ShowSpaces = show;
        NormalEditor.Options.ShowTabs = show;
        NormalEditor.Options.ShowEndOfLine = show;
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => Find(forward: true);
    private void FindPrevious_Click(object sender, RoutedEventArgs e) => Find(forward: false);

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (NormalEditor.SelectionLength > 0)
            NormalEditor.Document.Replace(NormalEditor.SelectionStart, NormalEditor.SelectionLength, ReplaceBox.Text);
        Find(forward: true);
    }

    private void Find(bool forward)
    {
        if (string.IsNullOrEmpty(FindBox.Text)) return;
        try
        {
            var pattern = RegexCheckBox.IsChecked == true ? FindBox.Text : Regex.Escape(FindBox.Text);
            if (WholeWordCheckBox.IsChecked == true) pattern = $@"\b(?:{pattern})\b";
            var options = RegexOptions.CultureInvariant;
            if (CaseSensitiveCheckBox.IsChecked != true) options |= RegexOptions.IgnoreCase;
            var regex = new Regex(pattern, options, TimeSpan.FromSeconds(2));
            Match? match;
            if (forward)
            {
                var start = NormalEditor.SelectionStart + NormalEditor.SelectionLength;
                match = regex.Match(NormalEditor.Text, Math.Min(start, NormalEditor.Text.Length));
                if (!match.Success) match = regex.Match(NormalEditor.Text, 0);
            }
            else
            {
                var matches = regex.Matches(NormalEditor.Text[..Math.Min(NormalEditor.SelectionStart, NormalEditor.Text.Length)]);
                match = matches.Count > 0 ? matches[^1] : regex.Matches(NormalEditor.Text).Cast<Match>().LastOrDefault();
            }

            if (match is { Success: true })
            {
                NormalEditor.Select(match.Index, match.Length);
                NormalEditor.ScrollToLine(NormalEditor.Document.GetLineByOffset(match.Index).LineNumber);
            }
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(Window.GetWindow(this), exception.Message, "Geçersiz regex", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (RegexMatchTimeoutException)
        {
            MessageBox.Show(Window.GetWindow(this), "Arama deseni zaman aşımına uğradı.", "Regex zaman aşımı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void NormalEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F) { FindBox.Focus(); FindBox.SelectAll(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.H) { ReplaceBox.Focus(); ReplaceBox.SelectAll(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.G)
        {
            var line = Microsoft.VisualBasic.Interaction.InputBox("Satır numarası:", "Satıra git", NormalEditor.TextArea.Caret.Line.ToString());
            if (int.TryParse(line, out var number) && number >= 1 && number <= NormalEditor.Document.LineCount)
            {
                NormalEditor.ScrollToLine(number); NormalEditor.TextArea.Caret.Line = number; NormalEditor.Focus();
            }
            e.Handled = true;
        }
        UpdateCaretStatus();
    }

    private void UpdateCaretStatus() => CaretStatus.Text = $" · Satır {NormalEditor.TextArea.Caret.Line:N0}, Sütun {NormalEditor.TextArea.Caret.Column:N0} · Seçim {NormalEditor.SelectionLength:N0}";
}
