using Foundation;
using UIKit;
using CoreGraphics;
using Microsoft.Maui.Platform;

namespace SuggestingBox.Maui;

internal static partial class TextFormatter
{
    // Read MAUI-level TextColor to avoid contamination: iOS overwrites
    // textView.TextColor with position-0 attributes after setting AttributedText.
    private static UIColor GetEditorForegroundColor(Editor editor) =>
        editor.TextColor is Color mauiColor ? mauiColor.ToPlatform() : UIColor.Label;

    internal static partial void ApplyFormatting(Editor editor, IReadOnlyList<SuggestionToken> tokens)
    {
        if (editor.Handler?.PlatformView is not UITextView textView) return;

        string text = textView.Text ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return;

        var attributedString = new NSMutableAttributedString(text);
        var fullRange = new NSRange(0, text.Length);

        // Always use a regular-weight system font for the default range so that
        // bold formatting from a previously deleted token cannot leak to the rest
        // of the text via textView.Font (iOS updates Font to position-0 attributes
        // after setting AttributedText).
        var fontSize = textView.Font?.PointSize ?? 17;
        var defaultFont = UIFont.SystemFontOfSize(fontSize);
        var foregroundColor = GetEditorForegroundColor(editor);

        attributedString.AddAttribute(UIStringAttributeKey.Font, defaultFont, fullRange);
        attributedString.AddAttribute(UIStringAttributeKey.ForegroundColor, foregroundColor, fullRange);

        foreach (var token in tokens)
        {
            if (token.StartIndex < 0 || token.EndIndex > text.Length) continue;

            var range = new NSRange(token.StartIndex, token.Length);
            var format = token.Format;

            if (format.BackgroundColor != Colors.Transparent)
                attributedString.AddAttribute(UIStringAttributeKey.BackgroundColor, format.BackgroundColor.ToPlatform(), range);

            attributedString.AddAttribute(UIStringAttributeKey.ForegroundColor, format.ForegroundColor.ToPlatform(), range);

            if (format.Bold == FormatEffect.On)
                attributedString.AddAttribute(UIStringAttributeKey.Font, UIFont.BoldSystemFontOfSize(fontSize), range);
        }

        var selectedRange = textView.SelectedRange;
        textView.AttributedText = attributedString;
        textView.SelectedRange = selectedRange;

        // Reset typing attributes: copy existing dictionary (preserving UIKit internals),
        // then override font/foreground and strip background color from deleted tokens.
        var typingAttributes = new NSMutableDictionary(textView.TypingAttributes2);
        typingAttributes[UIStringAttributeKey.Font] = defaultFont;
        typingAttributes[UIStringAttributeKey.ForegroundColor] = foregroundColor;
        typingAttributes.Remove(UIStringAttributeKey.BackgroundColor);
        textView.TypingAttributes2 = typingAttributes;
    }

    internal static partial void ResetNativeText(Editor editor, string text, int cursorPosition)
    {
        if (editor.Handler?.PlatformView is not UITextView textView) return;

        textView.Text = text;
        textView.SelectedRange = new NSRange(Math.Min(cursorPosition, text.Length), 0);

        var fontSize = textView.Font?.PointSize ?? 17;
        var defaultFont = UIFont.SystemFontOfSize(fontSize);
        var foregroundColor = GetEditorForegroundColor(editor);

        var typingAttributes = new NSMutableDictionary(textView.TypingAttributes2);
        typingAttributes[UIStringAttributeKey.Font] = defaultFont;
        typingAttributes[UIStringAttributeKey.ForegroundColor] = foregroundColor;
        typingAttributes.Remove(UIStringAttributeKey.BackgroundColor);
        textView.TypingAttributes2 = typingAttributes;
    }

    internal static partial double GetNativeContentHeight(CollectionView collectionView)
    {
        UICollectionView nativeView = TryGetNativeCollectionView(collectionView);
        if (nativeView is null)
            return 0;

        nativeView.LayoutIfNeeded();
        if (nativeView.NumberOfSections() <= 0 || nativeView.Bounds.Width <= 0)
            return 0;

        var layoutAttributes = nativeView.CollectionViewLayout.LayoutAttributesForElementsInRect(
            new CGRect(0, 0, nativeView.Bounds.Width, nfloat.MaxValue));
        if (layoutAttributes is null || layoutAttributes.Length == 0)
            return 0;

        nfloat maxBottom = 0;
        foreach (var layoutAttribute in layoutAttributes)
        {
            if (layoutAttribute.RepresentedElementCategory != UICollectionElementCategory.Cell) continue;
            if (layoutAttribute.Frame.GetMaxY() > maxBottom)
                maxBottom = layoutAttribute.Frame.GetMaxY();
        }

        return maxBottom > 0 ? maxBottom : nativeView.ContentSize.Height;
    }

