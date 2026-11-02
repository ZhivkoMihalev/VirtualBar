---
paths:
  - "**/*Currenc*"
  - "**/currenc*"
---

# Currencies

The app accepts only **EUR, GBP and USD** (#98). The Bulgarian lev (BGN) no longer exists and must not appear anywhere
in the app, and no other currency is offered.

## Backend
- The list lives in `SupportedCurrencies` (`VirtualBar.Application/Common/SupportedCurrencies.cs`): `All`,
  `IsSupported` (ignores case and surrounding spaces) and `ToCode` (trimmed, upper-case).
- Checked in the decorators, `400` with `SupportedCurrencies.NotSupportedMessage` ("Currency must be EUR, GBP or USD."):
  - `BottleValidationDecorator.ListForSaleAsync`, after the quantity check;
  - `OfferValidationDecorator.CreateAsync`, after "Currency is required." for an empty value.
- `BottleService.ListForSaleAsync` and `OfferService.CreateAsync` store `ToCode(...)`, so `" gbp "` is stored as `GBP`.
- **FX invariant:** each supported currency is the pricing base currency or a key of `Pricing:FxToBase` in
  `appsettings.json`, so every amount in the app can be converted. `SupportedCurrenciesTests` reads the real
  `appsettings.json` and fails when a currency cannot be converted.
- Price research reads web sources in any currency. `PriceProviderBase` converts or drops them internally, and users
  only see estimates in the base currency.

## Frontend
- `src/lib/currencies.ts`: `CURRENCIES` (`EUR`, `GBP`, `USD`), `DEFAULT_CURRENCY` (`EUR`) and `formCurrency`.
- The sale form and the offer form in `BottleDetailPanel` offer `CURRENCIES` and start with `formCurrency(bottle.currency)`:
  the bottle's own currency when it is still supported, otherwise EUR.
- A price whose bottle has no currency is shown with `DEFAULT_CURRENCY`.

## Existing data
- Listings and offers saved before #98 in another currency are left as they are; only new ones are refused.
- Converting or unlisting them is an `UPDATE` that needs the owner's explicit approval (CRITICAL RULES), still open in
  #98.

## Related rules
- Listings: `AskingPrice` and `Currency` on the bottle (`.claude/rules/domain/bottles.md`).
- Offers: `OfferedPrice` and `Currency` (`.claude/rules/domain/offers.md`).
- The FX table and the base currency (`.claude/rules/domain/pricing.md`).
