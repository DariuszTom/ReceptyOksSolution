using Moq;
using NUnit.Framework;
using ReceptyOks.Services;
using ILogger = Serilog.ILogger;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class AttachmentServiceTests
{
    private Mock<ILogger> _loggerMock = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger>();
    }

    [Test]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        void Act() => new AttachmentService(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Test]
    public void Constructor_ValidLogger_DoesNotThrow()
    {
        // Act
        AttachmentService Act() => new(_loggerMock.Object);

        // Assert
        Assert.DoesNotThrow(() => Act());
    }

    [Test]
    public async Task PickImageAsync_OutsideMauiHost_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act
        // Without a running MAUI host, MediaPicker.Default throws (NotImplementedException/
        // platform-not-supported), which is caught by the generic catch block, logged, and
        // results in a null return - this exercises the delegation + exception handling path.
        var result = await service.PickImageAsync();

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task PickImageAsync_WithCancellationToken_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await service.PickImageAsync(cts.Token);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CapturePhotoAsync_OutsideMauiHost_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act
        // useCamera=true path: MediaPicker.Default.IsCaptureSupported or CapturePhotoAsync
        // throws outside a MAUI host, caught by the generic catch, logged, returns null.
        var result = await service.CapturePhotoAsync();

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CapturePhotoAsync_WithCancellationToken_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await service.CapturePhotoAsync(cts.Token);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task PickPdfAsync_OutsideMauiHost_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act
        // FilePicker.Default.PickAsync throws outside a running MAUI host; this is caught by
        // the generic catch block (not PermissionException), logged via _logger.Error, and
        // the method returns null. This exercises the try/options-construction/catch(Exception)
        // path of PickPdfAsync.
        var result = await service.PickPdfAsync();

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task PickPdfAsync_WithCancellationToken_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await service.PickPdfAsync(cts.Token);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task PickDocumentAsync_OutsideMauiHost_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act
        // Same rationale as PickPdfAsync: FilePicker.Default.PickAsync throws outside a MAUI
        // host, is caught by the generic catch(Exception) block, logged, and null is returned.
        // This exercises the try/options-construction (all DevicePlatform entries)/catch path
        // of PickDocumentAsync.
        var result = await service.PickDocumentAsync();

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task PickDocumentAsync_WithCancellationToken_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await service.PickDocumentAsync(cts.Token);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void MaterializeAsync_NullData_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act
        async Task Act() => await service.MaterializeAsync(null!, "image/png");

        // Assert
        Assert.ThrowsAsync<ArgumentNullException>(Act);
    }

    [Test]
    public async Task MaterializeAsync_EmptyData_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act
        var result = await service.MaterializeAsync(Array.Empty<byte>(), "image/png");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task MaterializeAsync_NullMediaType_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act
        var result = await service.MaterializeAsync(new byte[] { 1, 2, 3 }, null!);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task MaterializeAsync_WhitespaceMediaType_ReturnsNull()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act
        var result = await service.MaterializeAsync(new byte[] { 1, 2, 3 }, "   ");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void DeleteAttachment_NullPath_DoesNotThrow()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act & Assert
        Assert.DoesNotThrow(() => service.DeleteAttachment(null));
    }

    [Test]
    public void DeleteAttachment_EmptyPath_DoesNotThrow()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act & Assert
        Assert.DoesNotThrow(() => service.DeleteAttachment(string.Empty));
    }

    [Test]
    public void DeleteAttachment_WhitespacePath_DoesNotThrow()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);

        // Act & Assert
        Assert.DoesNotThrow(() => service.DeleteAttachment("   "));
    }

    [Test]
    public void DeleteAttachment_NonExistentFile_DoesNotThrow()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");

        // Act & Assert
        Assert.DoesNotThrow(() => service.DeleteAttachment(path));
    }

    [Test]
    public void DeleteAttachment_ExistingFile_DeletesFile()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "content");

        // Act
        service.DeleteAttachment(path);

        // Assert
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public void DeleteAttachment_PathThrowsOnFileExists_LogsWarningAndDoesNotThrow()
    {
        // Arrange
        var service = new AttachmentService(_loggerMock.Object);
        // Invalid path characters cause File.Exists/File.Delete to throw internally,
        // which is caught by the generic catch(Exception) block.
        var invalidPath = "invalid\0path.txt";

        // Act & Assert
        Assert.DoesNotThrow(() => service.DeleteAttachment(invalidPath));
    }

    [Test]
    public void ChatAttachment_IsImage_TrueForImageMediaType()
    {
        // Arrange
        var attachment = new ChatAttachment("path", "image/png", "file.png", new byte[] { 1 });

        // Act & Assert
        Assert.That(attachment.IsImage, Is.True);
    }

    [Test]
    public void ChatAttachment_IsImage_CaseInsensitive_TrueForImageMediaType()
    {
        // Arrange
        var attachment = new ChatAttachment("path", "IMAGE/JPEG", "file.jpg", new byte[] { 1 });

        // Act & Assert
        Assert.That(attachment.IsImage, Is.True);
    }

    [Test]
    public void ChatAttachment_IsImage_FalseForNonImageMediaType()
    {
        // Arrange
        var attachment = new ChatAttachment("path", "application/pdf", "file.pdf", new byte[] { 1 });

        // Act & Assert
        Assert.That(attachment.IsImage, Is.False);
    }

    [Test]
    public void ChatAttachment_IsPdf_TrueForPdfMediaType()
    {
        // Arrange
        var attachment = new ChatAttachment("path", "application/pdf", "file.pdf", new byte[] { 1 });

        // Act & Assert
        Assert.That(attachment.IsPdf, Is.True);
    }

    [Test]
    public void ChatAttachment_IsPdf_CaseInsensitive_TrueForPdfMediaType()
    {
        // Arrange
        var attachment = new ChatAttachment("path", "APPLICATION/PDF", "file.pdf", new byte[] { 1 });

        // Act & Assert
        Assert.That(attachment.IsPdf, Is.True);
    }

    [Test]
    public void ChatAttachment_IsPdf_FalseForNonPdfMediaType()
    {
        // Arrange
        var attachment = new ChatAttachment("path", "image/png", "file.png", new byte[] { 1 });

        // Act & Assert
        Assert.That(attachment.IsPdf, Is.False);
    }

    [Test]
    public void ChatAttachment_IsTextDocument_TrueWhenTextContentPresent()
    {
        // Arrange
        var attachment = new ChatAttachment("path", "text/plain", "file.txt", new byte[] { 1 }, "some text");

        // Act & Assert
        Assert.That(attachment.IsTextDocument, Is.True);
    }

    [Test]
    public void ChatAttachment_IsTextDocument_FalseWhenTextContentNull()
    {
        // Arrange
        var attachment = new ChatAttachment("path", "image/png", "file.png", new byte[] { 1 });

        // Act & Assert
        Assert.That(attachment.IsTextDocument, Is.False);
    }
}
