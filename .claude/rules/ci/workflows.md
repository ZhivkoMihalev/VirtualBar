---
paths:
  - ".github/**"
---

# CI (GitHub Actions)

`.github/workflows/ci.yml` runs on every PR to `master`, every push to `master`, and on demand. Two parallel jobs:

## Backend
1. Restore → build (Release) → unit tests with coverage.
2. **HTTP tests** (`VirtualBar.HttpTests`) with coverage, against a SQL Server 2022 service container
   (`VIRTUALBAR_TEST_SQL`). They run even when the unit tests failed, so one push shows every failure; raw results of
   both are in the `backend-test-results` artifact.
3. **Coverage report**, merging both test projects (ReportGenerator, `VirtualBar.Infrastructure.Migrations.*`
   excluded). Informational: it never fails the build. It is:
   - written to the job summary;
   - posted as one PR comment that is edited in place on every push (skipped on Dependabot and fork PRs, whose token
     is read-only);
   - uploaded as the `coverage-report` HTML artifact.
4. `dotnet ef migrations has-pending-model-changes`, which fails when an entity changed without a matching migration.

It needs no secrets: the SQL Server password is a CI-only literal for a container that lives only as long as the job,
and a throwaway `Jwt:Key` is generated where one is needed.

## Frontend
From the repo root:
1. `npm ci` (both workspaces, root lockfile)
2. `npm run lint` (both workspaces; blocking — must report 0 problems)
3. `npm run typecheck` (the tests)
4. `npm test` (Vitest, no backend needed)
5. `npm run build`
