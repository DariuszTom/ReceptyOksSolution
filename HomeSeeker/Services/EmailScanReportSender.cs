using System.Security;
using System.Text;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedLibrary.Misc.MessegeSender;

namespace HomeSeeker.Services;

/// <summary>
/// Sends scan reports via email using the shared <see cref="MailSender"/> (MailKit).
/// Creates a new <see cref="MailSender"/> per send to ensure proper disposal.
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

        var recipients = NotificationEmails.Parse(recipient);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("No recipient specified, skipping email send");
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var password = ToSecureString(_smtpOptions.Password);
            using var mailSender = new MailSender { IsBodyHtml = true };

            await mailSender.MailConfig(_smtpOptions.Host, _smtpOptions.Port, password, _smtpOptions.Login)
                .ConfigureAwait(false);

            mailSender.CreateMail(subject, new StringBuilder(htmlContent));

            _logger.LogDebug("Sending email to {RecipientCount} recipient(s): {Subject}", recipients.Count, subject);

            await mailSender.SendMail(recipients.ToArray()).ConfigureAwait(false);

            _logger.LogInformation("Email sent successfully to {RecipientCount} recipient(s)", recipients.Count);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Email send cancelled");
            return false;
        }
        catch (SmtpCommandException ex)
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

    private static SecureString ToSecureString(string value)
    {
        var secure = new SecureString();
        foreach (var c in value)
        {
            secure.AppendChar(c);
        }
        secure.MakeReadOnly();
        return secure;
    }
}
