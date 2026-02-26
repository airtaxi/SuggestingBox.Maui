namespace SuggestingBox.Maui;

public static partial class SuggestingBoxInitializer
{
    public static MauiAppBuilder UseSuggestingBox(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers => RegisterPlatformHandlers(handlers));
        return builder;
    }

    static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers);
}
