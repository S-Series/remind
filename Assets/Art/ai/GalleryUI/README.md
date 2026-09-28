# Gallery UI assets

Reference: gallery screen supplied in the conversation (`codex-clipboard-c7da6fe3-32ae-40bd-af35-240b05932bf4.png`). These are AI-generated recreations of the reusable UI parts, then mechanically separated into transparent PNGs. They are not pixel-exact crops of the screenshot.

## Contents

- `Icons/` (9): five sidebar categories, card play and lock, chapter star, and the compass reused from `HomeUI`.
- `Lines/` (8): chapter and sidebar rules, detail and card separators, and small star, diamond, pin, and corner accents.
- `Containers/` (8): thumbnail frames for normal and selected states, lock overlay, caption strip, detail panel and image frame, selected sidebar tab, and replay button.

Each PNG is a separate UI unit with transparency. Build a gallery card from its illustration, a thumbnail frame, the caption strip, and play icon. Place the selected frame over the normal state when selected; place the lock overlay and lock icon over a locked card. The detail panel, detail image frame, and replay button can be positioned independently. Stretch long rules or panels in Unity as needed; check borders visually before choosing nine-slice values.

The scene background, card illustrations, photos, lettering, and other text were intentionally excluded. Artwork was made with image generation from the reference and project keywords: night sky, stars, glass, silver, navy, stardust, reflections, astronomical instruments, crystals, butterflies, light shards, and gothic academy. Unity scene placement and shader appearance have not been verified.
