namespace ReceptyOks.Configuration;

/// <summary>
/// Photo picker options that downscale and compress images on the device, keeping synced payloads
/// (and Azure SQL writes) small: ~150-300 KB instead of multi-MB originals.
/// </summary>
public static class DefaultAppPhotoOptions
{
    private const int PhotoMaxDimension = 1280;
    private const int PhotoMinDimension = 320;
    private const int PhotoCompressionQuality = 75;

    public static MediaPickerOptions CreatePhotoOptions(
        string title,
        int maxDimension = PhotoMaxDimension,
        int selectionLimit = 1)
    {
        var dimension = Math.Clamp(maxDimension, PhotoMinDimension, PhotoMaxDimension);

        return new()
        {
            Title = title,
            MaximumWidth = dimension,
            MaximumHeight = dimension,
            CompressionQuality = PhotoCompressionQuality,
            RotateImage = true,
            SelectionLimit = selectionLimit
        };
    }
}
