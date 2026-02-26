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
        new("댕댕3이 3"),
        new("멍멍4이 4"),
        new("Azure"),
        new("TypeScript")
    ];

    private readonly List<SampleEmailDataType> emails =
    [
        new("John Doe", "john@example.com"),
        new("Jane Smith", "jane@example.com"),
        new("Bob Johnson", "bob@example.com"),
        new("Alice Williams", "alice@example.com"),
        new("이호2원 2", "hoyo321@naver.com"),
        new("박성현", "ayanoquasar@naver.com"),
        new("Charlie Brown", "charlie@example.com")
    ];

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
}
