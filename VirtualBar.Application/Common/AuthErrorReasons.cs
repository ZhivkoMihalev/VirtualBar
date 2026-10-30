namespace VirtualBar.Application.Common;

/// <summary>
/// The <see cref="Result{T}.Reason"/> values the auth endpoints send. They are part of the API contract: the
/// frontend translates each one (i18n namespace <c>authErrors</c>, keys = these values), so never rename one.
/// </summary>
public static class AuthErrorReasons
{
    /// <summary>Unknown email or wrong password. Deliberately one reason for both, so login does not reveal which accounts exist.</summary>
    public const string InvalidCredentials = "InvalidCredentials";

    /// <summary>Too many failed attempts; the account is temporarily locked.</summary>
    public const string LockedOut = "LockedOut";

    /// <summary>The password is right, but the email address has not been confirmed yet.</summary>
    public const string EmailNotConfirmed = "EmailNotConfirmed";

    /// <summary>Registration with an email that already has an account (only when email confirmation is off).</summary>
    public const string EmailTaken = "EmailTaken";
}
