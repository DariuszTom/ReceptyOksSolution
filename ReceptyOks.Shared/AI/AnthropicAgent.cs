using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;

namespace ReceptyOks.Shared.AI
{
    public class AnthropicAgent : IDisposable
    {
        private AnthropicSettings _settings;
        private byte[] _apiToken;
        public AnthropicAgent(AnthropicSettings settings, byte[] apiTok)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _apiToken = apiTok ?? throw new ArgumentNullException(nameof(apiTok));
        }
        public IChatClient GetAgent()
        {
            var apiKey = Encoding.UTF8.GetString(_apiToken);
            var options = new ClientOptions
            {
                ApiKey = apiKey,
                BaseUrl = _settings.BaseUrl,
            };
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
