# Decorator Pattern for Validation

Every service that has validation logic uses the **Decorator pattern**:

- **`XxxValidationDecorator`** — contains all `if` guard checks (input validation, business rule checks like "does this resource exist?", "is this email already taken?"). Returns `Result<T>.Fail(...)` early on invalid state. Located in `VirtualBar.Infrastructure/Decorators/`. Always calls `cancellationToken.ThrowIfCancellationRequested()` at the top of every method.
- **`XxxService`** (inner) — pure business logic only. Assumes all validation has passed. No `if` guards for preconditions. No `cancellationToken.ThrowIfCancellationRequested()` — the decorator handles it; EF Core async methods honor the token automatically.

DI wiring in `DependencyInjection.cs`:
```csharp
services.AddScoped<AuthService>();
services.AddScoped<IAuthService>(sp =>
    new AuthValidationDecorator(sp.GetRequiredService<AuthService>(), ...));
```
