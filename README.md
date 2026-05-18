# SuggestingBox.Maui

[![NuGet](https://img.shields.io/nuget/v/SuggestingBox.Maui.svg)](https://www.nuget.org/packages/SuggestingBox.Maui) [![NuGet Downloads](https://img.shields.io/nuget/dt/SuggestingBox.Maui.svg)](https://www.nuget.org/packages/SuggestingBox.Maui)

🌐 [한국어](README.ko.md)

Migrating from v1? See the [migration guide](MIGRATION.md).

A .NET MAUI control that provides inline mention/tag suggestion functionality with formatted tokens. Type a prefix character (e.g., `@` or `#`) and get a popup suggestion list — selected items become styled, immutable tokens embedded in the editor.

## 📷 Showcase

![Showcase](https://raw.githubusercontent.com/airtaxi/SuggestingBox.Maui/master/Gallery/Showcase.png)

## Features

- **Prefix-based suggestions** — Configure one or more trigger characters (e.g., `@`, `#`) to activate the suggestion popup.
- **Formatted tokens** — Chosen suggestions become styled tokens with customizable background color, foreground color, and bold formatting.
- **Atomic token behavior** — Tokens are immutable units; cursor navigation skips over them and deletion removes the entire token.
- **Token extraction & restoration** — Use `GetTokens()` and `SetContent()` to serialize/deserialize editor state including tokens.
- **Image paste requests** — Receive `ImagePasteRequested` events and explicitly choose whether to insert pasted images as inline tokens.
- **Cross-platform** — Supports Android, iOS, macOS Catalyst, and Windows.

## Supported Platforms

| Platform | Minimum Version |
|---|---|
| Android | 21.0 |
| iOS | 15.0 |
| macOS Catalyst | 15.0 |
| Windows | 10.0.17763.0 |

## Getting Started

### 1. Register the handler

In your `MauiProgram.cs`:

```csharp
builder.UseMauiApp<App>()
       .UseSuggestingBox();
```

### 2. Add to XAML

```xml
xmlns:suggestingBox="clr-namespace:SuggestingBox.Maui;assembly=SuggestingBox.Maui"

<suggestingBox:SuggestingBox
    x:Name="SuggestingBoxControl"
    Prefixes="@#"
    Placeholder="Type @ or # to mention..."
    MaxSuggestionHeight="200">
    <suggestingBox:SuggestingBox.ItemTemplate>
        <DataTemplate>
            <VerticalStackLayout Padding="8,4">
                <Label Text="{Binding .}" />
            </VerticalStackLayout>
        </DataTemplate>
    </suggestingBox:SuggestingBox.ItemTemplate>
</suggestingBox:SuggestingBox>
```

### 3. Handle events

```csharp
SuggestingBoxControl.SuggestionRequested += (sender, args) =>
{
    sender.ItemsSource = args.Prefix == "#"
        ? hashtags.Where(x => x.Text.Contains(args.QueryText, StringComparison.OrdinalIgnoreCase))
        : emails.Where(x => x.Name.Contains(args.QueryText, StringComparison.OrdinalIgnoreCase));
};

SuggestingBoxControl.SuggestionChosen += (sender, args) =>
{
    args.DisplayText = args.SelectedItem.ToString();
    args.Format.BackgroundColor = Colors.LightSlateGray;
    args.Format.ForegroundColor = Colors.White;
    args.Format.Bold = FormatEffect.On;
};
```

## API Reference

### Properties

| Property | Type | Description |
|---|---|---|
| `Prefixes` | `string` | Characters that trigger the suggestion popup (e.g., `"@#"`). |
| `Text` | `string` | The plain text content of the editor (two-way bindable). |
| `ItemsSource` | `IEnumerable` | The suggestion items to display in the popup. |
| `ItemTemplate` | `DataTemplate` | Template for rendering suggestion items. |
| `Placeholder` | `string` | Placeholder text shown when the editor is empty. |
| `MaxSuggestionHeight` | `double` | Maximum height of the suggestion popup (default: `200`). |

### Events

| Event | Args | Description |
|---|---|---|
| `SuggestionRequested` | `SuggestionRequestedEventArgs` | Fired when a prefix is detected. Use to filter and set `ItemsSource`. |
| `SuggestionChosen` | `SuggestionChosenEventArgs` | Fired when a suggestion is selected. Set `DisplayText` and `Format`. |
| `ImagePasteRequested` | `ImagePasteRequestedEventArgs` | Fired when an image is pasted. Set `InsertImageImmediately = true` or call `InsertImageToken(...)` to insert it. |

### Methods

| Method | Description |
|---|---|
| `GetTokens()` | Returns `IReadOnlyList<SuggestingBoxTokenInfo>` of current tokens. |
| `SetContent(string text, IEnumerable<SuggestingBoxTokenInfo> tokens)` | Restores the editor with the given text and tokens. |
| `InsertImageToken(byte[] imageData, string contentType = null, string alternativeText = "", double widthRequest = -1, double heightRequest = -1, object item = null)` | Inserts an image token at the current cursor position. If both size values are `-1`, the original image size is used. |
| `InsertImageToken(int startIndex, byte[] imageData, string contentType = null, string alternativeText = "", double widthRequest = -1, double heightRequest = -1, object item = null)` | Inserts an image token at the specified text position. If one size value is set, the other is inferred from the original aspect ratio. |

#### Example: Extracting tokens with `GetTokens()`

```csharp
var tokens = SuggestingBoxControl.GetTokens();
foreach (var token in tokens)
{
    Console.WriteLine(token.Kind == SuggestingBoxTokenKind.Image
        ? $"[{token.StartIndex}..{token.EndIndex}] image ({token.ContentType})"
        : $"[{token.StartIndex}..{token.EndIndex}] {token.Prefix}{token.DisplayText}");
}
```

#### Example: Inserting pasted images explicitly

```csharp
SuggestingBoxControl.ImagePasteRequested += (sender, args) =>
{
    // Validate, resize, upload, or reject the image here.
    args.AlternativeText = "Pasted image";
    args.WidthRequest = 180;
    args.InsertImageImmediately = true;
};
```

#### Example: Saving and restoring with `SetContent()`

```csharp
// Save the current editor state
var savedText = SuggestingBoxControl.Text;
var savedTokens = SuggestingBoxControl.GetTokens().ToList();

// Restore the editor state later
SuggestingBoxControl.SetContent(savedText, savedTokens);
```

### SuggestionFormat

| Property | Type | Default |
|---|---|---|
| `BackgroundColor` | `Color` | `Colors.Transparent` |
| `ForegroundColor` | `Color` | `Colors.Black` |
| `Bold` | `FormatEffect` | `FormatEffect.Off` |

## Contributing

Pull requests are welcome! If you have ideas for improvements or find a bug, feel free to open an issue or submit a PR.

## Acknowledgements

This project was built with the help of [GitHub Copilot](https://github.com/features/copilot).

Inspired by and grateful to the following projects:

- [SpeakLink](https://github.com/engagesolutionsgroup/SpeakLink) — A .NET MAUI mention editor that served as a key reference for inline mention functionality.
- [Windows Community Toolkit](https://github.com/CommunityToolkit/Windows) — Provided valuable insights for Windows platform implementation.

## License

This project is licensed under the [MIT License](LICENSE.txt).

## Author

**Howon Lee** ([@airtaxi](https://github.com/airtaxi))
