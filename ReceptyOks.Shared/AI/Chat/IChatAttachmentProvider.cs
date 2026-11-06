namespace ReceptyOks.Shared.AI.Chat;

/// <summary>
/// Represents a single attachment prepared for a chat message.
/// </summary>
/// <param name="FilePath">Absolute path to the persisted copy on disk.</param>
/// <param name="MediaType">MIME type (e.g. "image/jpeg", "application/pdf", "text/plain").</param>
/// <param name="OriginalFileName">The original file name as provided by the picker.</param>
/// <param name="Data">Raw bytes of the file, used to build the AI request payload.</param>
/// <param name="TextContent">
/// Extracted plain-text content for text-based documents (TXT/MD/CSV/DOCX…). Sent inline in
/// the prompt instead of as a binary <c>DataContent</c>. <c>null</c> for images and PDFs.
/// </param>
public sealed record ChatAttachment(
    string FilePath,
    string MediaType,
    string OriginalFileName,
    byte[] Data,
    string? TextContent = null)
{
    public bool IsImage => MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public bool IsPdf => MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the attachment carries extracted text to be inlined into the prompt.</summary>
    public bool IsTextDocument => TextContent is not null;
}

/// <summary>
/// Picks, persists and cleans up chat attachments. Implementations are platform specific.
/// </summary>
public interface IChatAttachmentProvider
{
    /// <summary>Prompts the user to pick an image from the gallery.</summary>
    Task<ChatAttachment?> PickImageAsync(CancellationToken cancellationToken = default);

    /// <summary>Captures a photo with the device camera.</summary>
    Task<ChatAttachment?> CapturePhotoAsync(CancellationToken cancellationToken = default);

    /// <summary>Prompts the user to pick a PDF document.</summary>
    Task<ChatAttachment?> PickPdfAsync(CancellationToken cancellationToken = default);

    /// <summary>Prompts the user to pick a text-based document (TXT, MD, CSV, DOCX, ...).</summary>
    Task<ChatAttachment?> PickDocumentAsync(CancellationToken cancellationToken = default);

    /// <summary>Restores attachment bytes (e.g. from a saved conversation) to a local file.</summary>
    Task<ChatAttachment?> MaterializeAsync(byte[] data, string mediaType, string? originalFileName = null, CancellationToken cancellationToken = default);

    /// <summary>Removes stored attachments whose path is not in <paramref name="keepFilePaths"/>.</summary>
    void CleanupOrphanAttachments(IEnumerable<string> keepFilePaths);

    /// <summary>Deletes a stored attachment file. Missing files are ignored.</summary>
    void DeleteAttachment(string? filePath);
}
