namespace SuggestingBox.Maui;

public class ImagePasteRequestedEventArgs(byte[] imageData, string contentType, int cursorPosition) : EventArgs
{
    public byte[] ImageData { get; } = imageData;
    public string ContentType { get; } = contentType;
    public int CursorPosition { get; } = cursorPosition;
    public string AlternativeText { get; set; } = string.Empty;
    public double WidthRequest { get; set; } = -1;
    public double HeightRequest { get; set; } = -1;
    public object Item { get; set; }
    public string Tag { get; set; } = string.Empty;
    public bool InsertImageImmediately { get; set; }
}
