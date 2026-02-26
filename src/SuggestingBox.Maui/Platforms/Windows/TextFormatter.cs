using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
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

    internal static partial void ResetNativeText(Editor editor, string text, int cursorPosition) { }
}
