---
paths:
  - "VirtualBar.Tests/**"
---

# Testing Conventions

- One test class per service: `<ServiceName>Tests` in `VirtualBar.Tests/Services/`.
- Method naming: `<MethodName>_When<Condition>_<ExpectedOutcome>`.
- Each test creates an isolated InMemory DB: `Guid.NewGuid().ToString()` as the DB name.
- Use **EF Core InMemory** by default. Switch to **SQLite in-memory** only when the method calls `ExecuteUpdateAsync` / `ExecuteDeleteAsync`, or when the test must surface a **unique-index or duplicate-PK violation as `DbUpdateException`** (InMemory doesn't enforce secondary unique indexes and throws `ArgumentException`, not `DbUpdateException`, for PK duplicates — e.g. the `BottleReview` duplicate-race and `BadgeService` PK-race tests).
- Mock only `ICurrentUser`, `INotificationService`, `IBadgeService`, and `IProductRequestService` (trigger-service tests) — never mock `AppDbContext`.
- Services that depend on `INotificationService` / `IBadgeService` / `IProductRequestService` receive `Mock.Of<T>()` in their `CreateXxxService` helper (optional parameters with defaults).
- Seed helpers are `private static` methods in the test class: `SeedUser`, `SeedBottle`, `SeedComment`, etc.
- Cover every branch: every `if`, every `switch` arm, every `?.`, `??`, `&&`, `||`.
