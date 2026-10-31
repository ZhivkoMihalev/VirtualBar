using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using MimeKit;

namespace VirtualBar.HttpTests.Support;

/// <summary>The folder the API writes every email to (<c>Email:PickupDirectory</c>), read back as MIME messages.</summary>
public sealed partial class PickupMailbox
{
    private PickupMailbox(string folder) => Folder = folder;

    public string Folder { get; }

    public static PickupMailbox CreateTemporary() =>
        new(Path.Combine(Path.GetTempPath(), "virtualbar-httptests", Guid.NewGuid().ToString("N")));

    /// <summary>The emails sent to <paramref name="address"/>, oldest first.</summary>
    public IReadOnlyList<MimeMessage> To(string address)
    {
        if (!Directory.Exists(Folder))
            return [];

        // The API names each file after the UTC time it was written, so name order is send order.
        return Directory.GetFiles(Folder, "*.eml")
            .Order(StringComparer.Ordinal)
            .Select(path => MimeMessage.Load(path))
            .Where(message => message.To.Mailboxes.Any(
                mailbox => string.Equals(mailbox.Address, address, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// The <c>email</c> and <c>token</c> of the confirmation link in <paramref name="message"/>, decoded the way the
    /// frontend's <c>/confirm-email</c> page reads them from its query string.
    /// </summary>
    public static (string Email, string Token) ConfirmationLink(MimeMessage message)
    {
        var match = ConfirmationHref().Match(message.HtmlBody ?? "");
        Assert.True(match.Success, "The email has no /confirm-email link.");

        var link = new Uri(WebUtility.HtmlDecode(match.Groups["link"].Value));
        var query = QueryHelpers.ParseQuery(link.Query);
        return (query["email"].ToString(), query["token"].ToString());
    }

    public void Delete()
    {
        if (Directory.Exists(Folder))
            Directory.Delete(Folder, recursive: true);
    }

    [GeneratedRegex("href=\"(?<link>[^\"]*/confirm-email\\?[^\"]*)\"")]
    private static partial Regex ConfirmationHref();
}
