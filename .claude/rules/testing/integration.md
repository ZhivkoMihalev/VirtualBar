---
paths:
  - "VirtualBar.Tests/**"
---

# Integration tests

**Integration tests (`VirtualBar.Tests/Integration/`)** — the deliberate exception to the mocking rule above. A flow that spans services (trigger service → notification → badge) can only be proven end to end by wiring the **real** collaborators, so these classes assemble the service graph exactly as `DependencyInjection.cs` does — real `NotificationService` and `BadgeService` behind their decorators, one graph per acting user (a request scope), only `ICurrentUser` mocked. Do NOT "fix" them back to `Mock.Of<T>()`: mocks would hide the very chain under test. They complement the per-service classes, never replace them — branch coverage still belongs in `Services/`. Because the badge engine swallows and logs its own exceptions, these tests also assert `ILogger<BadgeService>` logged no error, so a silent failure cannot pass as success. Existing file: `ProductRequestApprovalFlowTests` (approve → requester notified + `FirstCatalogProduct` awarded).
