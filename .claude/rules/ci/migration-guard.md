---
paths:
  - ".github/**"
  - "**/Migrations/**"
---

# Migration guard

`.github/workflows/migration-guard.yml` (the **Migration guard** check) runs on every PR to `master`. Migrations apply automatically on startup, so it scans the `Up()` method of every migration the PR adds or changes (`.github/scripts/check-destructive-migrations.sh`) and fails on `DropTable`, `DropColumn`, `DeleteData`, `UpdateData` or any raw `Sql(...)`: operations that delete or rewrite existing data on the next deploy. `Down()` is ignored, since it legitimately drops what `Up()` created. The check passes once the PR carries the **`db-destructive-approved`** label; add it only after the data change was agreed (see CRITICAL RULES).
