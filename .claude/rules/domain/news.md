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

## Home feed (`GET /api/feed?skip=&take=&lang=`)
`[AllowAnonymous]`; `FeedService.GetFeedAsync` → `List<FeedItemDto>`, newest first. The home page (`HomePage`) asks for
the first 50 items.
- `FeedValidationDecorator`: a negative `skip` becomes 0, a `take` outside 1–100 becomes 20, a blank `lang` becomes `bg`.
- `FeedItemType` (append-only): `News`, `NewBottle`, `ForSale`, `Reviewed`.
- **News:** the 50 newest non-deleted posts, translated like `NewsService.Resolve` (`lang`, falling back to `bg`).
  - Known gap: #91. The 50 is fixed, so a page that reaches past the 50th post never returns an older one.
- **Followed activity**, only for a logged-in collector who follows someone:
  - `NewBottle`: a followed collector's non-deleted bottle, at its `CreatedAt`;
  - `ForSale`: their listed bottle, at its `ForSaleAt`;
  - `Reviewed`: their non-deleted review of a non-deleted bottle, at the review's `CreatedAt` (an edit does not bring it
    back to the top). A review of their own bottle shows too.
- Each followed-activity source loads only its newest `skip + take` items (added as `long`, so a huge `skip` cannot
  overflow): the page is cut from the merge of every source, so no source can put more on it.

### A `Reviewed` item
- `ActorUserId` / `ActorDisplayName` / `ActorAvatarUrl`: the reviewer. `BottleUserId` / `BottleUserDisplayName` stay
  the bottle's owner, who need not be the reviewer.
- `ReviewScore`; `ReviewSummary`, whole up to `FeedService.ReviewSummaryMaxLength` (280) characters, else cut at the last
  space that fits with "…" (a blank summary is `null`); `ReviewFlavors`, the first `FeedService.ReviewFlavorsShown` (3)
  in enum order.
- `ProductId` / `ProductName` / `ProductBrand` when the bottle is linked to a non-deleted product; the brand falls back
  to the product's non-deleted distillery, as the catalog does.
- The card (`ReviewActivityCard` in `HomePage.tsx`) reads "X оцени Y" (`home.rated`). Its title links to
  `/product/:id` under the product name and brand when the bottle is linked, else to the owner's bar `/bar/:userId`
  under the bottle name, with "от бара на …" (`home.fromBarOf`) when the owner is not the reviewer.
- On a phone the card hides the bottle picture, so the review's text gets the whole width.

## Related rules
- The feed also shows the new bottles and listings of followed collectors (`.claude/rules/domain/bottles.md`,
  `.claude/rules/domain/follows.md`) and their reviews (`.claude/rules/domain/reviews.md`).
- A review of a linked bottle links to its product page (`.claude/rules/domain/catalog.md`).
