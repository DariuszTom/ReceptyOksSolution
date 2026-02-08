#if WINDOWS
using ReceptyOks.Shared.OCR;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ReceptyOks.Platforms.Windows
{
    internal class WindowsOCR : OCSServiceBase
    {
        public override async Task<OCRResult> RecognizeTextAsync(Stream imageStream)
        {
            try
            {
                var randiomAccessStream = new InMemoryRandomAccessStream();
                await imageStream.CopyToAsync(randiomAccessStream.AsStreamForWrite());
                randiomAccessStream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(randiomAccessStream);
                var bitmap = await decoder.GetSoftwareBitmapAsync();

                var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (ocrEngine == null)
                {
                    return new OCRResult
                    {
                        Success = false,
                        ErrorMessage = "OCR engine could not be created for the user's profile languages."
                    };
                }
                var ocrResult = await ocrEngine.RecognizeAsync(bitmap);
                var text = string.Join("\n", ocrResult.Lines.Select(line => line.Text));
                return new OCRResult
                {
                    Success = true,
                    Text = text
                };
            }
            catch (Exception ex)
            {
                return new OCRResult
                {
                    Success = false,
                    ErrorMessage = $"An error occurred during OCR processing: {ex.Message}"
                };
            }


        }
    }
}
#endif