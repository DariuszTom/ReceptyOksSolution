using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Services;
using ReceptyOks.Shared;

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

        public LoginViewModel(BackendAuthService backendAuth)
        {
            _backendAuth = backendAuth;
        }

        [RelayCommand]
        private async Task InitializeAsync()
        {
            IsBusy = true;

            try
            {
                var hasValidSecret = await _backendAuth.IsCorrectSecretStored().ConfigureAwait(false);

                if (hasValidSecret)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("//main");
                    });
                }
            }
            catch
            {
                // Secret not stored or invalid - user needs to login
            }
            finally
            {
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

            try
            {
                // Send plaintext secret to server (server will perform HMAC or compare as configured).
                var secretBytes = Encoding.UTF8.GetBytes(Secret);
                var result = await _backendAuth.IsApiKeyValid(secretBytes).ConfigureAwait(false);

                if (result.IsValid)
                {
                    await SecureSecretService.SaveAsync(GlobalConstants.ApiKeyHeaderName, secretBytes).ConfigureAwait(false);
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("//main");
                    });
                }
                else
                {
                    ErrorMessage = result.Message ?? "Nieprawidłowy sekret";
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
            }
        }
    }
}
