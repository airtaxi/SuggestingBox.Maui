using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Microsoft.Maui.Platform;

namespace SuggestingBox.Maui;

internal static partial class TextFormatter
{
    internal static partial void ApplyFormatting(Editor editor, IReadOnlyList<SuggestionToken> tokens)
    {
        if (editor.Handler?.PlatformView is not Android.Widget.EditText editText) return;
        if (editText.EditableText is not ISpannable spannable) return;

        ClearSpans<BackgroundColorSpan>(spannable);
        ClearSpans<ForegroundColorSpan>(spannable);
        ClearSpans<StyleSpan>(spannable);

        foreach (var token in tokens)
        {
            if (token.StartIndex < 0 || token.EndIndex > spannable.Length()) continue;

            var format = token.Format;

            if (format.BackgroundColor != Colors.Transparent)
            {
                spannable.SetSpan(
                    AndroidColorSpanFactory.CreateBackgroundColorSpan(format.BackgroundColor.ToPlatform().ToArgb()),
                    token.StartIndex, token.EndIndex, SpanTypes.ExclusiveExclusive);
            }

            if (format.ForegroundColor != Colors.Black)
            {
                spannable.SetSpan(
                    AndroidColorSpanFactory.CreateForegroundColorSpan(format.ForegroundColor.ToPlatform().ToArgb()),
                    token.StartIndex, token.EndIndex, SpanTypes.ExclusiveExclusive);
            }

            if (format.Bold == FormatEffect.On)
            {
                spannable.SetSpan(
                    new StyleSpan(TypefaceStyle.Bold),
                    token.StartIndex, token.EndIndex, SpanTypes.ExclusiveExclusive);
            }
        }
    }

    internal static partial void ResetNativeText(Editor editor, string text, int cursorPosition) { }

    internal static partial double GetNativeContentHeight(CollectionView collectionView)
    {
        if (collectionView.Handler?.PlatformView is not AndroidX.RecyclerView.Widget.RecyclerView recyclerView)
            return 0;

        float density = recyclerView.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        int scrollRange = recyclerView.ComputeVerticalScrollRange();
        return scrollRange > 0 ? scrollRange / density : 0;
    }

    private static void ClearSpans<T>(ISpannable spannable) where T : Java.Lang.Object
    {
        var existing = spannable.GetSpans(0, spannable.Length(), Java.Lang.Class.FromType(typeof(T)));
        if (existing is null) return;
        foreach (var span in existing)
            spannable.RemoveSpan(span);
    }

    internal static partial void SubscribeCursorChanged(Editor editor, Action<int, int> onCursorMoved) { }
    internal static partial void UnsubscribeCursorChanged(Editor editor) { }
}
