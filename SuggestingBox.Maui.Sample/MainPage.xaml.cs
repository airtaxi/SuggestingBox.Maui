using SuggestingBox.Maui.Sample.Models;

namespace SuggestingBox.Maui.Sample;

public partial class MainPage : ContentPage
{
    private readonly List<SampleDataType> hashtags =
    [
        new("CSharp"),
        new("MAUI"),
        new("DotNet"),
        new("Xamarin"),
        new("Blazor"),
        new("ASPNET"),
        new("EntityFramework"),
        new("WinUI"),
        new("댕댕이"),
        new("멍멍이"),
        new("Azure"),
        new("TypeScript")
    ];

    private readonly List<SampleEmailDataType> emails =
    [
        new("John Doe", "john@example.com"),
        new("Jane Smith", "jane@example.com"),
        new("Bob Johnson", "bob@example.com"),
        new("Alice Williams", "alice@example.com"),
        new("이호원", "hoyo321@naver.com"),
        new("Charlie Brown", "charlie@example.com")
    ];

    private string savedText;
    private List<SuggestingBoxTokenInfo> savedTokens;

    public MainPage()
    {
        InitializeComponent();
        SuggestingBoxControl.SuggestionRequested += OnSuggestionRequested;
        SuggestingBoxControl.SuggestionChosen += OnSuggestingBoxSuggestionChosen;
    }

    private void OnSuggestingBoxSuggestionChosen(SuggestingBox sender, SuggestionChosenEventArgs args)
    {
        if (args.Prefix == "#")
        {
            args.Format.BackgroundColor = Colors.LightSlateGray;
            args.Format.ForegroundColor = Colors.White;
            args.Format.Bold = FormatEffect.On;
            args.DisplayText = ((SampleDataType)args.SelectedItem).Text;
        }
        else
        {
            args.DisplayText = ((SampleEmailDataType)args.SelectedItem).DisplayName;
        }
    }

    private void OnSuggestionRequested(SuggestingBox sender, SuggestionRequestedEventArgs args)
    {
        sender.ItemsSource = args.Prefix == "#"
            ? hashtags.Where(x => x.Text.Contains(args.QueryText, StringComparison.OrdinalIgnoreCase))
            : emails.Where(x => x.DisplayName.Contains(args.QueryText, StringComparison.OrdinalIgnoreCase));
    }

    private void OnRefreshTokensClicked(object sender, EventArgs eventArgs)
    {
        var tokenInfos = SuggestingBoxControl.GetTokens();
        TokenListView.ItemsSource = tokenInfos
            .Select(token =>
                $"[{token.StartIndex}..{token.StartIndex + token.Prefix.Length + token.DisplayText.Length}] " +
                $"{token.Prefix}{token.DisplayText}")
            .ToList();
    }

    private void OnSaveClicked(object sender, EventArgs eventArgs)
    {
        savedText = SuggestingBoxControl.Text;
        savedTokens = SuggestingBoxControl.GetTokens().ToList();
        RestoreButton.IsEnabled = true;
        SaveStatusLabel.Text = $"Saved: \"{savedText}\" with {savedTokens.Count} token(s)";
        SaveStatusLabel.TextColor = Colors.Green;
    }

    private void OnRestoreClicked(object sender, EventArgs eventArgs)
    {
        if (savedTokens is null) return;
        SuggestingBoxControl.SetContent(savedText, savedTokens);
        SaveStatusLabel.Text = "Restored!";
        SaveStatusLabel.TextColor = Colors.Blue;
    }
}
