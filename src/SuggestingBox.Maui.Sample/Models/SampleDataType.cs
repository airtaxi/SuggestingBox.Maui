namespace SuggestingBox.Maui.Sample.Models;

public class SampleDataType(string text)
{
    public string Text { get; set; } = text;

    public override string ToString() => Text;
}
