---
paths:
  - "**/*Follow*"
  - "**/usersApi*"
---

# UserFollow
Composite PK: `(FollowerId, FollowedId)`. No `BaseEntity`.
| Property | Type |
|---|---|
| `FollowerId` | `Guid` |
| `FollowedId` | `Guid` |
| `FollowedAt` | `DateTime` |

Both FK relations use `DeleteBehavior.Restrict`.

## Related rules
- `UserFollowService.FollowAsync` sends `NewFollower` (`.claude/rules/domain/notifications.md`) and evaluates the followed
  user's badges (`FollowerGained`, `.claude/rules/domain/badges.md`).
- Followers receive the `NewBottleFromFollowing` and `BottleListedForSale` fan-outs (`.claude/rules/domain/bottles.md`),
  and the home feed shows the activity of followed users (`.claude/rules/domain/news.md`).
