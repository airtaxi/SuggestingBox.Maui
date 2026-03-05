using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace SuggestingBox.Maui;

internal class FormattedEditorHandler : EditorHandler
{
    protected override MauiAppCompatEditText CreatePlatformView() => new PasteAwareEditText(Context);
}
