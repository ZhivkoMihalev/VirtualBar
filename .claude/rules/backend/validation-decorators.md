# Decorator Pattern for Validation

Every service that has validation logic uses the **Decorator pattern**:

- **`XxxValidationDecorator`**, located in `VirtualBar.Infrastructure/Decorators/`:
  - contains all `if` guard checks: input validation, and business rule checks like "does this resource exist?" or
    "is this email already taken?";
  - returns `Result<T>.Fail(...)` early on invalid state;
  - always calls `cancellationToken.ThrowIfCancellationRequested()` at the top of every method.
- **`XxxService`** (inner):
  - pure business logic only; assumes all validation has passed, so no `if` guards for preconditions;
  - no `cancellationToken.ThrowIfCancellationRequested()`: the decorator handles it, and EF Core async methods honor
    the token automatically.

DI wiring in `DependencyInjection.cs`:
```csharp
services.AddScoped<AuthService>();
services.AddScoped<IAuthService>(sp =>
    new AuthValidationDecorator(sp.GetRequiredService<AuthService>(), ...));
```
