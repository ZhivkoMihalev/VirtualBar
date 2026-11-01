# Result\<T\>

All service methods return `Result<T>` (never throw for expected failures):

```csharp
public async Task<Result<BottleDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
{
    var bottle = await db.Bottles.FindAsync(id, cancellationToken);
    if (bottle is null) return Result<BottleDto>.Fail("Bottle not found.");
    return Result<BottleDto>.Ok(Map(bottle));
}
```

Controllers translate results to HTTP via the `ToActionResult` extension (`VirtualBar.Api/Extensions/ResultExtensions.cs`):

```csharp
return result.Success ? Ok(result.Data) : result.ToActionResult(this);
```

Decorators use typed factory methods — never `Fail()` for infrastructure errors:
```csharp
Result<T>.NotFound("Bottle not found.")   // → 404
Result<T>.Forbidden("Forbidden.")         // → 403
Result<T>.Conflict("Already exists.")     // → 409
Result<T>.Fail("Name is required.")       // → 400 (validation)
```

The error body is `{ error }`. `Fail` and `Conflict` take an optional **reason**: a stable, machine-readable value that `ToActionResult` adds as `{ error, reason }` so the frontend can show a translated message instead of the English `error` text. The auth endpoints use it (`AuthErrorReasons` in `VirtualBar.Application/Common`: `InvalidCredentials`, `LockedOut`, `EmailNotConfirmed`, `EmailTaken`; the frontend translates them in the `authErrors` i18n namespace via `src/lib/authErrors.ts`). Reason values are API contract: never rename one. Login deliberately has a single `InvalidCredentials` for unknown email and wrong password, so it does not reveal which accounts exist.
