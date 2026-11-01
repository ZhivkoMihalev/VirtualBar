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

## Password reset
- `POST /api/auth/forgot-password` (`[AllowAnonymous]`, `{ email, language? }`):
  - always answers `200` with the same generic message, whether or not the email has an account;
  - for an existing account it emails a link to `{Email:FrontendBaseUrl}/reset-password?email=…&token=…` in the
    request's language (template `reset-password.<lang>.html`, default `bg`);
  - a failure to send is logged in `AuthValidationDecorator`, never shown.
- `POST /api/auth/reset-password` (`[AllowAnonymous]`, `{ email, token, newPassword }`) sets the new password: `200`, or
  `400` with the reason.
- Frontend: `/forgot-password` (`ForgotPasswordPage`) and `/reset-password` (`ResetPasswordPage`, which reads `email`
  and `token` from the query string), both public.
- Known gap: #81. `reset-password` answers an unknown email differently from a wrong token, so it reveals which emails
  have an account.
