namespace SuggestingBox.Maui;

internal class SuggestionToken(int startIndex, string prefix, string displayText, SuggestionFormat format, object item = null)
{
    public int StartIndex { get; set; } = startIndex;
    public string Prefix { get; } = prefix;
    public string DisplayText { get; } = displayText;
    public SuggestionFormat Format { get; } = format;
    public object Item { get; } = item;
    public string FullText => Prefix + DisplayText;
    public int Length => FullText.Length;
    public int EndIndex => StartIndex + Length;
}
