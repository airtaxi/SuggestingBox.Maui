namespace SuggestingBox.Maui;

public class SuggestingBoxTokenInfo(int startIndex, string prefix, string displayText, SuggestionFormat format)
{
    public int StartIndex { get; set; } = startIndex;
    public string Prefix { get; } = prefix;
    public string DisplayText { get; } = displayText;
    public SuggestionFormat Format { get; } = format;
}
