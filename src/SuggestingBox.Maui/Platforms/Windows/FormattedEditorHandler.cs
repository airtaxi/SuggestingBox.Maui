using Microsoft.Maui.Handlers;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SuggestingBox.Maui;

internal class FormattedEditorHandler : ViewHandler<FormattedEditor, RichEditBox>
{
    private bool ignoreTextChange;

    public static IPropertyMapper<FormattedEditor, FormattedEditorHandler> EditorMapper =
        new PropertyMapper<FormattedEditor, FormattedEditorHandler>(ViewHandler.ViewMapper)
        {
            [nameof(Editor.Text)] = MapText,
            [nameof(Editor.Placeholder)] = MapPlaceholder,
        };

    public FormattedEditorHandler() : base(EditorMapper) { }

    protected override RichEditBox CreatePlatformView() => new() { AcceptsReturn = true };

    protected override void ConnectHandler(RichEditBox platformView)
    {
        base.ConnectHandler(platformView);
        platformView.TextChanged += OnNativeTextChanged;
        platformView.SelectionChanged += OnNativeSelectionChanged;
    }

    protected override void DisconnectHandler(RichEditBox platformView)
    {
        platformView.TextChanged -= OnNativeTextChanged;
        platformView.SelectionChanged -= OnNativeSelectionChanged;
        base.DisconnectHandler(platformView);
    }

    private void OnNativeTextChanged(object sender, RoutedEventArgs args)
    {
        if (ignoreTextChange) return;

        PlatformView.Document.GetText(TextGetOptions.None, out string text);
        text = text.TrimEnd('\r', '\n');
        int cursorPosition = Math.Min(PlatformView.Document.Selection.StartPosition, text.Length);

        ignoreTextChange = true;
        VirtualView.Text = text;
        VirtualView.CursorPosition = cursorPosition;
        ignoreTextChange = false;
    }

    private void OnNativeSelectionChanged(object sender, RoutedEventArgs args)
    {
        if (ignoreTextChange) return;

        PlatformView.Document.GetText(TextGetOptions.None, out string text);
        int textLength = text.TrimEnd('\r', '\n').Length;
        VirtualView.CursorPosition = Math.Min(PlatformView.Document.Selection.StartPosition, textLength);
    }

    private static void MapText(FormattedEditorHandler handler, FormattedEditor editor)
    {
        if (handler.ignoreTextChange) return;

        handler.ignoreTextChange = true;
        var document = handler.PlatformView.Document;
        document.GetText(TextGetOptions.None, out string currentText);
        currentText = currentText.TrimEnd('\r', '\n');

        if (currentText != (editor.Text ?? string.Empty))
        {
            document.SetText(TextSetOptions.None, editor.Text ?? string.Empty);

            // Restore cursor position after SetText (which resets it to 0)
            int position = Math.Min(editor.CursorPosition, (editor.Text ?? string.Empty).Length);
            document.Selection.SetRange(position, position);
        }

        handler.ignoreTextChange = false;
    }

    private static void MapPlaceholder(FormattedEditorHandler handler, FormattedEditor editor)
    {
        handler.PlatformView.PlaceholderText = editor.Placeholder ?? string.Empty;
    }
}
