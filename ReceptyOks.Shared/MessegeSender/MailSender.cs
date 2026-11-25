using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SharedLibrary.Misc.MessegeSender
{
    public class MailSender : IMailSender, IDisposable
    {
        #region Fields
        private string? _host;
        private int _port;
        private NetworkCredential? _credentials;
        private bool _IsBodyHtml;
        private MimeMessage? _mailMessage;
        private string? _MyMail;
        #endregion
        #region Properiets
        public bool IsBodyHtml { get => _IsBodyHtml; set => _IsBodyHtml = value; }
        #endregion
        public Task MailConfig(string addres, int port, SecureString pw, string login)
        {
            if (IsMailValid(login) == false)
                throw new ArgumentException("Invalid login email address", nameof(login));

            _MyMail = login;
            _host = addres;
            _port = port;
            _credentials = new NetworkCredential(login, pw);
            return Task.CompletedTask;
        }
        public void CreateMail(string subject, StringBuilder body)
        {
            if (_MyMail is null) throw new InvalidOperationException("Mail sender is not configured. Call MailConfig first.");
            _mailMessage = new MimeMessage
            {
                Subject = subject,
                Body = new TextPart(_IsBodyHtml ? "html" : "plain") { Text = body.ToString() }
            };
            _mailMessage.From.Add(MailboxAddress.Parse(_MyMail));
        }
        public async Task SendMail(string[] sendTo, [Optional] params string[] sendCC)
        {
            if (_mailMessage is null || _host is null || _credentials is null)
                throw new InvalidOperationException("Mail sender is not configured or mail not created.");

            var message = _mailMessage;
            _mailMessage = null;

            // MimeMessage is IDisposable (it can own streams for the body/attachments); dispose it once sent.
            using (message)
            {
                foreach (string email in sendTo)
                {
                    if (IsMailValid(email) == true) message.To.Add(MailboxAddress.Parse(email));
                }
                if (sendCC != null)
                {
                    foreach (string email in sendCC)
                    {
                        if (IsMailValid(email) == true) message.Cc.Add(MailboxAddress.Parse(email));
                    }
                }

                using var client = new SmtpClient { Timeout = 20_000 }; // 20 seconds
                await client.ConnectAsync(_host, _port, ResolveSecureSocketOptions(_host, _port)).ConfigureAwait(false);
                if (!client.IsSecure && !IsLoopbackHost(_host))
                    throw new SslHandshakeException("SMTP connection is not encrypted; refusing to send credentials.");
                await client.AuthenticateAsync(_credentials).ConfigureAwait(false);
                await client.SendAsync(message).ConfigureAwait(false);
                await client.DisconnectAsync(true).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Port 465 uses implicit TLS; any other port requires STARTTLS so a network attacker cannot
        /// downgrade the session to plaintext. Loopback hosts (local dev relays) may stay unencrypted.
        /// </summary>
        public static SecureSocketOptions ResolveSecureSocketOptions(string host, int port)
        {
            if (port == 465)
                return SecureSocketOptions.SslOnConnect;

            return IsLoopbackHost(host)
                ? SecureSocketOptions.StartTlsWhenAvailable
                : SecureSocketOptions.StartTls;
        }

        private static bool IsLoopbackHost(string host) =>
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip));

        private static bool IsMailValid(string mail)
        {
            var addr = new EmailAddressAttribute();
            return addr.IsValid(mail);
        }

        public void Dispose()
        {
            _mailMessage?.Dispose();
            _mailMessage = null;
            _credentials = null;
            GC.SuppressFinalize(this);
        }
    }
}
