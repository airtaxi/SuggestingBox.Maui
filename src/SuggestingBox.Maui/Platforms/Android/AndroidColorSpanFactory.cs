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
        
        var bgColorSpanHandle = JNIEnv.NewObject(
            JNIEnv.FindClass("android/text/style/BackgroundColorSpan"),
            JNIEnv.GetMethodID(
                JNIEnv.FindClass("android/text/style/BackgroundColorSpan"),
                "<init>",
                "(I)V"  // Constructor that takes an int
            ),
            new JValue(colorArgb)
        );
        
        var bgSpan = Java.Lang.Object.GetObject<BackgroundColorSpan>(
            bgColorSpanHandle,
            JniHandleOwnership.TransferLocalRef
        );
        
        return bgSpan;
    }
    
    public static ForegroundColorSpan CreateForegroundColorSpan(int colorArgb)
    {
        var fgColorSpanHandle = JNIEnv.NewObject(
            JNIEnv.FindClass("android/text/style/ForegroundColorSpan"),
            JNIEnv.GetMethodID(
                JNIEnv.FindClass("android/text/style/ForegroundColorSpan"),
                "<init>",
                "(I)V"  // Constructor that takes an int
            ),
            new JValue(colorArgb)
        );
        
        var fgSpan = Java.Lang.Object.GetObject<ForegroundColorSpan>(
            fgColorSpanHandle,
            JniHandleOwnership.TransferLocalRef
        );
        
        return fgSpan;
    }
}

