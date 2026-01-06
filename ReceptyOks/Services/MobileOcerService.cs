using ReceptyOks.Shared.OCR;
using Plugin.Maui.OCR;

namespace ReceptyOks.Services
{
    internal class MobileOcerService:OCSServiceBase
    {
        private readonly IOcrService _ocrPlugin;
        public MobileOcerService()
        {
            _ocrPlugin = OcrPlugin.Default;
        }
        public override async Task<OCRResult> RecognizeTextAsync(byte[] imageData)
        {
            try 
            {
                var ocrResult = await _ocrPlugin.RecognizeTextAsync(imageData);
                return new OCRResult
                {
                    Success = ocrResult.Success,
                    Text = ocrResult.AllText ?? string.Empty,
                    ErrorMessage = ocrResult.Success ? null : "OCR failed"
                };
            }
            catch (Exception ex)
            {
                return new OCRResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        public override async Task<OCRResult> ScanRecipeFromCameraAsync()
        {
            try
            {
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                    if (cameraStatus != PermissionStatus.Granted)
                    {
                        return new OCRResult
                        {
                            Success = false,
                            ErrorMessage = "Camera permission denied."
                        };
                    }
                }

                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    return new OCRResult
                    {
                        Success = false,
                        ErrorMessage = "Camera capture is not supported on this device."
                    };
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo == null)
                {
                    return new OCRResult
                    {
                        Success = false,
                        ErrorMessage = "No photo was captured."
                    };
                }

                using var stream = await photo.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                return await RecognizeTextAsync(imageData);
            }
            catch (Exception ex)
            {
                return new OCRResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public override async Task<OCRResult> ScanRecipeFromGalleryAsync()
        {
            try
            {
                var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
                {
                    Title = "Wybierz zdjęcie przepisu",
                    SelectionLimit = 1
                });

                var photo = photos?.FirstOrDefault();

                if (photo == null)
                {
                    return new OCRResult
                    {
                        Success = false,
                        ErrorMessage = "No photo was selected."
                    };
                }

                using var stream = await photo.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                return await RecognizeTextAsync(imageData);
            }
            catch (Exception ex)
            {
                return new OCRResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
