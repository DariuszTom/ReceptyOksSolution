using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security;

namespace SharedLibrary.Misc.MessegeSender
{
    public class MailSender : IMailSender, IDisposable
    {
        #region Fields
        private SmtpClient? _smtpClient;
        private bool _IsBodyHtml;
        private MailMessage? _mailMessage;
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
            _smtpClient = new SmtpClient(addres)
            {
                Port = port,
                Credentials = new NetworkCredential(login, pw),
                EnableSsl = true,
                Timeout = 20_000 // 20 seconds
            };
        }
        public void CreateMail(string subject, StringBuilder body)
        {
            if (_MyMail is null) throw new InvalidOperationException("Mail sender is not configured. Call MailConfig first.");
            _mailMessage = new MailMessage
            {
                From = new MailAddress(_MyMail),
                Subject = subject,
                Body = body.ToString(),
                IsBodyHtml = _IsBodyHtml,
            };
        }
        public async Task SendMail(string[] sendTo, [Optional] params string[] sendCC)
        {
            if (_mailMessage is null || _smtpClient is null)
                throw new InvalidOperationException("Mail sender is not configured or mail not created.");

            using (_mailMessage)
            {
                foreach (string email in sendTo)
                {
                    if (await IsMailValid(email) == true) _mailMessage.To.Add(email);
                }
                if (sendCC != null)
                {
                    foreach (string email in sendCC)
                    {
                        if (await IsMailValid(email) == true) _mailMessage.CC.Add(email);
                    }
                }
                // Await the send before disposing the message
                await _smtpClient.SendMailAsync(_mailMessage).ConfigureAwait(false);
            }
        }

        private static async Task<bool> IsMailValid(string mail)
        {
            var addr = new EmailAddressAttribute();
            return await Task.Run(() => addr.IsValid(mail)).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _mailMessage?.Dispose();
            _smtpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
