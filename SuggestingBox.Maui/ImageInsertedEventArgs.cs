namespace SuggestingBox.Maui;

public class ImageInsertedEventArgs(byte[] imageData) : EventArgs
{
    public byte[] ImageData { get; } = imageData;
}
