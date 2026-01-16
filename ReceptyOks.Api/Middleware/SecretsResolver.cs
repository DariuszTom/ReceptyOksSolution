using Azure.Identity;

namespace ReceptyOks.Api.Middleware
{
    internal sealed class SecretsResolver
    {
        private readonly WebApplicationBuilder _builder;
        public SecretsResolver(WebApplicationBuilder builder)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            _builder = builder;
        }
        public void ResolveSecrets()
        {
            // Optional: set KeyVault:VaultUri in appsettings or environment (KeyVault__VaultUri)
            var keyVaultUri = _builder.Configuration["KeyVault:VaultUri"];
            if (!string.IsNullOrWhiteSpace(keyVaultUri))
            {
                // DefaultAzureCredential uses managed identity in Azure or developer creds locally
                _builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
                // Startup validation: if KeyVault is configured require ApiAuth:PasswordHash and validate formats
                string? passwordHash = _builder.Configuration["PasswordHash"];
                string? secretKey = _builder.Configuration["SecretKey"];

                bool IsBase64(string s)
                {
                    try
                    {
                        var b = Convert.FromBase64String(s);
                        return b.Length > 0;
                    }
                    catch { return false; }
                }

                bool IsHex(string s)
                {
                    try
                    {
                        var hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
                        var bytes = Convert.FromHexString(hex);
                        return bytes.Length > 0;
                    }
                    catch { return false; }
                }

                bool ValidateSecretFormat(string s) => IsBase64(s) || IsHex(s);

                if (string.IsNullOrWhiteSpace(passwordHash))
                {
                    Console.Error.WriteLine("FATAL: KeyVault is configured but 'ApiAuth:PasswordHash' is missing. Add secret 'ApiAuth--PasswordHash' to Key Vault.");
                    Environment.Exit(1);
                }

                if (!ValidateSecretFormat(passwordHash))
                {
                    Console.Error.WriteLine("FATAL: 'ApiAuth:PasswordHash' exists but is not valid Base64 or hex. Store the PasswordHash as Base64(HMAC_SHA256) or hex.");
                    Environment.Exit(1);
                }

                if (!string.IsNullOrWhiteSpace(secretKey) && !ValidateSecretFormat(secretKey))
                {
                    Console.Error.WriteLine("FATAL: 'ApiAuth:SecretKey' exists but is not valid Base64 or hex. Store SecretKey as Base64 (recommended) or hex.");
                    Environment.Exit(1);
                }

            }
        }
    }
}
