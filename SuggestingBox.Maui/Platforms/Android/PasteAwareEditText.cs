using Android.Content;
using Android.Runtime;
using Android.Util;
using Microsoft.Maui.Platform;

namespace SuggestingBox.Maui;

internal class PasteAwareEditText : MauiAppCompatEditText
{
    private const string LogTag = "SuggestingBox";

    internal Action<byte[]> OnImagePasted { get; set; }

    public PasteAwareEditText(Context context) : base(context)
    {
        Log.Debug(LogTag, "PasteAwareEditText created");
    }

    public override bool OnTextContextMenuItem(int id)
    {
        Log.Debug(LogTag, $"OnTextContextMenuItem called with id={id} (Paste={Android.Resource.Id.Paste})");
        if (id == Android.Resource.Id.Paste && TryHandleImagePaste())
            return true;
        return base.OnTextContextMenuItem(id);
    }

    private bool TryHandleImagePaste()
    {
        if (OnImagePasted is null)
        {
            Log.Warn(LogTag, "TryHandleImagePaste: OnImagePasted callback is null");
            return false;
        }

        if (Context?.GetSystemService(Context.ClipboardService) is not ClipboardManager clipboardManager)
        {
            Log.Warn(LogTag, "TryHandleImagePaste: ClipboardManager unavailable");
            return false;
        }

        var clipData = clipboardManager.PrimaryClip;
        if (clipData is null)
        {
            Log.Debug(LogTag, "TryHandleImagePaste: PrimaryClip is null");
            return false;
        }

        Log.Debug(LogTag, $"TryHandleImagePaste: ClipData has {clipData.ItemCount} item(s), description={clipData.Description}");

        for (int index = 0; index < clipData.ItemCount; index++)
        {
            var item = clipData.GetItemAt(index);
            var uri = item?.Uri;
            Log.Debug(LogTag, $"  Item[{index}]: Uri={uri}, Text={item?.Text?.Take(50)}");
            if (uri is null) continue;

            var mimeType = Context.ContentResolver?.GetType(uri);
            Log.Debug(LogTag, $"  Item[{index}]: resolved mimeType={mimeType}");
            if (mimeType is null || !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var inputStream = Context.ContentResolver.OpenInputStream(uri);
                if (inputStream is null)
                {
                    Log.Warn(LogTag, $"  Item[{index}]: OpenInputStream returned null");
                    continue;
                }

                using var memoryStream = new System.IO.MemoryStream();
                inputStream.CopyTo(memoryStream);
                byte[] imageData = memoryStream.ToArray();
                Log.Debug(LogTag, $"  Item[{index}]: read {imageData.Length} bytes");

                if (imageData.Length > 0)
                {
                    Post(() => OnImagePasted(imageData));
                    Log.Info(LogTag, $"TryHandleImagePaste: SUCCESS, dispatched {imageData.Length} bytes");
                    return true;
                }
            }
            catch (Exception exception)
            {
                Log.Error(LogTag, $"  Item[{index}]: Exception - {exception}");
            }
        }

        Log.Debug(LogTag, "TryHandleImagePaste: no image found in clipboard");
        return false;
    }
}
