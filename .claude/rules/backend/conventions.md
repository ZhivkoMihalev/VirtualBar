---
paths:
  - "**/*.cs"
---

# Backend conventions

## Ownership check
The rule is in `CLAUDE.md` → Security Rules: every mutation on a user-owned resource must verify ownership before
acting. A decorator writes it like this:

```csharp
if (bottle.UserId != currentUser.UserId)
    return Result<bool>.Forbidden("Forbidden.");
```

## Primary Constructor + DI

```csharp
public sealed class BottleService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService) : IBottleService
```

## Service registration
All services are registered in `VirtualBar.Infrastructure/DependencyInjection.cs` and called from `Program.cs`.

## CancellationToken on every EF Core call
`.ToListAsync(cancellationToken)`, `.FirstOrDefaultAsync(cancellationToken)`, `SaveChangesAsync(cancellationToken)` — always.

## Naming
- No abbreviated names: `cancellationToken`, never `ct` or `token` (CRITICAL RULES), and the same goes for every other
  parameter, variable and member (`bottle`, never `btl`).
- The one exception is the parameter of a short lambda, which the code names with one letter throughout:
  `.Where(b => !b.IsDeleted)`.

## Code Style
- One blank line between every property in entity classes.

## Controller XML docs
Every public action must have:

```csharp
/// <summary>One-line description.</summary>
/// <param name="cancellationToken">Cancellation token.</param>
/// <response code="200">Success.</response>
/// <response code="400">Error description.</response>
```

## Admin
Admin is determined by `AppUser.IsAdmin` (bool, default `false`). It is:
- Exposed via `ICurrentUser.IsAdmin` (reads the `"isAdmin"` JWT claim)
- Emitted in the JWT by `AuthService.GenerateJwtToken`
- Included in the auth response as `AuthUserDto.IsAdmin`
- **Seeded on startup:** if `AdminEmail` is set in config (typically `appsettings.Development.json`, gitignored), that
  user's `IsAdmin` is set to `true` automatically on `dotnet run`

Admin checks always live in the **`XxxValidationDecorator`** — never in controllers or inner services:
```csharp
if (!currentUser.IsAdmin)
    return Result<T>.Forbidden("Only administrators can do this.");
```
