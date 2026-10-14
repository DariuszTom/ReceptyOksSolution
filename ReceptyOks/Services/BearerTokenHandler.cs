using System.Net.Http.Headers;

namespace ReceptyOks.Services
{
    /// <summary>
    /// Attaches <c>Authorization: Bearer &lt;jwt&gt;</c> to outgoing requests when a valid
    /// token is stored. Use alongside <see cref="ApiKeyHandler"/> for endpoints that are
    /// owner-scoped (require a JWT in addition to the shared API key).
    /// </summary>
    public class BearerTokenHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is null)
            {
                var token = await AccessTokenStore.GetValidTokenAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
