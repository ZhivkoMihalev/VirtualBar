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

## Related rules
- `MessageService.SendAsync` sends `NewMessage` (`.claude/rules/domain/notifications.md`); the bell opens the chat for it
  instead of navigating.
- The frontend is the floating `ChatWidget` with `ChatContext` (`.claude/rules/frontend/shared-components.md`,
  `.claude/rules/frontend/conventions.md`).
