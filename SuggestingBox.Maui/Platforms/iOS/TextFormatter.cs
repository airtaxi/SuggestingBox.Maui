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
            if (token.IsImage)
            {
                ApplyImageAttachment(attributedString, textView, token);
                continue;
            }

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

    private static void ApplyImageAttachment(NSMutableAttributedString attributedString, UITextView textView, SuggestionToken token)
    {
        if (token.ImageData.Length == 0) return;

        using var data = NSData.FromArray(token.ImageData);
        var image = UIImage.LoadFromData(data);
        if (image is null) return;

        var (width, height) = GetImageSize(token, image.Size.Width, image.Size.Height);
        var attachment = new NSTextAttachment
        {
            Image = image,
            Bounds = new CGRect(0, 0, width, height)
        };

        var attachmentString = NSAttributedString.FromAttachment(attachment);
        attributedString.Replace(new NSRange(token.StartIndex, token.Length), attachmentString);
    }

    private static (double Width, double Height) GetImageSize(SuggestionToken token, double originalWidth, double originalHeight)
    {
        double width = token.WidthRequest;
        double height = token.HeightRequest;

        if (width > 0 && height <= 0) height = originalHeight * (width / originalWidth);
        else if (height > 0 && width <= 0) width = originalWidth * (height / originalHeight);
        else if (width <= 0 && height <= 0)
        {
            width = originalWidth;
            height = originalHeight;
        }

        return (Math.Max(1, width), Math.Max(1, height));
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

        var layoutAttributes = nativeView.CollectionViewLayout.LayoutAttributesForElementsInRect(new CGRect(0, 0, nativeView.Bounds.Width, nfloat.MaxValue));
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
    internal static partial void SubscribeUndoHandler(Editor editor, Func<bool> onUndoRequested) { }
    internal static partial void UnsubscribeUndoHandler(Editor editor) { }
    private static readonly Dictionary<Editor, UITextView>
        pasteHandlers = [];

    internal static partial void SubscribePasteHandler(Editor editor, Action<byte[], int> onImagePasteRequested)
    {
        if (editor.Handler?.PlatformView is not UITextView textView) return;

        if (textView is PasteAwareTextView pasteAwareTextView) pasteAwareTextView.OnImagePasted = onImagePasteRequested;

        pasteHandlers[editor] = textView;
    }

    internal static partial void UnsubscribePasteHandler(Editor editor)
    {
        if (!pasteHandlers.TryGetValue(editor, out var textView)) return;
        if (textView is PasteAwareTextView pasteAwareTextView) pasteAwareTextView.OnImagePasted = null;
        pasteHandlers.Remove(editor);
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
