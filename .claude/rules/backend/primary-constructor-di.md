# Primary Constructor + DI

```csharp
public sealed class BottleService(
    AppDbContext db,
    ICurrentUser currentUser,
    INotificationService notificationService) : IBottleService
```
