namespace ReceptyOks.Shared.OCR
{
    public abstract class OCSServiceBase : IOCRService
    {
        public virtual bool IsSupported => true;

        public virtual Task<OCRResult> RecognizeTextAsync(Stream imageStream)
        {
            throw new NotImplementedException("This method should be implemented in derived classes.");
        }

        public virtual Task<OCRResult> RecognizeTextAsync(byte[] imageData)
        {
            using var stream = new MemoryStream(imageData);
            return RecognizeTextAsync(stream);
        }

        public virtual Task<OCRResult> ScanRecipeFromCameraAsync()
        {
            throw new NotImplementedException("This method should be implemented in derived classes.");
        }

        public virtual Task<OCRResult> ScanRecipeFromGalleryAsync()
        {
            throw new NotImplementedException("This method should be implemented in derived classes.");
        }
    }
}
