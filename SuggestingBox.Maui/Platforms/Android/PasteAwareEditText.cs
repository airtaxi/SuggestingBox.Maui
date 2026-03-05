using Android.Content;
using Microsoft.Maui.Platform;

namespace SuggestingBox.Maui;

internal class PasteAwareEditText : MauiAppCompatEditText
{
    internal Action<byte[]> OnImagePasted { get; set; }

    public PasteAwareEditText(Context context) : base(context) { }

    public override bool OnTextContextMenuItem(int id)
    {
        if (id == Android.Resource.Id.Paste && TryHandleImagePaste())
            return true;
        return base.OnTextContextMenuItem(id);
    }

    private bool TryHandleImagePaste()
    {
        if (OnImagePasted is null) return false;
        if (Context?.GetSystemService(Context.ClipboardService) is not ClipboardManager clipboardManager)
            return false;

        var clipData = clipboardManager.PrimaryClip;
        if (clipData is null) return false;

        for (int index = 0; index < clipData.ItemCount; index++)
        {
            var uri = clipData.GetItemAt(index)?.Uri;
            if (uri is null) continue;

            var mimeType = Context.ContentResolver?.GetType(uri);
            if (mimeType is null || !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var inputStream = Context.ContentResolver.OpenInputStream(uri);
                if (inputStream is null) continue;

                using var memoryStream = new System.IO.MemoryStream();
                inputStream.CopyTo(memoryStream);
                byte[] imageData = memoryStream.ToArray();

                if (imageData.Length > 0)
                {
                    Post(() => OnImagePasted(imageData));
                    return true;
                }
            }
            catch (Exception) { }
        }

        return false;
    }
}
