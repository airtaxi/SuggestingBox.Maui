namespace SuggestingBox.Maui;

internal static class ImageContentTypeDetector
{
    internal const string DefaultContentType = "application/octet-stream";

    internal static string Resolve(byte[] imageData, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType)) return contentType;
        return Detect(imageData);
    }

    internal static string Detect(byte[] imageData)
    {
        if (imageData is null || imageData.Length < 2) return DefaultContentType;

        if (HasPrefix(imageData, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return "image/png";

        if (HasPrefix(imageData, [0xFF, 0xD8, 0xFF]))
            return "image/jpeg";

        if (HasAsciiPrefix(imageData, "GIF87a") || HasAsciiPrefix(imageData, "GIF89a"))
            return "image/gif";

        if (HasAsciiPrefix(imageData, "BM"))
            return "image/bmp";

        if (imageData.Length >= 12
            && HasAsciiPrefix(imageData, "RIFF")
            && imageData[8] == 'W'
            && imageData[9] == 'E'
            && imageData[10] == 'B'
            && imageData[11] == 'P')
            return "image/webp";

        return DefaultContentType;
    }

    private static bool HasPrefix(byte[] imageData, byte[] prefix)
    {
        if (imageData.Length < prefix.Length) return false;
        for (var index = 0; index < prefix.Length; index++)
        {
            if (imageData[index] != prefix[index]) return false;
        }

        return true;
    }

    private static bool HasAsciiPrefix(byte[] imageData, string prefix)
    {
        if (imageData.Length < prefix.Length) return false;
        for (var index = 0; index < prefix.Length; index++)
        {
            if (imageData[index] != prefix[index]) return false;
        }

        return true;
    }
}
