# Migration Guide

## v1 to v2

SuggestingBox.Maui v2 changes image paste handling from notification-only events to explicit inline image tokens. Mentions and images now share the same token model returned by `GetTokens()` and restored by `SetContent(...)`.

### Image paste events

`ImageInserted` and `ImageInsertedCommand` were replaced with `ImagePasteRequested` and `ImagePasteRequestedCommand`.

v1:

```csharp
SuggestingBoxControl.ImageInserted += (sender, args) =>
{
    PastedImage.Source = ImageSource.FromStream(() => new MemoryStream(args.ImageData));
};
```

v2:

```csharp
SuggestingBoxControl.ImagePasteRequested += (sender, args) =>
{
    // Validate, resize, upload, or reject the image here.
    args.AlternativeText = "Pasted image";
    args.WidthRequest = 180;
    args.InsertImageImmediately = true;
};
```

If the handler does not set `InsertImageImmediately` or call `InsertImageToken(...)`, the pasted image is not inserted into the editor.

### Token data

`SuggestingBoxTokenInfo` now includes `Kind`.

- `Kind == SuggestingBoxTokenKind.Mention` represents the existing mention/token behavior.
- `Kind == SuggestingBoxTokenKind.Image` represents an inline image token.
- Image tokens occupy one character in `Text`: `\uFFFC`.
- `Length` and `EndIndex` should be used instead of manually calculating ranges from `Prefix.Length + DisplayText.Length`.

v1:

```csharp
foreach (var token in SuggestingBoxControl.GetTokens())
{
    Console.WriteLine($"{token.Prefix}{token.DisplayText}");
}
```

v2:

```csharp
foreach (var token in SuggestingBoxControl.GetTokens())
{
    Console.WriteLine(token.Kind == SuggestingBoxTokenKind.Image
        ? $"image ({token.ContentType})"
        : $"{token.Prefix}{token.DisplayText}");
}
```

### Restoring content

`SetContent(string text, IEnumerable<SuggestingBoxTokenInfo> tokens)` is still the restore API. For image tokens, the text should contain `\uFFFC` at the image token position. If it is missing, v2 inserts it during restore.

### Image content type and size

`InsertImageToken(...)` accepts an optional `contentType`. When omitted, the library detects common image types from the image bytes.

If both `widthRequest` and `heightRequest` are `-1`, the original image size is used. If only one value is provided, the other is inferred from the original aspect ratio.
