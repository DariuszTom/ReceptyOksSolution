using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Misc.MessegeSender
{
    public interface IMailSender
    {
        public Task MailConfig(string addres, int port, SecureString pw, string login);
        public void CreateMail(string subject, StringBuilder body);
        public Task<IAsyncResult> SendMail(string[] sendTo, [Optional] params string[] sendCC);
    }
}
