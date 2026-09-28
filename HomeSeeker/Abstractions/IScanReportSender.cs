namespace HomeSeeker.Abstractions;

/// <summary>
/// Interface for sending scan reports via email.
/// </summary>
public interface IScanReportSender
{
    /// <summary>
    /// Sends an HTML report email.
    /// </summary>
    /// <param name="recipient">Recipient email address.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="htmlContent">HTML content of the email.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if sent successfully, false otherwise.</returns>
    Task<bool> SendAsync(
        string recipient,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken = default);
}
