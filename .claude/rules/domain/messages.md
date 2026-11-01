---
paths:
  - "**/*Message*"
  - "**/messages*"
  - "**/*Chat*"
---

# Message
| Property | Type | Notes |
|---|---|---|
| `SenderId` | `Guid` | FK → AppUser |
| `ReceiverId` | `Guid` | FK → AppUser |
| `Content` | `string` | |
| `IsRead` | `bool` | |

Both FK relations use `DeleteBehavior.Restrict`.
