namespace VirtualBar.Application.DTOs.Bottles;

/// <summary>
/// Gives a bottle the image of a catalog product. The request names the product, never an address: the server takes
/// the product's own <c>ImageUrl</c>, so a client cannot make the bottle's viewers load an address of its choice (#131).
/// </summary>
public sealed class LinkImageRequest
{
    /// <summary>The catalog product whose image the bottle takes. Required; checked in the validation decorator.</summary>
    public Guid? ProductId { get; set; }
}
