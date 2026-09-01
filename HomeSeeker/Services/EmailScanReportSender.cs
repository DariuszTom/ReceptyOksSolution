using System.Net;
using System.Net.Mail;
using System.Text;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeSeeker.Services;

/// <summary>
/// Sends scan reports via email using SMTP.
/// Creates a new SmtpClient per send to ensure proper disposal.
/// </summary>
public sealed class EmailScanReportSender : IScanReportSender
{
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<EmailScanReportSender> _logger;

    public EmailScanReportSender(
        IOptions<HomeSeekerOptions> options,
        ILogger<EmailScanReportSender> logger)
    {
        _smtpOptions = options.Value.Smtp;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string recipient,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
        {
            _logger.LogWarning("SMTP not configured, skipping email send");
            return false;
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogWarning("No recipient specified, skipping email send");
            return false;
        }

        try
        {
            using var smtpClient = new SmtpClient(_smtpOptions.Host)
            {
                Port = _smtpOptions.Port,
                Credentials = new NetworkCredential(_smtpOptions.Login, _smtpOptions.Password),
                EnableSsl = true,
                Timeout = 30_000 // 30 seconds
            };

            var fromAddress = string.IsNullOrWhiteSpace(_smtpOptions.FromAddress)
                ? _smtpOptions.Login
                : _smtpOptions.FromAddress;

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromAddress, "HomeSeeker"),
                Subject = subject,
                Body = htmlContent,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            mailMessage.To.Add(new MailAddress(recipient));

            _logger.LogDebug("Sending email to {Recipient}: {Subject}", recipient, subject);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Email sent successfully to {Recipient}", recipient);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Email send cancelled");
            return false;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error sending email to {Recipient}: {StatusCode}",
                recipient, ex.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", recipient);
            return false;
        }
    }
}
