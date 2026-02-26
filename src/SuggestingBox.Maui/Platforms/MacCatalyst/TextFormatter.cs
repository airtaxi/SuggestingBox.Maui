using Foundation;
using UIKit;
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
}
