using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
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
        public async Task MailConfig(string addres, int port, SecureString pw, string login)
        {
            if (await IsMailValid(login) == false)
                throw new ArgumentException("Invalid login email address", nameof(login));

            _MyMail = login;
            _host = addres;
            _port = port;
            _credentials = new NetworkCredential(login, pw);
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

            foreach (string email in sendTo)
            {
                if (await IsMailValid(email) == true) message.To.Add(MailboxAddress.Parse(email));
            }
            if (sendCC != null)
            {
                foreach (string email in sendCC)
                {
                    if (await IsMailValid(email) == true) message.Cc.Add(MailboxAddress.Parse(email));
                }
            }

            using var client = new SmtpClient { Timeout = 20_000 }; // 20 seconds
            await client.ConnectAsync(_host, _port, SecureSocketOptions.Auto).ConfigureAwait(false);
            await client.AuthenticateAsync(_credentials).ConfigureAwait(false);
            await client.SendAsync(message).ConfigureAwait(false);
            await client.DisconnectAsync(true).ConfigureAwait(false);
        }

        private static async Task<bool> IsMailValid(string mail)
        {
            var addr = new EmailAddressAttribute();
            return await Task.Run(() => addr.IsValid(mail)).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _mailMessage = null;
            _credentials = null;
            GC.SuppressFinalize(this);
        }
    }
}
