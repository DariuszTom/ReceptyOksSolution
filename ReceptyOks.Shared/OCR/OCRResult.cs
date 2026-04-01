namespace ReceptyOks.Shared.OCR
{
    public class OCRResult
    {
        public bool Success { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; } = string.Empty;

    }
}
