---
paths:
  - ".github/**"
  - "**/Migrations/**"
---

# Migration guard

`.github/workflows/migration-guard.yml` (the **Migration guard** check) runs on every PR to `master`.
- Migrations apply automatically on startup, so it scans the `Up()` method of every migration the PR adds or changes
  (`.github/scripts/check-destructive-migrations.sh`).
- It fails on `DropTable`, `DropColumn`, `DeleteData`, `UpdateData` or any raw `Sql(...)`: operations that delete or
  rewrite existing data on the next deploy.
- `Down()` is ignored, since it legitimately drops what `Up()` created.
- The check passes once the PR carries the **`db-destructive-approved`** label; add it only after the data change was
  agreed (see CRITICAL RULES).
- It runs on `opened`, `synchronize` and `reopened`, and again when `db-destructive-approved` is added or removed.
- Adding or removing any other label starts a run in a concurrency group of its own, whose job is skipped. In the PR's
  group it would cancel the real run, or be cancelled by it, when a PR is opened with a label, and could leave a
  cancelled check on the PR (#108).
