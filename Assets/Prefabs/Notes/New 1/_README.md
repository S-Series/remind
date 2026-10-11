# Box Note prototype

`BoxNote.prefab` is a separate visual prototype. The existing InGame/Preview Air
prefabs, `AirNoteFourFaceView`, and atlas shader remain the legacy implementation;
the Game and ChartMaker references have not been switched to this prototype.

The Body uses Unity's shared unit Cube mesh and `BoxNote.mat`. Its local X/Y/Z
scale controls width/thickness/depth before the -90 degree X rotation places
the top in the chart's XY plane. There is no per-note mesh/material creation or
Update loop. The shader computes face gradients, borders and HDR emission. Border
and inner glow widths use world units. The shader reads the full object-to-world
transform, including parent scale and shear from rotated children, and measures
perpendicular distances to each edge. No material scale parameters are needed.
Perspective, Bloom and subpixel antialiasing can still change the apparent screen
thickness. Image details stretch with each face. The default fill is transparent.

## Face textures

Assign three Texture2D image assets directly on `BoxNote.mat`:

| Slot | Body local faces |
| --- | --- |
| Top | Top and bottom (+Y / -Y) |
| Front | Front and back (-Z / +Z) |
| Side | Left and right (-X / +X) |

Each whole image stretches over its two faces. Opposite faces are oriented for
viewing from outside the box. Texture RGB/alpha multiplies the existing face
color/gradient; set the face colors to white for the original image colors.
The material's border and glow remain on top. Empty texture slots use white,
preserving the procedural fill. Use a separate image file for each face type;
this shader does not extract sliced Sprite regions from an atlas.

The six faces use one Cube MeshRenderer and the shared material. Face assignment
needs no runtime script or MaterialPropertyBlock. Identical notes share the
material's images and settings, keeping the shader eligible for the SRP Batcher.
Dense gameplay still needs measurement before choosing a renderer migration.

The Temporary Star child uses the existing `SettingsUI/small_star.png` sprite
and a shared unlit material. Replace its SpriteRenderer's Sprite with the final
transparent star PNG. Keep the root at unit scale and adjust the Body's X scale
to change width without stretching the star. This prefab contains only rendering
components; gameplay and ChartMaker selection integration is a later step.

Use `ReMind/Notes/Create Box Note Prototype` to create the prefab if missing.
Existing prefab edits and material settings are preserved.
`ReMind/Notes/Capture Box Note Prototype`
renders three widths into `Logs/BoxNotePreview/preview.png` and checks mesh/material
sharing, shader errors and Bloom activation. Its temporary camera uses HDR
and Bloom; another scene needs its own camera post-processing and Bloom settings
for the same light spread.

`BoxNoteSurfaceValidation.Run` checks all six outward-facing views with directional
texture fixtures, material texture replacement, empty slots and script-free rendering.
The images are written to `Logs/BoxNotePreview/Surfaces/`.
`BoxNoteSurfaceValidation.RunBorderScaleValidation` checks a 0.05 world-unit border
under different Body scales, nonuniform parent scale and rotated children.

This is a visual prototype, not a completed pooling, visible-range culling or
instanced rendering system. Measure dense gameplay before choosing that next step.
