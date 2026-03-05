using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using AndroidX.Core.View;
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
    private static readonly Dictionary<Editor, (Android.Widget.EditText editText, ContentReceiver receiver)>
        pasteHandlers = [];

    internal static partial void SubscribePasteHandler(Editor editor, Action<byte[]> onImagePasted)
    {
        if (editor.Handler?.PlatformView is not Android.Widget.EditText editText) return;

        var receiver = new ContentReceiver(editText, onImagePasted);
        ViewCompat.SetOnReceiveContentListener(editText, ContentReceiver.MimeTypes, receiver);

        if (editText is PasteAwareEditText pasteAwareEditText)
            pasteAwareEditText.OnImagePasted = onImagePasted;

        pasteHandlers[editor] = (editText, receiver);
    }

    internal static partial void UnsubscribePasteHandler(Editor editor)
    {
        if (!pasteHandlers.TryGetValue(editor, out var entry)) return;
        ViewCompat.SetOnReceiveContentListener(entry.editText, ContentReceiver.MimeTypes, null);

        if (entry.editText is PasteAwareEditText pasteAwareEditText)
            pasteAwareEditText.OnImagePasted = null;

        pasteHandlers.Remove(editor);
    }

    private class ContentReceiver(Android.Widget.EditText editText, Action<byte[]> onImagePasted)
        : Java.Lang.Object, IOnReceiveContentListener
    {
        internal static readonly string[] MimeTypes = ["image/*"];

        public AndroidX.Core.View.ContentInfoCompat OnReceiveContent(Android.Views.View view, AndroidX.Core.View.ContentInfoCompat payload)
        {
            var split = payload.Partition(new UriPredicate());
            var uriContent = split.First as AndroidX.Core.View.ContentInfoCompat;
            var remaining = split.Second as AndroidX.Core.View.ContentInfoCompat;

            if (uriContent is not null)
            {
                var clip = uriContent.Clip;

                for (int index = 0; index < clip.ItemCount; index++)
                {
                    var uri = clip.GetItemAt(index).Uri;
                    if (uri is null) continue;

                    try
                    {
                        var context = editText.Context;
                        if (context is null) continue;

                        using var inputStream = context.ContentResolver?.OpenInputStream(uri);
                        if (inputStream is null) continue;

                        using var memoryStream = new System.IO.MemoryStream();
                        inputStream.CopyTo(memoryStream);
                        byte[] imageData = memoryStream.ToArray();

                        if (imageData.Length > 0)
                            editText.Post(() => onImagePasted(imageData));
                    }
                    catch (Exception) { }
                }
            }

            return remaining;
        }
    }

    private class UriPredicate : Java.Lang.Object, AndroidX.Core.Util.IPredicate
    {
        public bool Test(Java.Lang.Object item)
        {
            if (item is Android.Content.ClipData.Item clipItem)
                return clipItem.Uri is not null;
            return false;
        }
    }

    internal static partial double GetCursorBottomY(Editor editor)
    {
        if (editor.Handler?.PlatformView is not Android.Widget.EditText editText) return 0;
        if (editText.Layout is null) return 0;

        int selectionStart = Math.Max(0, editText.SelectionStart);
        int line = editText.Layout.GetLineForOffset(selectionStart);
        float lineBottomPx = editText.Layout.GetLineBottom(line) + editText.TotalPaddingTop - editText.ScrollY;
        float density = editText.Resources?.DisplayMetrics?.Density ?? 1f;
        return lineBottomPx / density;
    }

    internal static partial double GetSoftKeyboardHeight()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30)) return 0;

        var activity = Platform.CurrentActivity;
        if (activity is null) return 0;

        var rootView = activity.FindViewById(Android.Resource.Id.Content);
        if (rootView is null) return 0;

        var windowInsets = rootView.RootWindowInsets;
        if (windowInsets is null) return 0;

        var imeInsets = windowInsets.GetInsets(Android.Views.WindowInsets.Type.Ime());
        float density = rootView.Resources?.DisplayMetrics?.Density ?? 1f;
        return imeInsets.Bottom / density;
    }

    internal static partial Microsoft.Maui.Graphics.Point GetPositionRelativeToView(VisualElement source, VisualElement target)
    {
        var sourceNativeView = source.Handler?.PlatformView as Android.Views.View;
        var targetNativeView = target.Handler?.PlatformView as Android.Views.View;
        if (sourceNativeView is null || targetNativeView is null)
            return new Microsoft.Maui.Graphics.Point(double.NaN, double.NaN);

        int[] sourceLocation = new int[2];
        int[] targetLocation = new int[2];
        sourceNativeView.GetLocationOnScreen(sourceLocation);
        targetNativeView.GetLocationOnScreen(targetLocation);

        float density = sourceNativeView.Resources?.DisplayMetrics?.Density ?? 1f;
        return new Microsoft.Maui.Graphics.Point(
            (sourceLocation[0] - targetLocation[0]) / density,
            (sourceLocation[1] - targetLocation[1]) / density);
    }
}
