using AsyncAwaitBestPractices;
using ReceptyOks.Interfaces;
using ReceptyOks.Services;
using ReceptyOks.Shared;
using System.Security.Cryptography;
using System.Text;


namespace ReceptyOks.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly BackendAuthService _backendAuth;

        [ObservableProperty]
        private string _secret = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;
        private ISyncService _sync;

        public LoginViewModel(BackendAuthService backendAuth, ISyncService sync)
        {
            _backendAuth = backendAuth;
            _sync = sync;
        }

        [RelayCommand]
        private async Task InitializeAsync()
        {
            IsBusy = true;

            byte[]? bytes = null;
            try
            {
                bytes = await SecureSecretService.GetSecretBytesAsync(GlobalConstants.ApiKeyHeaderName).ConfigureAwait(false);
                if (bytes is null || bytes.Length == 0)
                {
                    return;
                }

                using var cts = new CancellationTokenSource(GlobalConstants.AutoLoginValidationTimeout);
                var (outcome, _) = await _backendAuth.TryValidateAsync(bytes, cts.Token).ConfigureAwait(false);

                if (outcome != AuthOutcome.Valid)
                {
                    // Not valid, or server unreachable within the timeout: stay on the login
                    // page. The user can press the login button and take the Unreachable
                    // offline-fallback path if the API stays cold.
                    return;
                }

                _sync?.FullSyncAsync().SafeFireAndForget();

                // Yield off the LoginPage.Appearing frame before navigating - otherwise
                // WinUI Shell crashes when the current root navigates during its own
                // Appearing handler.
                await Task.Yield();
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.GoToAsync("//RecipesPage");
                });
            }
            catch
            {
                // Secret not stored or transient failure - user stays on login page.
            }
            finally
            {
                SecureSecretService.Clear(bytes);
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Secret))
            {
                ErrorMessage = "Proszę wprowadzić sekret";
                HasError = true;
                return;
            }

            HasError = false;
            IsBusy = true;

            byte[]? secretBytes = null;
            try
            {
                // Send plaintext secret to server (server will perform HMAC or compare as configured).
                secretBytes = Encoding.UTF8.GetBytes(Secret);
                var (outcome, response) = await _backendAuth.TryValidateAsync(secretBytes).ConfigureAwait(false);

                if (outcome == AuthOutcome.Valid)
                {
                    await SecureSecretService.SaveAsync(GlobalConstants.ApiKeyHeaderName, secretBytes).ConfigureAwait(false);
                    _sync?.FullSyncAsync().SafeFireAndForget();
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("//RecipesPage");
                    });
                }
                else if (outcome == AuthOutcome.Unreachable
                         && await MatchesStoredSecretAsync(secretBytes).ConfigureAwait(false))
                {
                    // Server unreachable (e.g. Container App cold-start) but the user typed the
                    // same secret they previously used successfully - allow offline entry so
                    // local SQLite features remain usable. Sync will retry when the API is up.
                    _sync?.FullSyncAsync().SafeFireAndForget();
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("//RecipesPage");
                    });
                }
                else
                {
                    ErrorMessage = response.Message ?? "Nieprawidłowy sekret";
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Błąd połączenia: {ex.Message}";
                HasError = true;
            }
            finally
            {
                IsBusy = false;
                Secret = string.Empty;
                if (secretBytes != null)
                {
                    CryptographicOperations.ZeroMemory(secretBytes);
                }
            }
        }

        private static async Task<bool> MatchesStoredSecretAsync(byte[] typed)
        {
            var stored = await SecureSecretService.GetSecretBytesAsync(GlobalConstants.ApiKeyHeaderName).ConfigureAwait(false);
            try
            {
                if (stored is null || stored.Length == 0 || stored.Length != typed.Length)
                {
                    return false;
                }
                return CryptographicOperations.FixedTimeEquals(stored, typed);
            }
            finally
            {
                SecureSecretService.Clear(stored);
            }
        }
    }
}
