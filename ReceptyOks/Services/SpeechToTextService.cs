using CommunityToolkit.Maui.Media;
using System.Globalization;

namespace ReceptyOks.Services
{
    internal class SpeechToTextService : IDisposable
    {
        private readonly ISpeechToText _speechToText;
        private bool disposedValue;

        // Events ViewModels subscribe to
        internal event Action<string>? RecognitionUpdated;
        internal event Action<string>? RecognitionCompleted;

        internal SpeechToTextService(ISpeechToText speechToText)
        {
            _speechToText = speechToText ?? throw new ArgumentNullException(nameof(speechToText));
        }

        internal async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            if (disposedValue) return;

            bool isGranted;
            try
            {
                isGranted = await _speechToText.RequestPermissions(cancellationToken);
            }
            catch (FileNotFoundException)
            {
                // On Windows unpackaged apps, AppxManifest.xml doesn't exist.
                isGranted = true;
            }

            if (!isGranted)
            {
                await SnackBarHelper.ShowErrorSnackbarAsync("Permission not granted");
                return;
            }

            _speechToText.RecognitionResultUpdated += OnRecognitionTextUpdated;
            _speechToText.RecognitionResultCompleted += OnRecognitionTextCompleted;

            try
            {
                await _speechToText.StartListenAsync(new SpeechToTextOptions
                {
                    Culture = CultureInfo.CurrentCulture,
                    ShouldReportPartialResults = true
                }, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                // On Windows, SpeechRecognizer may fail if speech recognition is not installed or configured.
                Unsubscribe();
                await SnackBarHelper.ShowErrorSnackbarAsync(
                 "Rozpoznawanie mowy nie jest dostępne. Upewnij się, że pakiet językowy rozpoznawania mowy jest zainstalowany w systemie Windows.");
                throw;
            }
        }

        internal async Task StopListeningAsync(CancellationToken cancellationToken)
        {
            await _speechToText.StopListenAsync(cancellationToken);
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            _speechToText?.RecognitionResultUpdated -= OnRecognitionTextUpdated;
            _speechToText?.RecognitionResultCompleted -= OnRecognitionTextCompleted;
        }

        private void OnRecognitionTextUpdated(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs args)
        {
            RecognitionUpdated?.Invoke(args.RecognitionResult);
        }

        private void OnRecognitionTextCompleted(object? sender, SpeechToTextRecognitionResultCompletedEventArgs args)
        {
            if (args.RecognitionResult.IsSuccessful)
            {
                RecognitionCompleted?.Invoke(args.RecognitionResult.Text);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Unsubscribe();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
