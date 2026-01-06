namespace ReceptyOks.Shared.OCR
{
    public interface IOCRService
    {
        Task<OCRResult> RecognizeTextAsync(Stream imageStream);
        Task<OCRResult> RecognizeTextAsync(byte[] imageData);
        Task<OCRResult> ScanRecipeFromCameraAsync();
        Task<OCRResult> ScanRecipeFromGalleryAsync();
        bool IsSupported { get; }
    }
}
