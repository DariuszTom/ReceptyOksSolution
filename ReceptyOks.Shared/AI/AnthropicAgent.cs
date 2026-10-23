using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;

namespace ReceptyOks.Shared.AI
{
    public class AnthropicAgent : IDisposable
    {
        private AnthropicSettings _settings;
        private byte[] _apiToken;
        private readonly HttpClient? _httpClient;

        public AnthropicAgent(AnthropicSettings settings, byte[] apiTok)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _apiToken = apiTok ?? throw new ArgumentNullException(nameof(apiTok));
        }

        public AnthropicAgent(AnthropicSettings settings, HttpClient httpClient)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiToken = Encoding.UTF8.GetBytes("proxy");
        }

        public IChatClient GetAgent()
        {
            var apiKey = Encoding.UTF8.GetString(_apiToken);
            var options = new ClientOptions
            {
                ApiKey = apiKey,
                BaseUrl = _settings.BaseUrl,
            };
            if (_httpClient is not null)
            {
                options = options with { HttpClient = _httpClient };
            }
            AnthropicClient client = new(options);
            IChatClient chatClient = client.AsIChatClient(_settings.Model, _settings.MaxTokens);
            return chatClient;
        }
        public void Dispose()
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(_apiToken);
        }
    }
}
