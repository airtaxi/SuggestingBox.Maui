namespace SuggestingBox.Maui;

public class SuggestionChosenEventArgs(string prefix, object selectedItem) : EventArgs
{
    public string Prefix { get; } = prefix;
    public object SelectedItem { get; } = selectedItem;
    public SuggestionFormat Format { get; } = new();
    public string DisplayText { get; set; } = string.Empty;
}
