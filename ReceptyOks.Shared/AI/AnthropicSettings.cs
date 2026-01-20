
namespace ReceptyOks.Shared.AI
{
    public class AnthropicSettings
    {

        /// <summary>
        /// Base URL for Anthropic API. Default is the public Anthropic endpoint.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.anthropic.com";

        /// <summary>
        /// Model to use (e.g. "claude-2", "claude-instant"). Match to what you have access to.
        /// </summary>
        public string Model { get; set; } = "";
        /// <summary>
        /// Maximum model tokens to request (model-specific limits apply).
        /// </summary>
        public int MaxTokens { get; set; } = 4000;
    }
}
