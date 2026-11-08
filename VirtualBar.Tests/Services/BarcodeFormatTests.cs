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
}
