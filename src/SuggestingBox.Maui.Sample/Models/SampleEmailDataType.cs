namespace SuggestingBox.Maui.Sample.Models;

public class SampleEmailDataType(string displayName, string email)
{
    public string DisplayName { get; set; } = displayName;
    public string Email { get; set; } = email;

    public override string ToString() => DisplayName;
}
