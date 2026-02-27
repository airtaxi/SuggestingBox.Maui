namespace SuggestingBox.Maui;

public static partial class SuggestingBoxInitializer
{
    static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers)
    {
        handlers.AddHandler<FormattedEditor, FormattedEditorHandler>();
    }
}
