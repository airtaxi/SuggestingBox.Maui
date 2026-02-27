using Foundation;
using UIKit;
using CoreGraphics;
using Microsoft.Maui.Platform;

namespace SuggestingBox.Maui;

internal static partial class TextFormatter
{
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
        if (collectionView.Handler?.PlatformView is UICollectionView nativeCollectionView)
            return nativeCollectionView;

        if (collectionView.Handler?.PlatformView is not UIView platformRootView)
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

    internal static partial double GetCursorBottomY(Editor editor)
    {
        if (editor.Handler?.PlatformView is not UITextView textView) return 0;
        if (textView.SelectedTextRange is not UITextRange selectedRange) return 0;

        CGRect caretRect = textView.GetCaretRectForPosition(selectedRange.Start);
        return caretRect.GetMaxY();
    }
}
