#!/usr/bin/env bash
# Fails when a migration added or changed in this PR can destroy or rewrite existing data.
#
# Migrations run automatically when the API starts, so a migration that drops a column or a table
# deletes production data on the next deploy, with no way back. CLAUDE.md requires asking first;
# this check makes that a visible, deliberate step: the PR passes once it carries the
# db-destructive-approved label (the workflow passes APPROVED=true).
#
# Only Up() is scanned: it is what runs on deploy. Down() legitimately drops what Up() created.
#
# Usage: check-destructive-migrations.sh <base-commit>
set -euo pipefail

base="${1:?usage: check-destructive-migrations.sh <base-commit>}"
approved="${APPROVED:-false}"
migrations_dir="VirtualBar.Infrastructure/Migrations"
label="db-destructive-approved"

# Drops lose data; DeleteData/UpdateData and raw SQL rewrite it. Raw SQL is flagged whatever it
# contains, because its effect cannot be read from the call itself.
export DESTRUCTIVE_PATTERN='migrationBuilder\.(DropTable|DropColumn|DeleteData|UpdateData|Sql)[[:space:]]*\('

summary() {
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    printf '%s\n' "$@" >> "$GITHUB_STEP_SUMMARY"
  fi
}

mapfile -t files < <(
  git diff --name-only --diff-filter=AMR "$base"...HEAD -- "$migrations_dir" \
    | grep -E '\.cs$' \
    | grep -vE '\.Designer\.cs$|ModelSnapshot\.cs$' \
    || true
)

if [ "${#files[@]}" -eq 0 ]; then
  echo "No migrations added or changed in this PR."
  exit 0
fi

findings=()
for file in "${files[@]}"; do
  while IFS= read -r hit; do
    [ -n "$hit" ] && findings+=("$file:$hit")
  done < <(
    awk '
      /void[[:space:]]+Up[[:space:]]*\(/   { inUp = 1 }
      /void[[:space:]]+Down[[:space:]]*\(/ { inUp = 0 }
      inUp && $0 ~ ENVIRON["DESTRUCTIVE_PATTERN"] {
        line = $0
        sub(/^[[:space:]]+/, "", line)
        print FNR ":" line
      }
    ' "$file"
  )
done

echo "Checked ${#files[@]} migration(s): ${files[*]}"

if [ "${#findings[@]}" -eq 0 ]; then
  echo "OK: nothing in Up() drops or rewrites existing data."
  exit 0
fi

level="error"
[ "$approved" = "true" ] && level="warning"

summary "### Migration guard" "" "| File | Line | Operation |" "|---|---|---|"
for finding in "${findings[@]}"; do
  file="${finding%%:*}"
  rest="${finding#*:}"
  line="${rest%%:*}"
  code="${rest#*:}"
  message="${code//%/%25} (runs on deploy and can delete or rewrite existing data)"
  echo "::${level} file=${file},line=${line},title=Destructive migration::${message}"
  summary "| \`${file##*/}\` | ${line} | \`${code}\` |"
done

if [ "$approved" = "true" ]; then
  echo "Approved: the PR carries the '${label}' label."
  summary "" "Approved with the \`${label}\` label."
  exit 0
fi

echo "This PR has ${#findings[@]} data-destroying migration operation(s) in Up()."
echo "If this is intended, add the '${label}' label to the PR; the check then passes."
summary "" "Blocked: add the \`${label}\` label to the PR if this is intended."
exit 1
