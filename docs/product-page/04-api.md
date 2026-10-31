# Slice 04 — API: three actions on `ProductsController`

> Prereq: slice 03. Build gate only.

Add to the existing controller (class stays `[Authorize]`; each action is `[AllowAnonymous]`).
Route order matters: `browse` and `barcode/{barcode}` are literal segments, `{id:guid}` is
constrained, so none of them collide.

| Action | Route | Responses |
|---|---|---|
| `Browse` | `GET /api/products/browse?search=&category=&page=&pageSize=` | `200 PagedResult<ProductCardDto>`, `400` bad search / page |
| `GetById` | `GET /api/products/{id:guid}` | `200 ProductDetailDto`, `404` |
| `GetReviews` | `GET /api/products/{id:guid}/reviews?page=&pageSize=` | `200 PagedResult<BottleReviewDto>`, `404`, `400` page |

Each action: full XML docs (`summary`, every `param`, every `response`),
`return result.Success ? Ok(result.Data) : result.ToActionResult(this);`.

Also fix the stale XML doc on `Search`: it says "clamped to 1-50", the decorator clamps to 1–51.

## Manual check
`dotnet run` in `VirtualBar.Api`, then without a token:
```bash
curl -s "http://localhost:5000/api/products/browse?page=1" | head -c 400
curl -s "http://localhost:5000/api/products/browse?search=mac&category=Whisky"
curl -s -o /dev/null -w "%{http_code}\n" "http://localhost:5000/api/products/00000000-0000-0000-0000-000000000001"   # 404
```

## Gate
`dotnet build` → 0 errors.
