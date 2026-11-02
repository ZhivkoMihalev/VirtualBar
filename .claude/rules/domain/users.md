---
paths:
  - "**/AppUser*"
  - "**/*Profile*"
  - "**/*Auth*"
  - "**/*auth*"
  - "**/usersApi*"
---

# AppUser (extends IdentityUser\<Guid\>)
| Property | Type | Notes |
|---|---|---|
| `DisplayName` | `string` | Indexed |
| `Bio` | `string?` | |
| `AvatarUrl` | `string?` | Relative path under `/uploads/avatars/` |
| `Country`, `City` | `string?` | |
| `IsAdmin` | `bool` | Platform administrator flag (default `false`) |
| `CreatedAt` | `DateTime` | Set on registration. `AppUser` extends `IdentityUser`, not `BaseEntity`, so it has no `UpdatedAt` or soft delete |

Relations: one user → many `Bottle`, `BottleComment`, `Message` (sent/received).
Junction relations: `BottleLike` (liked), `UserFollow` (followers/following), `UserBadge` (earned badges).

## Auth endpoints (`/api/auth`)
Every action is rate-limited by the `auth` policy (`[EnableRateLimiting("auth")]` on the controller).
- `POST /api/auth/register` — `[AllowAnonymous]`; a token, or `requiresEmailConfirmation: true` when confirmation is
  on (`.claude/rules/frontend/routing.md` → Email confirmation); `409` with `reason: EmailTaken`
- `POST /api/auth/login` — `[AllowAnonymous]`; a token and the user, or `400` with an `AuthErrorReasons` reason
  (`AuthValidationDecorator.LoginAsync`):
  - `InvalidCredentials` for an unknown email and for a wrong password, on an unconfirmed account too, so login does
    not reveal which emails have an account;
  - `LockedOut` after 5 failed attempts, for 15 minutes (the lockout options in `Program.cs`), even for the right
    password. Wrong passwords on an unconfirmed account count too;
  - `EmailNotConfirmed` only after the right password on an unconfirmed account. `SignInManager` refuses such an
    account before it looks at the password, so the decorator checks the password and the lockout itself
    (`CheckUnconfirmedPasswordAsync`, #95).
- `POST /api/auth/logout` — `[Authorize]`; revokes the user's existing tokens by rotating the security stamp
- `POST /api/auth/confirm-email` — `[AllowAnonymous]`; `400` for an invalid or expired link
- `POST /api/auth/resend-confirmation` — `[AllowAnonymous]`; always a generic answer
- `POST /api/auth/forgot-password` and `POST /api/auth/reset-password` — see Password reset below

## Password reset
- `POST /api/auth/forgot-password` (`[AllowAnonymous]`, `{ email, language? }`):
  - always answers `200` with the same generic message, whether or not the email has an account;
  - for an existing account it emails a link to `{Email:FrontendBaseUrl}/reset-password?email=…&token=…` in the
    request's language (template `reset-password.<lang>.html`, default `bg`);
  - a failure to send is logged in `AuthValidationDecorator`, never shown.
- `POST /api/auth/reset-password` (`[AllowAnonymous]`, `{ email, token, newPassword }`) sets the new password: `200`, or
  `400`:
  - an unknown email and a wrong or expired token get the same body, `AuthService.InvalidResetLinkMessage`
    ("Invalid or expired reset link."), so the answer does not reveal which emails have an account (#81);
  - a valid link with a password that breaks the rules gets the password errors: Identity checks the token first, so
    they are reachable only with a real link.
- Frontend: `/forgot-password` (`ForgotPasswordPage`) and `/reset-password` (`ResetPasswordPage`, which reads `email`
  and `token` from the query string), both public.
