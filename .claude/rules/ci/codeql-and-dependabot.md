---
paths:
  - ".github/**"
  - "**/package.json"
  - "**/*.csproj"
---

# CodeQL and Dependabot

**CodeQL** (security scanning of C#, JS/TS and the workflows) runs through GitHub's *default setup*, configured in the repository settings (Advanced Security → Code scanning), not in a workflow file. Findings appear in the Security tab and on PRs.

`.github/dependabot.yml` opens **monthly** update PRs for npm (one entry at the repo root, which covers both workspaces and the root lockfile), NuGet and GitHub Actions (minor + patch grouped into one PR per ecosystem, majors separate — except `vite` and `@vitejs/*`, whose majors come as one PR because neither installs without the other). Security updates are not bound to the schedule.
