using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Text.Style;

namespace SuggestingBox.Maui;

/// <summary>
/// Factory for creating Android color spans with the correct constructor signature for .NET 10
/// The .NET 10 Android bindings appear to only expose the Parcel constructor
/// We need to bypass this using reflection/JNI to access the int constructor
/// </summary>
internal static class AndroidColorSpanFactory
{
    public static BackgroundColorSpan CreateBackgroundColorSpan(int colorArgb)
    {
        // Create via reflection using JNI to call the int constructor directly
        // This accesses the original Android Java API: BackgroundColorSpan(int color)

        var backgroundColorSpanClass = JNIEnv.FindClass("android/text/style/BackgroundColorSpan");
        var backgroundColorSpanConstructor = JNIEnv.GetMethodID(backgroundColorSpanClass, "<init>", "(I)V");
        var backgroundColorSpanHandle = JNIEnv.NewObject(backgroundColorSpanClass, backgroundColorSpanConstructor, new JValue(colorArgb));
        var backgroundColorSpan = Java.Lang.Object.GetObject<BackgroundColorSpan>(backgroundColorSpanHandle, JniHandleOwnership.TransferLocalRef);

        return backgroundColorSpan;
    }

    public static ForegroundColorSpan CreateForegroundColorSpan(int colorArgb)
    {
        var foregroundColorSpanClass = JNIEnv.FindClass("android/text/style/ForegroundColorSpan");
        var foregroundColorSpanConstructor = JNIEnv.GetMethodID(foregroundColorSpanClass, "<init>", "(I)V");
        var foregroundColorSpanHandle = JNIEnv.NewObject(foregroundColorSpanClass, foregroundColorSpanConstructor, new JValue(colorArgb));
        var foregroundColorSpan = Java.Lang.Object.GetObject<ForegroundColorSpan>(foregroundColorSpanHandle, JniHandleOwnership.TransferLocalRef);

        return foregroundColorSpan;
    }
}

