namespace SuggestingBox.Maui;

public class SuggestingBoxTokenInfo
{
    public SuggestingBoxTokenInfo() { }

    public SuggestingBoxTokenInfo(int startIndex, string prefix, string displayText, SuggestionFormat format, object item = null)
    {
        Kind = SuggestingBoxTokenKind.Mention;
        StartIndex = startIndex;
        Prefix = prefix ?? string.Empty;
        DisplayText = displayText ?? string.Empty;
        Format = format ?? new SuggestionFormat();
        Item = item;
    }

    public static SuggestingBoxTokenInfo CreateImage(int startIndex, byte[] imageData, string contentType = null, string alternativeText = "", double widthRequest = -1, double heightRequest = -1, object item = null) =>
        new()
        {
            Kind = SuggestingBoxTokenKind.Image,
            StartIndex = startIndex,
            ImageData = imageData ?? [],
            ContentType = ImageContentTypeDetector.Resolve(imageData, contentType),
            AlternativeText = alternativeText ?? string.Empty,
            WidthRequest = widthRequest,
            HeightRequest = heightRequest,
            Item = item
        };

    public SuggestingBoxTokenKind Kind { get; set; } = SuggestingBoxTokenKind.Mention;
    public int StartIndex { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public SuggestionFormat Format { get; set; } = new();
    public object Item { get; set; }
    public byte[] ImageData { get; set; } = [];
    public string ContentType { get; set; } = ImageContentTypeDetector.DefaultContentType;
    public string AlternativeText { get; set; } = string.Empty;
    public double WidthRequest { get; set; } = -1;
    public double HeightRequest { get; set; } = -1;
    public int Length => Kind == SuggestingBoxTokenKind.Image
        ? SuggestingBoxText.ImagePlaceholderString.Length
        : FullText.Length;
    public int EndIndex => StartIndex + Length;
    public string FullText => Kind == SuggestingBoxTokenKind.Image
        ? SuggestingBoxText.ImagePlaceholderString
        : (Prefix ?? string.Empty) + (DisplayText ?? string.Empty);
}
