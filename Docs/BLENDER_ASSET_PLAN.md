# One Valley — Blender asset plan

Written 23 Aug 2026. This is the work queue for `blender-mcp`, kept in the project so it
survives a Claude Code restart.

## Where things stand

Everything in the valley is a Unity primitive. Gameplay is finished and verified: three
distinct attack shapes, essence economy, four-style visual lens, custom procedural rock
shader (`Assets/Shaders/ProceduralRock.shader`), full post-processing stack
(`Assets/Scripts/ValleyAtmosphere.cs`). The scene is rebuilt entirely from
`Assets/Scripts/ValleyBuilder.cs`, so swapping primitives for real meshes is a change in
one file.

## Step 0 — probe before building

`blender-mcp` ships a Poly Haven integration (free CC0 assets). Check whether it is
enabled first. Download anything where realism matters; generate anything that has to fit
the valley's exact dimensions.

## What to build

| Asset | Count | Replaces | Tri budget |
|---|---|---|---|
| Valley terrain | 1 | the flat `Ground` box | ~25k |
| Cliff wall segments | 4 variants | the four `Cliff*` boxes | ~2k each |
| Boulders | 6 variants | `Pillar1`–`Pillar4`, narrows shoulders | ~600 each |
| Rubble / debris | 5 variants | new — scatter detail | ~150 each |
| The Gate | 1 | the `TheGate` box | ~3k |
| Shrine | 1 | the `ShrineOfEssence` box | ~1.5k |
| Essence crystal | 1 | the pickup cube in `EssencePickup.cs` | ~200 |
| Dead trees | 3 variants | new — silhouette interest | ~800 each |

Roughly 60k triangles total.

## Generation technique per family

Everything here is chosen because the result follows from rules rather than from
eye-judgement. That is the whole reason these are viable to author blind.

- **Boulders / rubble** — icosphere, Displace modifier driven by Voronoi and Clouds
  textures at two frequencies, Decimate to budget, shade flat. Random seed per variant.
- **Cliff segments** — subdivided cube, layered noise displacement biased along the
  horizontal axes so vertical faces stay readable, Solidify, Decimate. Built as modular
  tiles that repeat with random rotation, so four meshes cover 160 m of wall.
- **Terrain** — subdivided grid, multi-octave noise displacement multiplied by a falloff
  function that raises the edges and keeps the centre walkable. The valley shape comes
  from the falloff, not from sculpting.
- **Gate and shrine** — parametric hard-surface: primitives, boolean cuts, bevels, arrays.
- **Crystals** — cone/prism with randomised facet scaling.
- **Dead trees** — small L-system: trunk, recursive branch splits with angle and taper
  falloff. Highest risk of the set. Drop it rather than burn iterations if the first pass
  looks wrong.

## Integration rules

- **Axis conversion**: export FBX with `axis_forward='-Z'`, `axis_up='Y'`, Apply Transform.
  Blender is Z-up and Unity is Y-up; without this everything arrives lying on its side.
- **Units**: both default to 1 unit = 1 metre. No scaling needed.
- **Export straight into** `Assets/Resources/Models/` so Unity auto-imports and
  `ValleyBuilder` can load prefabs at runtime with `Resources.Load<GameObject>()`. This
  keeps the rebuild-from-script workflow intact.
- **No Blender materials.** They do not survive into URP cleanly. Export geometry only and
  assign `OneValley/ProceduralRock` in Unity — that also keeps the meshes compatible with
  the style lens instead of breaking it.
- **Colliders**: mesh colliders on terrain and cliffs (static, non-convex); convex mesh
  colliders on boulders; **no collider** on rubble or trees, because invisible obstacles
  that block a sword swing are worse than no collision at all.
- **Variety**: random rotation and ±15% scale per placed instance, so six boulder meshes
  read as thirty rocks.

## Order of work

1. Terrain — largest thing on screen, largest payoff
2. Cliffs
3. Boulders into the narrows
4. Rubble scatter pass
5. Gate and shrine
6. Crystals, then trees if they land well

Screenshot after each stage so the user can veto early rather than after placement.

## Explicitly NOT built in Blender

The characters — Grunt, Darter, Warden, player. A rock is math; a face is judgement, and
the MCP iteration loop is roughly 300x slower than an artist's hand-eye loop, which is
fatal for aesthetic work and irrelevant for procedural work.

Characters come from **Mixamo** (free, rigged, animated). That download is still needed and
Blender does not remove it.
