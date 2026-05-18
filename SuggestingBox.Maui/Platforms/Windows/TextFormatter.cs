using System.Runtime.CompilerServices;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinFormatEffect = Microsoft.UI.Text.FormatEffect;

namespace SuggestingBox.Maui;

internal static partial class TextFormatter
{
    private static readonly Dictionary<RichEditBox, string> s_imageLayoutSignatures = [];
    private static readonly Dictionary<RichEditBox, double> s_lastKnownEditorHeights = [];

    internal static partial void ApplyFormatting(Editor editor, IReadOnlyList<SuggestionToken> tokens)
    {
        if (editor.Handler is not FormattedEditorHandler formattedEditorHandler) return;
        if (formattedEditorHandler.PlatformView is not RichEditBox richEditBox) return;

        var document = richEditBox.Document;
        var hasImages = tokens.Any(token => token.IsImage);
        var cursorPosition = 0;
        var textLength = 0;
        var shouldInsertImages = false;
        var imageLayoutSignature = string.Empty;
        var editorText = editor.Text ?? string.Empty;

        void ResetDocumentText()
        {
            document.SetText(TextSetOptions.None, editorText);
            document.Selection.SetRange(cursorPosition, cursorPosition);
        }

        void InsertImageTokens()
        {
            foreach (var token in tokens.Where(token => token.IsImage)) InsertImageToken(richEditBox, token, textLength);
        }

        if (hasImages)
        {
            RememberEditorHeight(richEditBox);
            cursorPosition = Math.Min(document.Selection.StartPosition, editorText.Length);
            imageLayoutSignature = GetImageLayoutSignature(tokens);
            shouldInsertImages = !s_imageLayoutSignatures.TryGetValue(richEditBox, out var currentSignature) || currentSignature != imageLayoutSignature;

            if (shouldInsertImages)
            {
                FreezeEditorHeight(richEditBox);
                formattedEditorHandler.RunIgnoringTextChange(ResetDocumentText);
            }

            textLength = editorText.Length;
        }
        else
        {
            s_imageLayoutSignatures.Remove(richEditBox);
            s_lastKnownEditorHeights.Remove(richEditBox);
            richEditBox.MinHeight = 0;
            document.GetText(TextGetOptions.UseLf, out var fullText);
            textLength = fullText.Length;
            cursorPosition = Math.Min(document.Selection.StartPosition, textLength);
        }

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
            if (token.IsImage) continue;

            var range = document.GetRange(token.StartIndex, token.EndIndex);
            var format = token.Format;

            if (format.BackgroundColor != Colors.Transparent)
            {
                var color = format.BackgroundColor;
                range.CharacterFormat.BackgroundColor = Windows.UI.Color.FromArgb((byte)(color.Alpha * 255), (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));
            }

            if (format.ForegroundColor != Colors.Black)
            {
                var color = format.ForegroundColor;
                range.CharacterFormat.ForegroundColor = Windows.UI.Color.FromArgb((byte)(color.Alpha * 255), (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));
            }

            if (format.Bold == FormatEffect.On)
                range.CharacterFormat.Bold = WinFormatEffect.On;
        }

        if (shouldInsertImages)
        {
            formattedEditorHandler.RunIgnoringTextChange(InsertImageTokens);
            s_imageLayoutSignatures[richEditBox] = imageLayoutSignature;
            RememberEditorHeightAfterLayout(richEditBox);
        }

