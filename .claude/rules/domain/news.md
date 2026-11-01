---
paths:
  - "**/*News*"
  - "**/news*"
  - "**/*Feed*"
  - "**/HomePage*"
---

# News

## NewsPost
| Property | Type | Notes |
|---|---|---|
| `CoverImageUrl` | `string?` | Optional cover image (shared across languages) |
| `AuthorId` | `Guid` | FK → AppUser |

Has `ICollection<NewsPostTranslation> Translations`. The flat `Title`/`Excerpt`/`Content` columns were removed by migration — all text is in the translations table. Admin-only write (`IsAdmin` check in `NewsValidationDecorator`). Public read via `GET /api/news?lang=bg` and `GET /api/feed?lang=bg`.

## NewsPostTranslation
Composite PK: `(PostId, LanguageCode)`. Cascade delete from `NewsPost`.
| Property | Type | Notes |
|---|---|---|
| `PostId` | `Guid` | FK → NewsPost |
| `LanguageCode` | `string` | e.g. `"bg"`, `"en"` |
| `Title` | `string` | |
| `Content` | `string` | Full article body |

`NewsService.Resolve(post, lang)` returns the translation matching `lang`, falls back to `"bg"`. Bulgarian translation is required — enforced in `NewsValidationDecorator`.

## Related rules
- The feed (`GET /api/feed`) also shows the new bottles and listings of followed collectors (`.claude/rules/domain/bottles.md`,
  `.claude/rules/domain/follows.md`).
