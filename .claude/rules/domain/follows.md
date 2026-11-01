# UserFollow
Composite PK: `(FollowerId, FollowedId)`. No `BaseEntity`.
| Property | Type |
|---|---|
| `FollowerId` | `Guid` |
| `FollowedId` | `Guid` |
| `FollowedAt` | `DateTime` |

Both FK relations use `DeleteBehavior.Restrict`.
