# Character overlay artwork

The two PNGs in this folder were created with the built-in image generation tool
from the user-provided Character Select layout reference. The reference was used
for composition and character appearance, not as a source of gameplay data.

- `character_select_background.png`: Opaque moonlit academy background with the
  silver-haired character on the left. Prompt: Remove all typography, UI panels,
  cards, controls, borders, and logos from the supplied layout; restore the
  castle and lake behind them; preserve the character's face, pose, clothing,
  scale, and dark blue moonlit style; leave calm negative space on the right.
- `ame_hina_portraits.png`: Two-panel portrait sheet. Prompt: Use the supplied
  layout as a style and character reference; create a pink-haired AME portrait
  on the left and a dark-haired HINA portrait on the right, with moonlit navy
  backgrounds and a straight 50/50 division; exclude text, UI, frames, icons,
  and logos.

`CharacterOverlay.prefab` references both textures through `RawImage`. The
small portrait cards use UV rectangles within those original PNGs; no cropped
copies are maintained. Displayed profile stats and skill copy are layout
samples. They are not wired to the gameplay character/gauge rules.

## Character Select UI sprites

The additional PNGs in this folder were generated from the Character Select
layout reference as separate transparent Unity sprites. They do not contain
character portraits, scenery, or baked text. Place the existing character and
portrait artwork behind the frames, and render names, labels, values, and button
text with Unity UI text.

- Tabs and panels: `tab_selected`, `tab_idle`, `stats_panel_frame`,
  `skill_panel_frame`, `begin_button`.
- Portrait cards: `thumbnail_frame_idle`, `thumbnail_frame_selected`,
  `thumbnail_locked_overlay`, `icon_lock`, `navigation_chevron`.
- Stats: `stat_icon_memory`, `stat_icon_resonance`, `stat_icon_focus`,
  `stat_icon_support`, `progress_track`, `progress_fill`.
- Skill: `skill_icon_frame`, `skill_icon_moon`.
- Decorations: `icon_compass`, `icon_crescent`, `divider_starline`,
  `character_orbit`, `ornament_star`.

Each sprite has its own Unity `.meta` file and uses transparent RGBA pixels.
The source artwork is generative, so its line weight and ornament detail may
need layout tuning against the reference at final UI scale.

`CharacterOverlayPrefabBuilder` assigns these sprites to the overlay prefab.
`CharacterOverlayController` switches the tab sprites when the selected page
changes. Text, portrait crops, button events, and navigation remain Unity UI
elements so they can be edited independently from the artwork.
