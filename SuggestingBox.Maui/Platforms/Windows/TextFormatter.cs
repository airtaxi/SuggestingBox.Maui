using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WinFormatEffect = Microsoft.UI.Text.FormatEffect;

namespace SuggestingBox.Maui;

internal static partial class TextFormatter
{
    internal static partial void ApplyFormatting(Editor editor, IReadOnlyList<SuggestionToken> tokens)
    {
        if (editor.Handler?.PlatformView is not RichEditBox richEditBox) return;

        var document = richEditBox.Document;
        document.GetText(TextGetOptions.None, out string fullText);
        int textLength = fullText.TrimEnd('\r', '\n').Length;
        if (textLength <= 0) return;

        // Use the document's default character format for reset values (theme-aware)
        var defaultFormat = document.GetDefaultCharacterFormat();

        // Reset formatting for the entire document before re-applying
        var resetRange = document.GetRange(0, textLength);
        resetRange.CharacterFormat.BackgroundColor = defaultFormat.BackgroundColor;
        resetRange.CharacterFormat.ForegroundColor = defaultFormat.ForegroundColor;
        resetRange.CharacterFormat.Bold = WinFormatEffect.Off;

        foreach (var token in tokens)
        {
            if (token.StartIndex < 0 || token.EndIndex > textLength) continue;

            var range = document.GetRange(token.StartIndex, token.EndIndex);
            var format = token.Format;

            if (format.BackgroundColor != Colors.Transparent)
            {
                var color = format.BackgroundColor;
                range.CharacterFormat.BackgroundColor = Windows.UI.Color.FromArgb(
                    (byte)(color.Alpha * 255), (byte)(color.Red * 255),
                    (byte)(color.Green * 255), (byte)(color.Blue * 255));
            }

            if (format.ForegroundColor != Colors.Black)
            {
                var color = format.ForegroundColor;
                range.CharacterFormat.ForegroundColor = Windows.UI.Color.FromArgb(
                    (byte)(color.Alpha * 255), (byte)(color.Red * 255),
                    (byte)(color.Green * 255), (byte)(color.Blue * 255));
            }

            if (format.Bold == FormatEffect.On)
                range.CharacterFormat.Bold = WinFormatEffect.On;
        }

        // Reset formatting at the current insertion point (selection) so subsequently typed text
        // does not inherit the last token's character format.
        var selection = document.Selection;
        if (selection.StartPosition == selection.EndPosition)
        {
            selection.CharacterFormat.BackgroundColor = defaultFormat.BackgroundColor;
            selection.CharacterFormat.ForegroundColor = defaultFormat.ForegroundColor;
            selection.CharacterFormat.Bold = WinFormatEffect.Off;
        }
    }

    internal static partial void ResetNativeText(Editor editor, string text, int cursorPosition)
    {
        if (editor.Handler?.PlatformView is not RichEditBox richEditBox) return;

        // Set cursor position directly on the native RichEditBox — MAUI's editor.CursorPosition
        // assignment on Windows sometimes lands at the wrong position (e.g. right after # or @)
        // because the handler defers or clamps the position.
        richEditBox.Document.Selection.SetRange(
            Math.Min(cursorPosition, text.Length),
            Math.Min(cursorPosition, text.Length));
    }

    private static readonly Dictionary<Editor, (RichEditBox richEditBox, RoutedEventHandler handler)>
        cursorHandlers = [];

    internal static partial void SubscribeCursorChanged(Editor editor, Action<int, int> onCursorMoved)
    {
        if (editor.Handler?.PlatformView is not RichEditBox richEditBox) return;

        int previousPosition = -1;

        void selectionChangedHandler(object sender, RoutedEventArgs e)
        {
            var selection = richEditBox.Document.Selection;
            if (selection.StartPosition != selection.EndPosition) return;

            int newPosition = selection.StartPosition;
            int previous = previousPosition;
            previousPosition = newPosition;
            onCursorMoved(previous, newPosition);
        }

        richEditBox.SelectionChanged += selectionChangedHandler;
        cursorHandlers[editor] = (richEditBox, selectionChangedHandler);
    }

    internal static partial void UnsubscribeCursorChanged(Editor editor)
    {
        if (!cursorHandlers.TryGetValue(editor, out var entry)) return;
        entry.richEditBox.SelectionChanged -= entry.handler;
        cursorHandlers.Remove(editor);
    }

    internal static partial double GetNativeContentHeight(CollectionView collectionView)
    {
        if (collectionView.Handler?.PlatformView is not FrameworkElement platformView)
            return 0;

        // Walk the visual tree to find the inner ScrollViewer that WinUI uses for CollectionView.
        // Its ScrollableHeight + ViewportHeight gives actual content height.
        ScrollViewer scrollViewer = FindDescendant<ScrollViewer>(platformView);
        if (scrollViewer is null || scrollViewer.ExtentHeight <= 0)
            return 0;

        double density = collectionView.Handler.MauiContext?.Services
            .GetService<Microsoft.Maui.Devices.IDeviceDisplay>()?.MainDisplayInfo.Density ?? 1.0;

        return scrollViewer.ExtentHeight / density;
    }

    private static T FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
                return typedChild;

            T descendant = FindDescendant<T>(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }
}
