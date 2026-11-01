# Notification
| Property | Type | Notes |
|---|---|---|
| `UserId` | `Guid` | FK → AppUser (recipient) |
| `Type` | `NotificationType` | See enum below |
| `ActorId` | `Guid` | FK → AppUser (who triggered it) |
| `ActorDisplayName` | `string` | Denormalised — avoids join on read |
| `ResourceId` | `Guid?` | Bottle ID (for bottle-related types) or Message ID |
| `ResourceName` | `string?` | Bottle name (for bottle-related types) |
| `IsRead` | `bool` | |

Both FK relations use `DeleteBehavior.Restrict`. Composite index on `(UserId, IsDeleted, CreatedAt)`.

**NotificationType enum:**
| Value | Triggered by |
|---|---|
| `BottleLiked` | `BottleLikeService.LikeAsync` |
| `BottleCommented` | `BottleCommentService.AddCommentAsync` |
| `NewFollower` | `UserFollowService.FollowAsync` |
| `NewMessage` | `MessageService.SendAsync` |
| `NewBottleFromFollowing` | `BottleService.AddBottleAsync` — fan-out to all followers |
| `BottleListedForSale` | `BottleService.ListForSaleAsync` — fan-out to all followers |
| `WishListMatch` | `BottleService.ListForSaleAsync` — fan-out to wish list owners whose criteria match the listed bottle |
| `OfferReceived` | `OfferService.CreateAsync` — sent to the seller |
| `OfferAccepted` | `OfferService.AcceptAsync` — sent to the buyer |
| `OfferDeclined` | `OfferService.DeclineAsync` — sent to the buyer |
| `BottleReviewed` | `BottleReviewService.AddReviewAsync` — sent to the bottle owner (create only, not on update/delete) |
| `BadgeEarned` | `BadgeService.EvaluateAsync` — sent to the badge earner via `CreateSystemAsync` (actor = recipient; `ResourceName` = the `BadgeType` name) |
| `ProductRequestApproved` | `ProductRequestService.ApproveAsync` — sent to the requester (`ResourceId` = new/linked product id, `ResourceName` = product name) |
| `ProductRequestRejected` | `ProductRequestService.RejectAsync` — sent to the requester (`ResourceId` = request id, `ResourceName` = requested product name) |

**INotificationService methods:**
- `CreateAsync(recipientId, type, resourceId, resourceName, ct)` — single recipient; decorator skips if `recipientId == currentUser.UserId` (no self-notifications).
- `CreateBulkAsync(recipientIds, type, resourceId, resourceName, ct)` — fan-out; queries actor display name once, `AddRange` + single `SaveChangesAsync`. Decorator filters out self from the list.
- `CreateSystemAsync(recipientId, type, resourceId, resourceName, ct)` — system notification with **no self-skip**: the recipient MAY equal the current user (own-milestone notifications); inner sets `ActorId = recipientId` + the recipient's **own** display name (missing recipient → silent return). Used only by the badge engine (`BadgeEarned`). Do NOT "fix" the self-skip on the other two — it protects every other type.
- All three are fire-and-forget calls from trigger services (not controller-exposed). They return `Task` (not `Result<T>`).
- `GET /api/notifications` — returns last 30 + global unread count.
- `PATCH /api/notifications/{id}/read` and `POST /api/notifications/read-all`.
