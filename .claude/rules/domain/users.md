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

Relations: one user → many `Bottle`, `BottleComment`, `Message` (sent/received).
Junction relations: `BottleLike` (liked), `UserFollow` (followers/following), `UserBadge` (earned badges).
