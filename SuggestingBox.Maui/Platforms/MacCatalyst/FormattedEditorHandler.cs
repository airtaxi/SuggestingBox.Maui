using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace SuggestingBox.Maui;

internal class FormattedEditorHandler : EditorHandler
{
    protected override MauiTextView CreatePlatformView()
        => new PasteAwareTextView();
}

internal class PasteAwareTextView : MauiTextView
{
    internal Action<byte[], int> OnImagePasted { get; set; }

    public override void Paste(NSObject sender)
    {
        if (TryHandleImagePaste()) return;
        base.Paste(sender);
    }

    public override void Paste(NSItemProvider[] itemProviders)
    {
        if (TryHandleImagePaste()) return;
        base.Paste(itemProviders);
    }

    private bool TryHandleImagePaste()
    {
        if (OnImagePasted is null) return false;

        int cursorPosition = Math.Max(0, (int)SelectedRange.Location);
        var pasteboard = UIPasteboard.General;
        if (!pasteboard.HasImages) return false;

        var image = pasteboard.Image;
        if (image is null) return false;

        using var pngData = image.AsPNG();
        if (pngData is null || pngData.Length == 0) return false;

        byte[] imageData = new byte[pngData.Length];
        System.Runtime.InteropServices.Marshal.Copy(pngData.Bytes, imageData, 0, (int)pngData.Length);
        OnImagePasted(imageData, cursorPosition);
        return true;
    }
}
