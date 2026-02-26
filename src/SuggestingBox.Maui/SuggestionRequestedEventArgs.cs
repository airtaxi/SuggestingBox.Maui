namespace SuggestingBox.Maui;

public class SuggestionRequestedEventArgs(string prefix, string queryText) : EventArgs
{
    public string Prefix { get; } = prefix;
    public string QueryText { get; } = queryText;
}
