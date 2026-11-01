---
paths:
  - "VirtualBar.Tests/**"
---

# Testing Conventions (`VirtualBar.Tests`)

## Folders
- `Services/`: xUnit unit tests (one file per service)
- `Integration/`: cross-service flow tests (real service graph, no mocks)
- `Persistence/`: EF model conventions (UTC DateTime round-trip)
- `Api/`: API helpers (ResultExtensions: status code + error body) + controller tests

## Service tests
- One test class per service: `<ServiceName>Tests` in `VirtualBar.Tests/Services/`.
- Method naming: `<MethodName>_When<Condition>_<ExpectedOutcome>`.
- Each test creates an isolated InMemory DB: `Guid.NewGuid().ToString()` as the DB name.
- Use **EF Core InMemory** by default. Switch to **SQLite in-memory** only when:
  - the method calls `ExecuteUpdateAsync` / `ExecuteDeleteAsync`, or
  - the test must surface a **unique-index or duplicate-PK violation as `DbUpdateException`**: InMemory doesn't enforce
    secondary unique indexes and throws `ArgumentException`, not `DbUpdateException`, for PK duplicates (e.g. the
    `BottleReview` duplicate-race and `BadgeService` PK-race tests).
- Mock only:
  - `ICurrentUser`;
  - the trigger services `INotificationService`, `IBadgeService` and `IProductRequestService`;
  - what lies outside the process: `IEmailService`, `IWebHostEnvironment` and `IFormFile` for uploads, and the Identity
    plumbing `AuthServiceTests` needs to build its managers.
- Never mock `AppDbContext`.
- Services that depend on `INotificationService` / `IBadgeService` / `IProductRequestService` receive `Mock.Of<T>()` in
  their `CreateXxxService` helper (optional parameters with defaults).
- Pass `CancellationToken.None` as the `cancellationToken` argument.
- Seed helpers are `private static` methods in the test class: `SeedUser`, `SeedBottle`, `SeedComment`, etc.
- Cover every branch: every `if`, every `switch` arm, every `?.`, `??`, `&&`, `||`.

## Integration tests (`VirtualBar.Tests/Integration/`)
- The deliberate exception to the mocking rule above.
- A flow that spans services (trigger service → notification → badge) can only be proven end to end by wiring the
  **real** collaborators. So these classes assemble the service graph exactly as `DependencyInjection.cs` does: real
  `NotificationService` and `BadgeService` behind their decorators, one graph per acting user (a request scope), only
  `ICurrentUser` mocked.
- Do NOT "fix" them back to `Mock.Of<T>()`: mocks would hide the very chain under test.
- They complement the per-service classes, never replace them — branch coverage still belongs in `Services/`.
- Because the badge engine swallows and logs its own exceptions, these tests also assert `ILogger<BadgeService>` logged
  no error, so a silent failure cannot pass as success.
- Existing file: `ProductRequestApprovalFlowTests` (approve → requester notified + `FirstCatalogProduct` awarded).
