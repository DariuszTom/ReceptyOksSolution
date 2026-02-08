using ReceptyOks.Shared;
using System.Text;

namespace ReceptyOks.Services
{

    public class ApiKeyHandler : DelegatingHandler
    {
        private readonly string _secretStorageKey;

        // opcjonalnie przyjmij nazwę klucza w SecureStorage przez DI
        public ApiKeyHandler(string secretStorageKey = GlobalConstants.ApiKeyHeaderName)
        {
            _secretStorageKey = secretStorageKey;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Pobierz tajny klucz z SecureStorage (możesz zamiast tego użyć innego serwisu/DI)
            var bytes = await SecureSecretService.GetSecretBytesAsync(_secretStorageKey).ConfigureAwait(false);

            if (bytes != null && bytes.Length > 0)
            {
                var apiKey = Encoding.UTF8.GetString(bytes);
                if (!string.IsNullOrEmpty(apiKey))
                {
                    // Dodaj nagłówek tylko dla tego requestu
                    request.Headers.Remove(GlobalConstants.ApiKeyHeaderName);
                    request.Headers.Add(GlobalConstants.ApiKeyHeaderName, apiKey);
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
