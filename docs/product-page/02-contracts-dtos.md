# Slice 02 — Contracts & DTOs

> Prereq: `00-OVERVIEW.md` §3.3–3.6. Build gate only; no tests yet.

## Files — create (`VirtualBar.Application/DTOs/Products/`, one type per file)
### `PagedResult<T>` → `VirtualBar.Application/Common/PagedResult.cs`
`List<T> Items`, `bool HasMore`. Generic, reused by browse and product reviews.

### `ProductCardDto`
All `ProductDto` fields **plus** `OwnersCount` (distinct users), `AverageScore` (`double?`, 1 decimal),
`ReviewsCount`, `ListingsCount` (linked bottles with `IsForSale`). Inherit from nothing — copy the
fields (the codebase has no DTO inheritance).

### `ProductDetailDto`
- product fields: everything in `ProductDto` + `Description`;
- `AverageScore` (`double?`), `ReviewsCount` (`int`), `TopFlavors` (`List<FlavorTag>`);
- `OwnersCount` (`int`), `Owners` (`List<ProductOwnerDto>`, ≤ 20);
- `Listings` (`List<ProductListingDto>`);
- `LatestReviews` (`List<BottleReviewDto>`, ≤ 5 — reuse the existing DTO).

### `ProductOwnerDto`
`UserId`, `DisplayName`, `AvatarUrl?`, `BottleCount` (sum of `Quantity`).

### `ProductListingDto`
`BottleId`, `SellerId`, `SellerDisplayName`, `SellerAvatarUrl?`, `ForSaleQuantity`, `AskingPrice?`,
`Currency?`, `Condition` (`BottleCondition`), `VintageYear?`, `ForSaleAt?`.

## Files — modify
`VirtualBar.Application/Interfaces/IProductCatalogService.cs` — add:
```csharp
Task<Result<PagedResult<ProductCardDto>>> BrowseAsync(string? search, SpiritCategory? category, int page, int pageSize, CancellationToken cancellationToken);

Task<Result<ProductDetailDto>> GetProductAsync(Guid productId, CancellationToken cancellationToken);

Task<Result<PagedResult<BottleReviewDto>>> GetProductReviewsAsync(Guid productId, int page, int pageSize, CancellationToken cancellationToken);
```
Add throwing stubs to `ProductCatalogService` and pass-throughs to the decorator so it builds; slice
03 fills them in.

## Gate
`dotnet build` → 0 errors.
