namespace VirtualBar.Application.Common;

/// <summary>
/// The <see cref="Result{T}.Reason"/> values the offer endpoints send. They are part of the API contract: the
/// frontend recognises them to show a translated message, so never rename one.
/// </summary>
public static class OfferErrorReasons
{
    /// <summary>Every bottle of the entry is reserved for buyers (#130): it cannot be offered on or sold again.</summary>
    public const string NoBottleLeft = "NoBottleLeft";
}
