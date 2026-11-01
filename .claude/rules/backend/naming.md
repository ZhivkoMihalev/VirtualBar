---
paths:
  - "**/*.cs"
---

# Naming

- No abbreviated names: `cancellationToken`, never `ct` or `token` (CRITICAL RULES), and the same goes for every other parameter, variable and member (`bottle`, never `btl`).
- The one exception is the parameter of a short lambda, which the code names with one letter throughout: `.Where(b => !b.IsDeleted)`.
