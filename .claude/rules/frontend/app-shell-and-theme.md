---
paths:
  - "VirtualBar.Web/**"
  - "VirtualBar.Web.Tests/unit/**"
---

# App shell and visual theme

**App shell (`src/App.tsx`):** Fixed background image (`/public/bg-room.png`) + darkening overlay both `position: fixed; z-index: -1/-2`, content wrapper wrapped in `ChatProvider`, `<ChatWidget />` before `<Footer />`.

**Visual theme:** Tailwind CSS 4 + inline styles for the speakeasy aesthetic.
- Background: `#07030A` / `stone-950` / `stone-900`
- Accent gold: `#C9A84C` / `#E8C870`
- Text: `stone-100` / `stone-300`
- Cards/surfaces: dark semi-transparent with amber borders
- Fonts: Playfair Display (headings), Cormorant Garamond, Cinzel (labels/nav)

**Static inline styles:** Module-level `CSSProperties` constants (never created inside render). Dynamic values (hover state, `item.isRead` background) stay inline.
