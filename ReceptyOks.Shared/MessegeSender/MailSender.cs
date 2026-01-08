using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Misc.MessegeSender
{
    public class MailSender : IMailSender, IDisposable
    {
        #region Fields
        private SmtpClient _smtpClient;
        private bool _IsBodyHtml;
        private MailMessage _mailMessage;
        private string _MyMail;
        #endregion
        #region Properiets
        public bool IsBodyHtml { get => _IsBodyHtml; set => _IsBodyHtml = value; }
        #endregion
        public async Task<bool> MailConfig(string addres, int port, SecureString pw, string login)
        {
            if (await IsMailValid(login) == false) return false;

            _MyMail = login;
            _smtpClient = new SmtpClient(addres)
            {
                Port = port,
                Credentials = new NetworkCredential(login, pw),
                EnableSsl = true,
            };
            await Task.Run(() => _smtpClient.Timeout = 20);
            return true;
        }
        public void CreateMail(string subject, StringBuilder body)
        {
            _mailMessage = new MailMessage
            {
                From = new MailAddress(_MyMail),
                Subject = subject,
                Body = body.ToString(),
                IsBodyHtml = _IsBodyHtml,
            };
        }
        public async Task<IAsyncResult> SendMail(string[] sendTo, [Optional] params string[] sendCC)
        {
            if (_mailMessage is null || _smtpClient is null) return null;
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
                return _smtpClient.SendMailAsync(_mailMessage);
            }
        }


        public void Dispose()
        {
            _mailMessage.Dispose();
            _smtpClient.Dispose();
            GC.SuppressFinalize(this);
        }
        private async Task<bool> IsMailValid(string mail)
        {
            var addr = new EmailAddressAttribute();
            return await Task.Run(() => addr.IsValid(mail));
        }

        Task IMailSender.MailConfig(string addres, int port, SecureString pw, string login)
        {
            throw new NotImplementedException();
        }
    }
}
