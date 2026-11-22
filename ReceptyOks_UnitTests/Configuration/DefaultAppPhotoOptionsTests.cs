using ReceptyOks.Configuration;

namespace ReceptyOks_UnitTests.Configuration;

[TestFixture]
public class DefaultAppPhotoOptionsTests
{
    [Test]
    public void CreatePhotoOptions_LargerDimension_ClampsToMaximum()
    {
        var options = DefaultAppPhotoOptions.CreatePhotoOptions("Tytuł", 4000);

        Assert.Multiple(() =>
        {
            Assert.That(options.MaximumWidth, Is.EqualTo(1280));
            Assert.That(options.MaximumHeight, Is.EqualTo(1280));
        });
    }

    [Test]
    public void CreatePhotoOptions_SmallerDimension_UsesRequestedDimension()
    {
        var options = DefaultAppPhotoOptions.CreatePhotoOptions("Tytuł", 640);

        Assert.Multiple(() =>
        {
            Assert.That(options.MaximumWidth, Is.EqualTo(640));
            Assert.That(options.MaximumHeight, Is.EqualTo(640));
        });
    }

    [Test]
    public void CreatePhotoOptions_Default_LimitsSizeAndCompresses()
    {
        var options = DefaultAppPhotoOptions.CreatePhotoOptions("Tytuł");

        Assert.Multiple(() =>
        {
            Assert.That(options.Title, Is.EqualTo("Tytuł"));
            Assert.That(options.SelectionLimit, Is.EqualTo(1));
            Assert.That(options.MaximumWidth, Is.EqualTo(1280));
            Assert.That(options.MaximumHeight, Is.EqualTo(1280));
            Assert.That(options.CompressionQuality, Is.EqualTo(75));
            Assert.That(options.RotateImage, Is.True);
        });
    }

    [Test]
    public void CreatePhotoOptions_CustomSelectionLimit_UsesRequestedLimit()
    {
        var options = DefaultAppPhotoOptions.CreatePhotoOptions("Tytuł", selectionLimit: 5);

        Assert.That(options.SelectionLimit, Is.EqualTo(5));
    }
}
