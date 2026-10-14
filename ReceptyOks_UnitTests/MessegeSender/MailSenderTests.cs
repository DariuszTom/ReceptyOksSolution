using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using SharedLibrary.Misc.MessegeSender;

namespace ReceptyOks_UnitTests.MessegeSender;

[TestFixture]
public sealed class MailSenderTests
{
    private const string Login = "sender@example.com";

    private static SecureString Password()
    {
        var s = new SecureString();
        foreach (var c in "secret") s.AppendChar(c);
        s.MakeReadOnly();
        return s;
    }

    [Test]
    public void MailConfig_InvalidLogin_ThrowsArgumentException()
    {
        using var sut = new MailSender();

        var ex = Assert.ThrowsAsync<ArgumentException>(() => sut.MailConfig("localhost", 25, Password(), "not-an-email"));
        Assert.That(ex!.ParamName, Is.EqualTo("login"));
    }

    [Test]
    public void CreateMail_WithoutConfig_ThrowsInvalidOperationException()
    {
        using var sut = new MailSender();

        Assert.Throws<InvalidOperationException>(() => sut.CreateMail("subject", new StringBuilder("body")));
    }

    [Test]
    public void SendMail_WithoutConfig_ThrowsInvalidOperationException()
    {
        using var sut = new MailSender();

        Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendMail(["a@example.com"]));
    }

    [Test]
    public async Task SendMail_ConfiguredButNoMailCreated_ThrowsInvalidOperationException()
    {
        using var sut = new MailSender();
        await sut.MailConfig("localhost", 25, Password(), Login);

        Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendMail(["a@example.com"]));
    }

    [Test]
    public async Task SendMail_HtmlBody_SendsMessageWithExpectedHeadersAndRecipients()
    {
        using var server = new FakeSmtpServer();
        using var sut = new MailSender { IsBodyHtml = true };
        await sut.MailConfig("127.0.0.1", server.Port, Password(), Login);
        sut.CreateMail("Hello", new StringBuilder("<b>hi</b>"));

        await sut.SendMail(["to1@example.com", "to2@example.com"], "cc@example.com");
        var session = await server.Completed;

        Assert.Multiple(() =>
        {
            Assert.That(session.Authenticated, Is.True);
            Assert.That(session.MailFrom, Is.EqualTo(Login));
            Assert.That(session.RcptTo, Is.EquivalentTo(new[] { "to1@example.com", "to2@example.com", "cc@example.com" }));
            Assert.That(session.Data, Does.Contain("Subject: Hello"));
            Assert.That(session.Data, Does.Contain($"From: {Login}"));
            Assert.That(session.Data, Does.Contain("To: to1@example.com, to2@example.com"));
            Assert.That(session.Data, Does.Contain("Cc: cc@example.com"));
            Assert.That(session.Data, Does.Contain("Content-Type: text/html"));
            Assert.That(session.Data, Does.Contain("<b>hi</b>"));
        });
    }

    [Test]
    public async Task SendMail_PlainBody_UsesTextPlainContentType()
    {
        using var server = new FakeSmtpServer();
        using var sut = new MailSender { IsBodyHtml = false };
        await sut.MailConfig("127.0.0.1", server.Port, Password(), Login);
        sut.CreateMail("Plain", new StringBuilder("just text"));

        await sut.SendMail(["to@example.com"]);
        var session = await server.Completed;

        Assert.That(session.Data, Does.Contain("Content-Type: text/plain"));
        Assert.That(session.Data, Does.Not.Contain("text/html"));
    }

    [Test]
    public async Task SendMail_InvalidRecipients_AreSkipped()
    {
        using var server = new FakeSmtpServer();
        using var sut = new MailSender();
        await sut.MailConfig("127.0.0.1", server.Port, Password(), Login);
        sut.CreateMail("s", new StringBuilder("b"));

        await sut.SendMail(["valid@example.com", "not an email"], "also-invalid");
        var session = await server.Completed;

        Assert.That(session.RcptTo, Is.EqualTo(new[] { "valid@example.com" }));
    }

    [Test]
    public async Task SendMail_AllRecipientsInvalid_ThrowsInvalidOperationException()
    {
        using var server = new FakeSmtpServer();
        using var sut = new MailSender();
        await sut.MailConfig("127.0.0.1", server.Port, Password(), Login);
        sut.CreateMail("s", new StringBuilder("b"));

        Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendMail(["nope"]));
    }

    [Test]
    public async Task SendMail_CalledTwiceForSameMail_SecondCallThrows()
    {
        using var server = new FakeSmtpServer();
        using var sut = new MailSender();
        await sut.MailConfig("127.0.0.1", server.Port, Password(), Login);
        sut.CreateMail("s", new StringBuilder("b"));
        await sut.SendMail(["to@example.com"]);

        Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendMail(["to@example.com"]));
    }

    [Test]
    public async Task SendMail_ServerNotListening_ThrowsSocketException()
    {
        int freePort;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            freePort = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
        }

        using var sut = new MailSender();
        await sut.MailConfig("127.0.0.1", freePort, Password(), Login);
        sut.CreateMail("s", new StringBuilder("b"));

        Assert.ThrowsAsync<SocketException>(() => sut.SendMail(["to@example.com"]));
    }

    /// <summary>Minimal single-session plaintext SMTP server supporting EHLO, AUTH PLAIN, MAIL, RCPT, DATA, QUIT.</summary>
    private sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly TaskCompletionSource<Session> _completed = new();

        public FakeSmtpServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(RunAsync);
        }

        public int Port { get; }
        public Task<Session> Completed => _completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        private async Task RunAsync()
        {
            var session = new Session();
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

                await writer.WriteLineAsync("220 localhost ESMTP fake");

                string? line;
                while ((line = await reader.ReadLineAsync()) is not null)
                {
                    var upper = line.ToUpperInvariant();
                    if (upper.StartsWith("EHLO") || upper.StartsWith("HELO"))
                    {
                        await writer.WriteLineAsync("250-localhost");
                        await writer.WriteLineAsync("250 AUTH PLAIN");
                    }
                    else if (upper.StartsWith("AUTH PLAIN"))
                    {
                        if (line.Length <= "AUTH PLAIN".Length)
                        {
                            await writer.WriteLineAsync("334 ");
                            await reader.ReadLineAsync();
                        }
                        session.Authenticated = true;
                        await writer.WriteLineAsync("235 Authentication successful");
                    }
                    else if (upper.StartsWith("MAIL FROM:"))
                    {
                        session.MailFrom = ExtractAddress(line);
                        await writer.WriteLineAsync("250 OK");
                    }
                    else if (upper.StartsWith("RCPT TO:"))
                    {
                        session.RcptTo.Add(ExtractAddress(line));
                        await writer.WriteLineAsync("250 OK");
                    }
                    else if (upper.StartsWith("DATA"))
                    {
                        await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                        var sb = new StringBuilder();
                        while ((line = await reader.ReadLineAsync()) is not null && line != ".")
                        {
                            sb.AppendLine(line);
                        }
                        session.Data = sb.ToString();
                        await writer.WriteLineAsync("250 OK queued");
                    }
                    else if (upper.StartsWith("QUIT"))
                    {
                        await writer.WriteLineAsync("221 Bye");
                        break;
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 OK");
                    }
                }

                _completed.TrySetResult(session);
            }
            catch (Exception ex)
            {
                _completed.TrySetException(ex);
            }
        }

        private static string ExtractAddress(string line)
        {
            var start = line.IndexOf('<') + 1;
            var end = line.IndexOf('>', start);
            return line[start..end];
        }

        public void Dispose() => _listener.Stop();

        public sealed class Session
        {
            public bool Authenticated { get; set; }
            public string? MailFrom { get; set; }
            public List<string> RcptTo { get; } = [];
            public string Data { get; set; } = string.Empty;
        }
    }
}