        // Reset formatting at the current insertion point (selection) so subsequently typed text
        // does not inherit the last token's character format.
        document.Selection.SetRange(cursorPosition, cursorPosition);
        var selection = document.Selection;
        if (selection.StartPosition == selection.EndPosition)
        {
            selection.CharacterFormat.BackgroundColor = defaultFormat.BackgroundColor;
            selection.CharacterFormat.ForegroundColor = defaultFormat.ForegroundColor;
            selection.CharacterFormat.Bold = WinFormatEffect.Off;
        }
    }

    private static string GetImageLayoutSignature(IReadOnlyList<SuggestionToken> tokens) =>
        string.Join("|", tokens.Where(token => token.IsImage).Select(CreateImageLayoutSignature));

    private static string CreateImageLayoutSignature(SuggestionToken token) =>
        $"{token.StartIndex}:{RuntimeHelpers.GetHashCode(token.ImageData)}:{token.ImageData.Length}:{token.ContentType}:{token.AlternativeText}:{token.WidthRequest}:{token.HeightRequest}";

    internal static void InvalidateImageLayout(RichEditBox richEditBox) => s_imageLayoutSignatures.Remove(richEditBox);

    private static void RememberEditorHeight(RichEditBox richEditBox)
    {
        if (richEditBox.ActualHeight <= 0) return;
        s_lastKnownEditorHeights[richEditBox] = Math.Max(s_lastKnownEditorHeights.GetValueOrDefault(richEditBox), richEditBox.ActualHeight);
    }

    private static void FreezeEditorHeight(RichEditBox richEditBox)
    {
        if (!s_lastKnownEditorHeights.TryGetValue(richEditBox, out var lastKnownEditorHeight)) return;
        if (lastKnownEditorHeight <= 0) return;
        richEditBox.MinHeight = Math.Max(richEditBox.MinHeight, lastKnownEditorHeight);
    }

    private static void RememberEditorHeightAfterLayout(RichEditBox richEditBox)
    {
        richEditBox.DispatcherQueue.TryEnqueue(() =>
        {
            richEditBox.UpdateLayout();
            RememberEditorHeight(richEditBox);
            FreezeEditorHeight(richEditBox);
        });
    }

    private static void InsertImageToken(RichEditBox richEditBox, SuggestionToken token, int textLength)
    {
        if (token.StartIndex < 0 || token.EndIndex > textLength || token.ImageData.Length == 0) return;

        try
        {
            using var stream = CreateImageStream(token.ImageData);
            var (width, height) = GetImageSize(token, stream);
            stream.Seek(0);

            int imageAscent = Math.Max(1, height);
            var range = richEditBox.Document.GetRange(token.StartIndex, token.EndIndex);
            range.InsertImage(width, height, imageAscent, VerticalCharacterAlignment.Baseline, token.AlternativeText ?? string.Empty, stream);
        }
        catch (Exception) { }
    }

    private static InMemoryRandomAccessStream CreateImageStream(byte[] imageData)
    {
        var stream = new InMemoryRandomAccessStream();
        using var outputStream = stream.GetOutputStreamAt(0);
        using var dataWriter = new DataWriter(outputStream);
        dataWriter.WriteBytes(imageData);
        dataWriter.StoreAsync().AsTask().GetAwaiter().GetResult();
        dataWriter.FlushAsync().AsTask().GetAwaiter().GetResult();
        return stream;
    }

    private static (int Width, int Height) GetImageSize(SuggestionToken token, IRandomAccessStream stream)
    {
        double imageWidth = token.WidthRequest;
        double imageHeight = token.HeightRequest;

        if (imageWidth <= 0 || imageHeight <= 0)
        {
            try
            {
                stream.Seek(0);
                var decoder = BitmapDecoder.CreateAsync(stream).AsTask().GetAwaiter().GetResult();
                double originalWidth = decoder.PixelWidth;
                double originalHeight = decoder.PixelHeight;

                if (imageWidth > 0 && imageHeight <= 0) imageHeight = originalHeight * (imageWidth / originalWidth);
                else if (imageHeight > 0 && imageWidth <= 0) imageWidth = originalWidth * (imageHeight / originalHeight);
                else
                {
                    imageWidth = originalWidth;
                    imageHeight = originalHeight;
                }
            }
            catch (Exception)
            {
                if (imageWidth <= 0) imageWidth = 160;
                if (imageHeight <= 0) imageHeight = 90;
            }
        }

        return ((int)Math.Max(1, Math.Round(imageWidth)), (int)Math.Max(1, Math.Round(imageHeight)));
    }

    internal static partial void ResetNativeText(Editor editor, string text, int cursorPosition)
    {
        if (editor.Handler?.PlatformView is not RichEditBox richEditBox) return;

        // Set cursor position directly on the native RichEditBox — MAUI's editor.CursorPosition
        // assignment on Windows sometimes lands at the wrong position (e.g. right after # or @)
        // because the handler defers or clamps the position.
        richEditBox.Document.Selection.SetRange(Math.Min(cursorPosition, text.Length), Math.Min(cursorPosition, text.Length));
    }

    private static readonly Dictionary<Editor, (RichEditBox richEditBox, RoutedEventHandler handler)>
        s_cursorHandlers = [];

    private static readonly Dictionary<Editor, (RichEditBox richEditBox, TextControlPasteEventHandler handler)>
        s_pasteHandlers = [];

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
        s_cursorHandlers[editor] = (richEditBox, selectionChangedHandler);
    }

    internal static partial void UnsubscribeCursorChanged(Editor editor)
    {
        if (!s_cursorHandlers.TryGetValue(editor, out var entry)) return;
        entry.richEditBox.SelectionChanged -= entry.handler;
        s_cursorHandlers.Remove(editor);
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

        ScrollViewer scrollViewer = FindDescendant<ScrollViewer>(platformView);
        if (scrollViewer is null)
            return 0;

        // Force WinUI to complete its deferred layout pass
        if (scrollViewer.ExtentHeight <= 0)
            scrollViewer.UpdateLayout();

        // When content overflows, ExtentHeight is the true content height
        if (scrollViewer.ScrollableHeight > 0)
            return scrollViewer.ExtentHeight;

        // When content fits within the viewport, ExtentHeight == ViewportHeight (not content height).
        // Sum realized item heights from the items panel for an accurate measurement.
        double panelContentHeight = MeasureItemsPanelContentHeight(scrollViewer);

        return panelContentHeight > 0 ? panelContentHeight : scrollViewer.ExtentHeight;
    }

    private static double MeasureItemsPanelContentHeight(ScrollViewer scrollViewer)
    {
        var itemsPanel = FindDescendant<ItemsStackPanel>(scrollViewer);
        if (itemsPanel is null || itemsPanel.Children.Count <= 0)
            return 0;

        double totalHeight = 0;
        foreach (UIElement child in itemsPanel.Children)
        {
            if (child is FrameworkElement frameworkElement)
                totalHeight += frameworkElement.DesiredSize.Height;
        }

        return totalHeight;
    }

    internal static partial void SubscribePasteHandler(Editor editor, Action<byte[], int> onImagePasteRequested)
    {
        if (editor.Handler?.PlatformView is not RichEditBox richEditBox) return;

        void pasteHandler(object sender, TextControlPasteEventArgs eventArgs)
        {
            var clipboard = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (!clipboard.Contains(StandardDataFormats.Bitmap)) return;

            // Block the image from being inserted into the RichEditBox
            eventArgs.Handled = true;
            int cursorPosition = Math.Min(richEditBox.Document.Selection.StartPosition, (editor.Text ?? string.Empty).Length);

            Task.Run(async () =>
            {
                var streamReference = await clipboard.GetBitmapAsync();
                using var stream = await streamReference.OpenReadAsync();
                byte[] imageData = new byte[stream.Size];
                using var reader = new DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(imageData);

                richEditBox.DispatcherQueue.TryEnqueue(() => onImagePasteRequested(imageData, cursorPosition));
            });
        }

        richEditBox.Paste += pasteHandler;
        s_pasteHandlers[editor] = (richEditBox, pasteHandler);
    }

    internal static partial void UnsubscribePasteHandler(Editor editor)
    {
        if (!s_pasteHandlers.TryGetValue(editor, out var entry)) return;
        entry.richEditBox.Paste -= entry.handler;
        s_imageLayoutSignatures.Remove(entry.richEditBox);
        s_lastKnownEditorHeights.Remove(entry.richEditBox);
        s_pasteHandlers.Remove(editor);
    }

    internal static partial void SubscribeUndoHandler(Editor editor, Func<bool> onUndoRequested)
    {
        if (editor.Handler is not FormattedEditorHandler formattedEditorHandler) return;
        formattedEditorHandler.UndoRequested = onUndoRequested;
    }

    internal static partial void UnsubscribeUndoHandler(Editor editor)
    {
        if (editor.Handler is not FormattedEditorHandler formattedEditorHandler) return;
        formattedEditorHandler.UndoRequested = null;
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

    internal static partial double GetSoftKeyboardHeight() => 0;

    internal static partial Point GetPositionRelativeToView(VisualElement source, VisualElement target)
    {
        var sourceNativeView = source.Handler?.PlatformView as FrameworkElement;
        var targetNativeView = target.Handler?.PlatformView as FrameworkElement;
        if (sourceNativeView is null || targetNativeView is null)
            return new Point(double.NaN, double.NaN);

        var transform = sourceNativeView.TransformToVisual(targetNativeView);
        var point = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
        return new Point(point.X, point.Y);
    }
}
