using Microsoft.Maui.Handlers;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using WinKeyEventHandler = Microsoft.UI.Xaml.Input.KeyEventHandler;

namespace SuggestingBox.Maui;

internal class FormattedEditorHandler : ViewHandler<FormattedEditor, RichEditBox>
{
    private bool _ignoreTextChange;
    private WinKeyEventHandler _undoKeyDownHandler;

    internal Func<bool> UndoRequested { get; set; }

    public static IPropertyMapper<FormattedEditor, FormattedEditorHandler> EditorMapper =
        new PropertyMapper<FormattedEditor, FormattedEditorHandler>(ViewHandler.ViewMapper)
        {
            [nameof(Editor.Text)] = MapText,
            [nameof(Editor.Placeholder)] = MapPlaceholder,
        };

    public FormattedEditorHandler() : base(EditorMapper) { }

    protected override RichEditBox CreatePlatformView() => new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };

    protected override void ConnectHandler(RichEditBox platformView)
    {
        base.ConnectHandler(platformView);
        platformView.TextChanged += OnNativeTextChanged;
        platformView.SelectionChanged += OnNativeSelectionChanged;

        _undoKeyDownHandler = OnUndoPreviewKeyDown;
        platformView.AddHandler(UIElement.PreviewKeyDownEvent, _undoKeyDownHandler, true);
    }

    protected override void DisconnectHandler(RichEditBox platformView)
    {
        platformView.TextChanged -= OnNativeTextChanged;
        platformView.SelectionChanged -= OnNativeSelectionChanged;
        if (_undoKeyDownHandler is not null)
        {
            platformView.RemoveHandler(UIElement.PreviewKeyDownEvent, _undoKeyDownHandler);
            _undoKeyDownHandler = null;
        }

        UndoRequested = null;
        base.DisconnectHandler(platformView);
    }

    internal void RunIgnoringTextChange(Action action)
    {
        if (_ignoreTextChange)
        {
            action();
            return;
        }

        _ignoreTextChange = true;
        try { action(); }
        finally { _ignoreTextChange = false; }
    }

    private void OnUndoPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!IsUndoShortcut(args)) return;
        if (UndoRequested?.Invoke() == true) args.Handled = true;
    }

    private static bool IsUndoShortcut(KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Z) return false;

        return IsKeyDown(VirtualKey.Control) || IsKeyDown(VirtualKey.LeftControl) || IsKeyDown(VirtualKey.RightControl);
    }

    private static bool IsKeyDown(VirtualKey virtualKey)
    {
        var keyState = InputKeyboardSource.GetKeyStateForCurrentThread(virtualKey);
        return (keyState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private void OnNativeTextChanged(object sender, RoutedEventArgs args)
    {
        if (_ignoreTextChange) return;

        var text = GetDocumentText(PlatformView);
        var cursorPosition = Math.Min(PlatformView.Document.Selection.StartPosition, text.Length);

        _ignoreTextChange = true;
        VirtualView.Text = text;
        VirtualView.CursorPosition = cursorPosition;
        _ignoreTextChange = false;
    }

    private void OnNativeSelectionChanged(object sender, RoutedEventArgs args)
    {
        if (_ignoreTextChange) return;

        var text = GetDocumentText(PlatformView);
        VirtualView.CursorPosition = Math.Min(PlatformView.Document.Selection.StartPosition, text.Length);
    }

    private static void MapText(FormattedEditorHandler handler, FormattedEditor editor)
    {
        if (handler._ignoreTextChange) return;

        handler._ignoreTextChange = true;
        var document = handler.PlatformView.Document;
        var currentText = GetDocumentText(handler.PlatformView);

        if (currentText != (editor.Text ?? string.Empty))
        {
            document.SetText(TextSetOptions.None, editor.Text ?? string.Empty);
            TextFormatter.InvalidateImageLayout(handler.PlatformView);

            // Restore cursor position after SetText (which resets it to 0)
            var position = Math.Min(editor.CursorPosition, (editor.Text ?? string.Empty).Length);
            document.Selection.SetRange(position, position);
        }

        handler._ignoreTextChange = false;
    }

    private static string GetDocumentText(RichEditBox richEditBox)
    {
        richEditBox.Document.GetText(TextGetOptions.UseLf, out var text);
        return text;
    }

    private static void MapPlaceholder(FormattedEditorHandler handler, FormattedEditor editor)
    {
        handler.PlatformView.PlaceholderText = editor.Placeholder ?? string.Empty;
    }
}
