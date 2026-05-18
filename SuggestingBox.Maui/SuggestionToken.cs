namespace SuggestingBox.Maui;

internal class SuggestionToken
{
    internal SuggestionToken(int startIndex, string prefix, string displayText, SuggestionFormat format, object item = null)
    {
        Kind = SuggestingBoxTokenKind.Mention;
        StartIndex = startIndex;
        Prefix = prefix ?? string.Empty;
        DisplayText = displayText ?? string.Empty;
        Format = format ?? new SuggestionFormat();
        Item = item;
    }

    internal SuggestionToken(int startIndex, byte[] imageData, string contentType = null, string alternativeText = "", double widthRequest = -1, double heightRequest = -1, object item = null)
    {
        Kind = SuggestingBoxTokenKind.Image;
        StartIndex = startIndex;
        ImageData = imageData ?? [];
        ContentType = ImageContentTypeDetector.Resolve(imageData, contentType);
        AlternativeText = alternativeText ?? string.Empty;
        WidthRequest = widthRequest;
        HeightRequest = heightRequest;
        Item = item;
    }

    internal SuggestionToken(SuggestingBoxTokenInfo tokenInfo)
    {
        Kind = tokenInfo.Kind;
        StartIndex = tokenInfo.StartIndex;
        Prefix = tokenInfo.Prefix ?? string.Empty;
        DisplayText = tokenInfo.DisplayText ?? string.Empty;
        Format = tokenInfo.Format ?? new SuggestionFormat();
        Item = tokenInfo.Item;
        ImageData = tokenInfo.ImageData ?? [];
        ContentType = ImageContentTypeDetector.Resolve(ImageData, tokenInfo.ContentType);
        AlternativeText = tokenInfo.AlternativeText ?? string.Empty;
        WidthRequest = tokenInfo.WidthRequest;
        HeightRequest = tokenInfo.HeightRequest;
    }

    public SuggestingBoxTokenKind Kind { get; }
    public int StartIndex { get; set; }
    public string Prefix { get; } = string.Empty;
    public string DisplayText { get; } = string.Empty;
    public SuggestionFormat Format { get; } = new();
    public object Item { get; }
    public byte[] ImageData { get; } = [];
    public string ContentType { get; } = ImageContentTypeDetector.DefaultContentType;
    public string AlternativeText { get; } = string.Empty;
    public double WidthRequest { get; } = -1;
    public double HeightRequest { get; } = -1;
    public string FullText => Kind == SuggestingBoxTokenKind.Image
        ? SuggestingBoxText.ImagePlaceholderString
        : Prefix + DisplayText;
    public int Length => FullText.Length;
    public int EndIndex => StartIndex + Length;
    public bool IsImage => Kind == SuggestingBoxTokenKind.Image;
    public bool IsMention => Kind == SuggestingBoxTokenKind.Mention;

    public SuggestingBoxTokenInfo ToInfo() =>
        new()
        {
            Kind = Kind,
            StartIndex = StartIndex,
            Prefix = Prefix,
            DisplayText = DisplayText,
            Format = new SuggestionFormat
            {
                BackgroundColor = Format.BackgroundColor,
                ForegroundColor = Format.ForegroundColor,
                Bold = Format.Bold
            },
            Item = Item,
            ImageData = ImageData,
            ContentType = ContentType,
            AlternativeText = AlternativeText,
            WidthRequest = WidthRequest,
            HeightRequest = HeightRequest
        };
}
