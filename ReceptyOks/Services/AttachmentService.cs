using System.Text;
using ILogger = Serilog.ILogger;

namespace ReceptyOks.Services;

/// <summary>
/// Handles picking (from gallery / camera / file system) and persisting chat attachments.
/// Supported types: images (JPEG, PNG, GIF, WebP, HEIC) and PDF documents.
/// Files are copied to <see cref="FileSystem.AppDataDirectory"/> so they survive across sessions
/// and can be referenced by saved conversations.
/// </summary>
public sealed class AttachmentService
{
    /// <summary>Anthropic Claude image size hard limit (~5 MB).</summary>
    private const long MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>PDF size limit accepted by Anthropic Claude (~32 MB per request, we cap earlier).</summary>
    private const long MaxPdfBytes = 10 * 1024 * 1024;

    /// <summary>Text document size limit (~2 MB of source bytes, which decodes to ~500k tokens worst case).</summary>
    private const long MaxTextDocumentBytes = 2 * 1024 * 1024;

    /// <summary>Character cap for extracted text inlined in the prompt.</summary>
    private const int MaxTextCharacters = 200_000;

    private const string AttachmentsFolderName = "chat-attachments";

    private static readonly HashSet<string> AllowedImageMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/heic",
    };

    /// <summary>
    /// Extension → MIME map for text-based documents whose content is inlined in the prompt.
    /// </summary>
    private static readonly Dictionary<string, string> TextDocumentMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".markdown"] = "text/markdown",
        [".csv"] = "text/csv",
        [".tsv"] = "text/tab-separated-values",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".log"] = "text/plain",
        [".ini"] = "text/plain",
        [".yaml"] = "text/yaml",
        [".yml"] = "text/yaml",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private readonly ILogger _logger;

    public AttachmentService(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Prompts the user to pick an image from the gallery.
    /// </summary>
    public Task<ChatAttachment?> PickImageAsync(CancellationToken cancellationToken = default)
        => PickImageInternalAsync(useCamera: false, cancellationToken);

    /// <summary>
    /// Prompts the user to capture a photo with the device camera.
    /// </summary>
    public Task<ChatAttachment?> CapturePhotoAsync(CancellationToken cancellationToken = default)
        => PickImageInternalAsync(useCamera: true, cancellationToken);

    /// <summary>
    /// Prompts the user to pick a PDF document.
    /// </summary>
    public async Task<ChatAttachment?> PickPdfAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Wybierz plik PDF",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/pdf" } },
                    { DevicePlatform.iOS,     new[] { "com.adobe.pdf" } },
                    { DevicePlatform.WinUI,   new[] { ".pdf" } },
                    { DevicePlatform.MacCatalyst, new[] { "pdf" } },
                }),
            };

            var result = await FilePicker.Default.PickAsync(options).ConfigureAwait(false);
            if (result is null)
            {
                _logger.Information("PDF picker returned null (user cancelled)");
                return null;
            }

            // Validate by file extension because FileResult.ContentType is inconsistent
            // across platforms (empty on Windows, extension on some, MIME on others).
            var extension = Path.GetExtension(result.FileName);
            if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning("User selected non-PDF file: {FileName} (ContentType={ContentType})",
                    result.FileName, result.ContentType);
                return null;
            }

            using var stream = await result.OpenReadAsync().ConfigureAwait(false);
            return await PersistAsync(stream, "application/pdf", result.FileName, cancellationToken).ConfigureAwait(false);
        }
        catch (PermissionException ex)
        {
            _logger.Warning(ex, "Permission denied when picking PDF");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to pick PDF attachment");
            return null;
        }
    }

    /// <summary>
    /// Prompts the user to pick a text-based document (TXT, MD, CSV, JSON, XML, HTML, DOCX, ...).
    /// The document content is extracted as UTF-8 text and inlined in the prompt.
    /// </summary>
    public async Task<ChatAttachment?> PickDocumentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Wybierz dokument",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[]
                        {
                            "text/plain", "text/markdown", "text/csv", "text/html",
                            "application/json", "application/xml",
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        }
                    },
                    { DevicePlatform.iOS, new[]
                        {
                            "public.plain-text", "public.text", "public.comma-separated-values-text",
                            "public.html", "public.json", "public.xml",
                            "org.openxmlformats.wordprocessingml.document",
                        }
                    },
                    { DevicePlatform.WinUI, new[]
                        {
                            ".txt", ".md", ".markdown", ".csv", ".tsv", ".json", ".xml",
                            ".html", ".htm", ".log", ".ini", ".yaml", ".yml", ".docx",
                        }
                    },
                    { DevicePlatform.MacCatalyst, new[]
                        {
                            "txt", "md", "csv", "json", "xml", "html", "docx",
                        }
                    },
                }),
            };

            var result = await FilePicker.Default.PickAsync(options).ConfigureAwait(false);
            if (result is null)
            {
                _logger.Information("Document picker returned null (user cancelled)");
                return null;
            }

            var extension = Path.GetExtension(result.FileName);
            if (!TextDocumentMediaTypes.TryGetValue(extension, out var mediaType))
            {
                _logger.Warning("Unsupported document extension: {Extension} ({FileName})",
                    extension, result.FileName);
                return null;
            }

            using var stream = await result.OpenReadAsync().ConfigureAwait(false);
            return await PersistTextDocumentAsync(stream, mediaType, result.FileName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PermissionException ex)
        {
            _logger.Warning(ex, "Permission denied when picking document");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to pick document attachment");
            return null;
        }
    }

    /// <summary>
    /// Rematerializes raw attachment bytes (e.g. reconstructed from a saved conversation) back
    /// onto disk under <see cref="FileSystem.AppDataDirectory"/> so it can be shown as a thumbnail.
    /// </summary>
    /// <param name="data">Raw file bytes.</param>
    /// <param name="mediaType">MIME type (e.g. image/jpeg, application/pdf).</param>
    /// <param name="originalFileName">Optional file name to preserve for display.</param>
    /// <returns>The materialised attachment, or <c>null</c> when <paramref name="data"/> is empty.</returns>
    public async Task<ChatAttachment?> MaterializeAsync(
        byte[] data,
        string mediaType,
        string? originalFileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || string.IsNullOrWhiteSpace(mediaType))
        {
            return null;
        }

        var folder = Path.Combine(FileSystem.AppDataDirectory, AttachmentsFolderName);
        Directory.CreateDirectory(folder);

        var extension = GetExtensionForMediaType(mediaType, originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(folder, storedFileName);

        await File.WriteAllBytesAsync(storedPath, data, cancellationToken).ConfigureAwait(false);

        _logger.Information("Materialized attachment from history: {Path} ({Size} bytes, {MediaType})",
            storedPath, data.Length, mediaType);

        return new ChatAttachment(storedPath, mediaType, originalFileName ?? storedFileName, data);
    }

    /// <summary>
    /// Deletes a persisted attachment file from disk. Missing files are ignored.
    /// </summary>
    public void DeleteAttachment(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to delete attachment {Path}", filePath);
        }
    }

    private async Task<ChatAttachment?> PickImageInternalAsync(bool useCamera, CancellationToken cancellationToken)
    {
        try
        {
            if (useCamera && !MediaPicker.Default.IsCaptureSupported)
            {
                _logger.Warning("Photo capture is not supported on this device");
                return null;
            }

            var result = useCamera
                ? await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(false)
                : await MediaPicker.Default.PickPhotoAsync().ConfigureAwait(false);

            if (result is null)
            {
                return null;
            }

            // FileResult.ContentType is inconsistent across platforms (empty on Windows,
            // extension on some, MIME on others). Trust the file extension first.
            var mediaType = InferImageMediaTypeFromName(result.FileName);
            if (!AllowedImageMediaTypes.Contains(mediaType) &&
                !string.IsNullOrWhiteSpace(result.ContentType) &&
                AllowedImageMediaTypes.Contains(result.ContentType))
            {
                mediaType = result.ContentType;
            }

            if (!AllowedImageMediaTypes.Contains(mediaType))
            {
                _logger.Warning("Unsupported image media type: {MediaType} ({FileName})",
                    mediaType, result.FileName);
                return null;
            }

            using var stream = await result.OpenReadAsync().ConfigureAwait(false);
            return await PersistAsync(stream, mediaType, result.FileName, cancellationToken).ConfigureAwait(false);
        }
        catch (PermissionException ex)
        {
            _logger.Warning(ex, "Permission denied when picking image");
            return null;
        }
        catch (FeatureNotSupportedException ex)
        {
            _logger.Warning(ex, "Media picker feature not supported");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to pick image attachment");
            return null;
        }
    }

    private async Task<ChatAttachment?> PersistAsync(
        Stream source,
        string mediaType,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        var isPdf = mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
        var maxBytes = isPdf ? MaxPdfBytes : MaxImageBytes;

        // Read into memory so we can validate size and reuse for both disk and byte[] output.
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length == 0)
        {
            _logger.Warning("Selected attachment is empty");
            return null;
        }

        if (buffer.Length > maxBytes)
        {
            _logger.Warning("Attachment exceeds size limit: {Size} bytes (max {Max})", buffer.Length, maxBytes);
            return null;
        }

        var folder = Path.Combine(FileSystem.AppDataDirectory, AttachmentsFolderName);
        Directory.CreateDirectory(folder);

        var extension = GetExtensionForMediaType(mediaType, originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(folder, storedFileName);

        buffer.Position = 0;
        await using (var fileStream = File.Create(storedPath))
        {
            await buffer.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        var data = buffer.ToArray();

        _logger.Information("Persisted chat attachment {Path} ({Size} bytes, {MediaType})",
            storedPath, data.Length, mediaType);

        return new ChatAttachment(storedPath, mediaType, originalFileName ?? storedFileName, data);
    }

    private async Task<ChatAttachment?> PersistTextDocumentAsync(
        Stream source,
        string mediaType,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length == 0)
        {
            _logger.Warning("Selected document is empty");
            return null;
        }

        if (buffer.Length > MaxTextDocumentBytes)
        {
            _logger.Warning("Document exceeds size limit: {Size} bytes (max {Max})",
                buffer.Length, MaxTextDocumentBytes);
            return null;
        }

        var data = buffer.ToArray();

        string? extractedText;
        try
        {
            extractedText = ExtractText(data, mediaType);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to extract text from document {FileName}", originalFileName);
            return null;
        }

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            _logger.Warning("No readable text extracted from document {FileName}", originalFileName);
            return null;
        }

        if (extractedText.Length > MaxTextCharacters)
        {
            _logger.Information("Truncating document text from {Length} to {Max} characters",
                extractedText.Length, MaxTextCharacters);
            extractedText = extractedText[..MaxTextCharacters] + "\n\n[... treść skrócona ...]";
        }

        var folder = Path.Combine(FileSystem.AppDataDirectory, AttachmentsFolderName);
        Directory.CreateDirectory(folder);

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".txt";
        }

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(folder, storedFileName);

        await File.WriteAllBytesAsync(storedPath, data, cancellationToken).ConfigureAwait(false);

        _logger.Information("Persisted text document {Path} ({Size} bytes, {MediaType}, {Chars} chars extracted)",
            storedPath, data.Length, mediaType, extractedText.Length);

        return new ChatAttachment(storedPath, mediaType, originalFileName ?? storedFileName, data, extractedText);
    }

    private static string ExtractText(byte[] data, string mediaType)
    {
        // DOCX = zipped OpenXML; word/document.xml holds the body text.
        if (string.Equals(mediaType,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                StringComparison.OrdinalIgnoreCase))
        {
            return ExtractDocxText(data);
        }

        // Plain-text formats: decode as UTF-8 with BOM detection.
        return DecodeText(data);
    }

    private static string DecodeText(byte[] data)
    {
        // BOM detection (UTF-8 / UTF-16 LE / UTF-16 BE).
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(data, 3, data.Length - 3);
        }
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(data, 2, data.Length - 2);
        }
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
        }

        return Encoding.UTF8.GetString(data);
    }

    private static string ExtractDocxText(byte[] data)
    {
        using var ms = new MemoryStream(data, writable: false);
        using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);

        var documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry is null)
        {
            return string.Empty;
        }

        using var entryStream = documentEntry.Open();
        var xdoc = System.Xml.Linq.XDocument.Load(entryStream);
        System.Xml.Linq.XNamespace w =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var builder = new StringBuilder();
        foreach (var paragraph in xdoc.Descendants(w + "p"))
        {
            foreach (var text in paragraph.Descendants(w + "t"))
            {
                builder.Append(text.Value);
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string InferImageMediaTypeFromName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".heic" or ".heif" => "image/heic",
            _ => string.Empty,
        };
    }

    private static string GetExtensionForMediaType(string mediaType, string? originalFileName)
    {
        if (!string.IsNullOrWhiteSpace(originalFileName))
        {
            var ext = Path.GetExtension(originalFileName);
            if (!string.IsNullOrEmpty(ext))
            {
                return ext.ToLowerInvariant();
            }
        }

        return mediaType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/heic" => ".heic",
            "application/pdf" => ".pdf",
            _ => ".jpg",
        };
    }
}

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