    private static UICollectionView TryGetNativeCollectionView(CollectionView collectionView)
    {
        var platformView = collectionView.Handler?.PlatformView;
        if (platformView is UICollectionView nativeCollectionView)
            return nativeCollectionView;

        if (platformView is not UIView platformRootView)
            return null;

        return FindCollectionViewDescendant(platformRootView);
    }

    private static UICollectionView FindCollectionViewDescendant(UIView view)
    {
        if (view is UICollectionView nativeCollectionView)
            return nativeCollectionView;

        foreach (UIView subview in view.Subviews)
        {
            UICollectionView foundCollectionView = FindCollectionViewDescendant(subview);
            if (foundCollectionView is not null)
                return foundCollectionView;
        }

        return null;
    }

    internal static partial void SubscribeCursorChanged(Editor editor, Action<int, int> onCursorMoved) { }
    internal static partial void UnsubscribeCursorChanged(Editor editor) { }
    private static readonly Dictionary<Editor, (UITextView textView, PasteInterceptor interceptor)>
        pasteHandlers = [];

    internal static partial void SubscribePasteHandler(Editor editor, Action<byte[]> onImagePasted)
    {
        if (editor.Handler?.PlatformView is not UITextView textView) return;

        var interceptor = new PasteInterceptor(textView, onImagePasted);
        pasteHandlers[editor] = (textView, interceptor);
    }

    internal static partial void UnsubscribePasteHandler(Editor editor)
    {
        if (!pasteHandlers.TryGetValue(editor, out var entry)) return;
        entry.interceptor.Dispose();
        pasteHandlers.Remove(editor);
    }

    private class PasteInterceptor : IDisposable
    {
        private readonly UITextView textView;
        private readonly Action<byte[]> onImagePasted;
        private readonly Foundation.NSObject notificationToken;

        internal PasteInterceptor(UITextView textView, Action<byte[]> onImagePasted)
        {
            this.textView = textView;
            this.onImagePasted = onImagePasted;

            // Observe UITextView text changes to detect paste events with images
            notificationToken = NSNotificationCenter.DefaultCenter.AddObserver(
                UITextView.TextDidChangeNotification, HandleTextChanged, textView);
        }

        private void HandleTextChanged(NSNotification notification)
        {
            var pasteboard = UIPasteboard.General;
            if (!pasteboard.HasImages) return;

            var image = pasteboard.Image;
            if (image is null) return;

            using var pngData = image.AsPNG();
            if (pngData is null || pngData.Length == 0) return;

            byte[] imageData = new byte[pngData.Length];
            System.Runtime.InteropServices.Marshal.Copy(pngData.Bytes, imageData, 0, (int)pngData.Length);

            onImagePasted(imageData);
        }

        public void Dispose()
        {
            if (notificationToken is not null)
                NSNotificationCenter.DefaultCenter.RemoveObserver(notificationToken);
        }
    }

    internal static partial double GetCursorBottomY(Editor editor)
    {
        if (editor.Handler?.PlatformView is not UITextView textView) return 0;
        if (textView.SelectedTextRange is not UITextRange selectedRange) return 0;

        CGRect caretRect = textView.GetCaretRectForPosition(selectedRange.Start);
        return caretRect.GetMaxY();
    }

    private static double cachedKeyboardHeight;
    private static bool keyboardObserversRegistered;

    internal static partial double GetSoftKeyboardHeight() => cachedKeyboardHeight;

    internal static void RegisterKeyboardObservers()
    {
        if (keyboardObserversRegistered) return;
        keyboardObserversRegistered = true;

        UIKeyboard.Notifications.ObserveWillShow((sender, args) =>
        {
            CGRect frame = args.FrameEnd;
            cachedKeyboardHeight = frame.Height;
        });

        UIKeyboard.Notifications.ObserveWillHide((sender, args) =>
        {
            cachedKeyboardHeight = 0;
        });
    }

    internal static partial Point GetPositionRelativeToView(VisualElement source, VisualElement target)
    {
        var sourceNativeView = source.Handler?.PlatformView as UIView;
        var targetNativeView = target.Handler?.PlatformView as UIView;
        if (sourceNativeView is null || targetNativeView is null)
            return new Point(double.NaN, double.NaN);

        CGPoint point = sourceNativeView.ConvertPointToView(CGPoint.Empty, targetNativeView);
        return new Point(point.X, point.Y);
    }
}
