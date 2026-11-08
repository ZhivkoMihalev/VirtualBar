using VirtualBar.Application.Common;

namespace VirtualBar.Tests.Services;

/// <summary>The barcode rule shared by bottles, product requests and the barcode lookup (#49).</summary>
public sealed class BarcodeFormatTests
{
    [Theory]
    [InlineData("50103140")]           // EAN-8
    [InlineData("082184090466")]       // UPC-A
    [InlineData("5000267014203")]      // EAN-13
    [InlineData("15000267014200")]     // ITF-14
    [InlineData("  5000267014203  ")]  // padded: checked trimmed
    public void IsValid_WhenEightToFourteenDigits_IsTrue(string barcode) =>
        Assert.True(BarcodeFormat.IsValid(barcode));

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("123456789012345")]
    [InlineData("50002670142AB")]
    [InlineData("5000267-014203")]
    [InlineData("５０００２６７０１４２０３")] // full-width digits are not ASCII digits
    public void IsValid_WhenNotEightToFourteenAsciiDigits_IsFalse(string barcode) =>
        Assert.False(BarcodeFormat.IsValid(barcode));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" 5000267014203 ", "5000267014203")]
    public void Normalize_TrimsAndTurnsBlankIntoNull(string? barcode, string? expected) =>
        Assert.Equal(expected, BarcodeFormat.Normalize(barcode));

    [Fact]
    public void Equivalents_OfAUpcA_AreItsEan13AndGtin14Forms() =>
        Assert.Equal(
            ["082184090466", "0082184090466", "00082184090466"],
            BarcodeFormat.Equivalents("082184090466"));

    [Fact]
    public void Equivalents_OfAnEan13WithALeadingZero_IncludeTheUpcA() =>
        Assert.Contains("082184090466", BarcodeFormat.Equivalents("0082184090466"));

    [Fact]
    public void Equivalents_OfAnEan13_AreItAndItsGtin14Form() =>
        Assert.Equal(["5000267014203", "05000267014203"], BarcodeFormat.Equivalents("5000267014203"));

    [Fact]
    public void Equivalents_OfAnEan8_ReachEveryLongerForm() =>
        Assert.Equal(
            ["50103140", "000050103140", "0000050103140", "00000050103140"],
            BarcodeFormat.Equivalents("50103140"));

    [Fact]
    public void Equivalents_KeepALengthNoGtinHas_AsGiven() =>
        Assert.Equal("123456789", BarcodeFormat.Equivalents(" 123456789 ")[0]);
}
