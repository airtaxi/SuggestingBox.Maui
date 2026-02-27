using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
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

    private static readonly Dictionary<Editor, (RichEditBox richEditBox, TextControlPasteEventHandler handler)>
        pasteHandlers = [];

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

    internal static partial double GetCursorBottomY(Editor editor)
    {
        if (editor.Handler?.PlatformView is not RichEditBox richEditBox) return 0;

        richEditBox.Document.Selection.GetRect(PointOptions.ClientCoordinates, out Windows.Foundation.Rect clientRect, out _);
        if (clientRect.IsEmpty) return 0;

        // ClientCoordinates are relative to the RichEditBox's scrollable content area
        // (inside border + padding). Add the top inset to get element-relative coordinates.
        double topInset = richEditBox.BorderThickness.Top + richEditBox.Padding.Top;
        return clientRect.Y + clientRect.Height + topInset;
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

    internal static partial void SubscribePasteHandler(Editor editor, Action<byte[]> onImagePasted)
    {
        if (editor.Handler?.PlatformView is not RichEditBox richEditBox) return;

        void pasteHandler(object sender, TextControlPasteEventArgs eventArgs)
        {
            var clipboard = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (!clipboard.Contains(StandardDataFormats.Bitmap)) return;

            // Block the image from being inserted into the RichEditBox
            eventArgs.Handled = true;

            Task.Run(async () =>
            {
                var streamReference = await clipboard.GetBitmapAsync();
                using var stream = await streamReference.OpenReadAsync();
                byte[] imageData = new byte[stream.Size];
                using var reader = new DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(imageData);

                richEditBox.DispatcherQueue.TryEnqueue(() => onImagePasted(imageData));
            });
        }

        richEditBox.Paste += pasteHandler;
        pasteHandlers[editor] = (richEditBox, pasteHandler);
    }

    internal static partial void UnsubscribePasteHandler(Editor editor)
    {
        if (!pasteHandlers.TryGetValue(editor, out var entry)) return;
        entry.richEditBox.Paste -= entry.handler;
        pasteHandlers.Remove(editor);
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
