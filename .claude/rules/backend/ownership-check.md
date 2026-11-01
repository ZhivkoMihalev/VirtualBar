# Ownership check

Every mutation on a user-owned resource must verify ownership before acting:

```csharp
if (bottle.UserId != currentUser.UserId)
    return Result<bool>.Forbidden("Forbidden.");
```
