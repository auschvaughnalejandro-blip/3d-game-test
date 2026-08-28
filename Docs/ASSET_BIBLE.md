# One Valley — the complete asset list

Every image that needs to be generated, what it is for, where it goes, and the prompt to
generate it with.

Written 24 Aug 2026 against the game as it actually stands. Nothing in this list is
speculative: every entry corresponds to a real surface, character, prop or panel that the
code builds today. Where something is currently a flat colour or a grey box, that is said
plainly.

**112 assets** - 104 specified individually, plus 8 per-lens variants covered by two prompt patterns at the end. Read Part 0 before generating anything — it is the difference between one
game and a hundred unrelated pictures.

---

# PART 0 — READ THIS FIRST

## 0.1 The four roles an image can have

Every entry is tagged with one of these. They are used completely differently.

| Tag | What it is | What happens to it |
|---|---|---|
| **TEX** | A material surface — stone, cloth, metal | Goes into `Assets/Resources/Textures/<Set>/` and is loaded by the game directly |
| **REF** | Concept art / orthographic turnaround | Never ships. Loaded into Blender as a background image and modelled over |
| **UI** | A panel, button, icon, logo | Ships as-is, drawn by the HUD code |
| **FX** | A particle, glow, decal, trail | Ships as-is, usually needs its background keyed out |

A REF image is the one people get wrong: you are not generating the sword, you are
generating the *drawing of the sword* that you model from. Ask for flat orthographic
views on a plain background, never a dramatic render.

## 0.2 What ChatGPT image generation cannot do, and what to do about it

Be honest about this up front or you will waste days.

**It cannot make seamlessly tileable textures reliably.** It will say it has. It has not.
Fix: generate at 2048×2048, then in GIMP (free) run `Filters → Map → Offset` at half the
width and height, and heal the visible seam cross with the clone tool. Ten minutes per
texture. Alternatively use each texture as a *unique* surface on a large object where the
seam never repeats, which works for the Vault and the dungeon walls.

**It cannot output an alpha channel.** Everything comes back opaque.
Fix: ask for the subject on **pure black** for anything glowing (flames, essence, portal
surfaces — these use additive blending, where black *is* transparent), and on **pure
magenta `#FF00FF`** for anything with a hard edge (icons, UI). Key the magenta out in
GIMP with `Colour → Colour to Alpha`.

**It cannot make normal, roughness or ambient-occlusion maps.** It will produce something
purple that looks like a normal map and is not.
Fix: the game loads `_albedo`, `_normal`, `_rough`, `_ao` per texture set
(`ValleyBuilder.cs:1833`). Generate only the **albedo**. Derive the other three:
- **Normal** — Blender: image → `Bump` node → `Normal Map`, or bake. Free standalone:
  Materialize, or `NormalMap-Online` in a browser.
- **Roughness** — desaturate the albedo, invert, raise contrast. Ten seconds in GIMP.
- **AO** — usually skippable. Leave the file out; the loader handles a missing map.

**It cannot make a font.** It can make a wordmark — one fixed image of the words "ONE
VALLEY" — which is what UI-25 is. Body text stays as the built-in font.

**It cannot reliably match a previous image's style from memory.** Hence 0.3.

## 0.3 THE STYLE PREAMBLE — paste this before every single prompt

This is the most important paragraph in the document. Without it you get 131 images from
131 different games.

> **STYLE:** Stylised low-fantasy realism for a 3D game. Weathered hand-cut stone, not
> ornate high fantasy. Cold grey-violet rock, warm orange firelight, and a single magical
> violet that belongs only to the Vault. Muted, desaturated base palette with saturated
> light sources. Strong readable silhouettes and clear large forms — small fussy detail
> is lost at gameplay distance. No text, no watermarks, no logos, no UI elements, no
> people unless asked for. Even neutral lighting unless the prompt says otherwise.

For character and prop REF sheets, add:

> **LAYOUT:** Orthographic technical reference sheet. Front, side and back views in a row,
> same scale, aligned on the same baseline. Plain flat mid-grey background. Even flat
> lighting with no cast shadows and no dramatic rim light. T-pose or neutral stance. This
> is a modelling reference, not an illustration.

## 0.4 The palette — use these hex values by name in prompts

Taken from the actual materials in the code, so generated art will match what is already
on screen.

| Name | Hex | Where it comes from |
|---|---|---|
| Valley stone | `#4C4750` | Narrows and arena rock |
| Dungeon stone | `#54505C` | `DungeonBuilder` walls |
| Dungeon floor | `#42403B` | `DungeonBuilder` floor |
| Cliff grey | `#665F5C` | Cliff faces |
| Vault violet | `#8C38F2` | Portal surface, crystals, the Warden's core |
| Vault violet (bright) | `#B26BFF` | Portal glow, the Edge |
| Essence teal | `#6BF2D6` | Essence shards, the shrine |
| Firelight orange | `#FF9438` | Braziers |
| Homeward green | `#80FFD9` | The way out of the Vault — the ONLY green light in the game |
| Grunt hide | `#6B5B47` | Grunt body |
| Darter hide | `#8C5238` | Darter body |
| Spitter hide | `#5C8042` | Spitter body |
| Warden iron | `#2B2B33` | Warden plating |
| Player cloth | `#4D6B9E` | Player tunic |
| Enemy eye | `#FFD94D` | The glow every enemy has |
| Blood/damage red | `#D13840` | Health bar, hit flash |

## 0.5 The constraint nobody expects: silhouette is everything

The game has four lenses (`StyleLens.cs`). Two of them — **NEON** and **CHALK** — draw
every object as **flat unlit colour with no shading at all**. All surface detail, every
normal map, every painted highlight, vanishes completely in those two styles.

What survives is the **outline**.

So: if a shape is not identifiable from its silhouette alone, it will be unidentifiable
in half the game's visual styles. When reviewing any generated character or prop, squint
until it is a black shape. If you cannot tell what it is, regenerate it.

This is also why the prompts below ask for *bold separated forms* rather than fine detail.

## 0.6 Where files go

```
Assets/Resources/Textures/<SetName>/<SetName>_albedo.jpg
                                    <SetName>_normal.jpg
                                    <SetName>_rough.jpg
                                    <SetName>_ao.jpg
Assets/Resources/Models/<Name>.fbx          <- Blender exports
Assets/Resources/UI/<Name>.png              <- new folder, needs creating
Assets/Resources/FX/<Name>.png              <- new folder, needs creating
```

The texture loader is `ValleyBuilder.MakeTexturedRockMaterial`. A missing map is handled
gracefully, so albedo alone is enough to see results immediately.

## 0.7 Suggested order of work

If time runs out, run out of it at the bottom of this list, not the top.

1. **UI-20 to UI-31** — the title screen and menus. First thing anybody sees, ships as-is, no Blender needed.
2. **CHR-01 to CHR-06** — the Warden and the three enemy types. Most screen time.
3. **TEX-10 to TEX-17** — the dungeon. First ninety seconds of the game and currently flat colour.
4. **WPN-01 to WPN-08** — the four weapons. Held in front of the camera the whole game.
5. **CHR-07 to CHR-09** — Orrin. On screen for two long conversations.
6. **UI-01 to UI-19** — the HUD.
7. **FX-01 to FX-14** — effects.
8. **TEX-01 to TEX-09, TEX-18 to TEX-26** — the valley and Vault. Already textured; improvement rather than absence.
9. **SKY-01 to SKY-05**.
10. **LENS-01 to LENS-08** — per-lens variants. Pure upside, cut first.

---

# PART 1 — ENVIRONMENT TEXTURES

## The valley

### TEX-01 — Valley ground `[TEX]`
**Where:** `ValleyBuilder.BuildGroundAndCliffs`, texture set `Terrain`. **Already exists**
(Poly Haven CC0) — regenerate only if you want it to match the rest more closely.
**Output:** 2048², tileable albedo → `Textures/Terrain/Terrain_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable top-down texture of a high mountain valley floor:
> dry pale grass in patches over compacted grey-brown earth and scattered flat stones.
> Sparse, windswept, cold climate. Muted olive and grey-brown, no bright green. Even
> overhead light, no shadows cast across the tile. Photographic detail, top-down flat
> view, no perspective.

### TEX-02 — Cliff face `[TEX]`
**Where:** all four cliff walls, the Narrows shoulders, barriers, pillars, portal frame.
Texture set `Cliff`. **Already exists.**
**Output:** 2048², tileable albedo → `Textures/Cliff/Cliff_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a sheer natural cliff face: horizontally
> bedded grey stone with deep vertical cracks and crumbling ledges. Cold grey `#665F5C`
> with faint rust-brown mineral staining. Strong large forms, deep crevices. Flat even
> lighting, viewed straight on, no perspective.

### TEX-03 — Arena floor `[TEX]`
**Where:** `BuildTheHollow` → `HollowFloor`, the circular platform every round is fought
on. Currently procedural rock, no texture.
**Output:** 2048², tileable albedo → `Textures/ArenaFloor/ArenaFloor_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a large fitted flagstone floor of an
> ancient open-air arena. Big irregular slabs of dark grey-violet stone `#4C4750`, tight
> mortar lines, worn smooth in the centres, chipped at the edges. Faint old scorch marks
> and scuffing. Flat even lighting, top-down, no perspective, no cast shadows.

### TEX-04 — The Gate `[TEX]`
**Where:** `BuildTheHollow` → `TheGate`, the ten-metre slab sealing the north end that
lifts at the very end of the game.
**Output:** 2048², tileable albedo → `Textures/Gate/Gate_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a colossal sealed stone door: a single
> slab of near-black basalt with heavy horizontal iron banding, huge rivets, and deep
> tool marks. Grim and impassable, no ornament, no carvings, no symbols. Flat even
> lighting, straight-on view, no perspective.

### TEX-05 — Narrows shoulder rock `[TEX]`
**Where:** `BuildTheNarrows` → `NarrowsWestShoulder`, `NarrowsEastShoulder`, `Pillar1-4`.
The raised rock the Spitters stand on.
**Output:** 2048², tileable albedo → `Textures/Shoulder/Shoulder_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a weathered rock outcrop shoulder: layered
> grey stone with a thin crust of dry lichen and gravel on the upper surfaces. Slightly
> warmer and more broken than a sheer cliff. Flat even lighting, no perspective.

### TEX-06 — Rising cover pillar `[TEX]`
**Where:** `Pillar.cs` — the stone columns that rise between rounds and are shattered by
the Warden.
**Output:** 1024², tileable albedo → `Textures/CoverPillar/CoverPillar_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture wrapping a squat cylindrical stone column:
> roughly dressed grey-violet stone, vertical chisel marks, chipped edges, a band of
> darker weathering around the base. Flat even lighting, no perspective.

### TEX-07 — Zone barrier `[TEX]`
**Where:** `ZoneBarrier.cs` — the walls that rise to seal a zone during a round.
**Output:** 1024², tileable albedo → `Textures/Barrier/Barrier_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a mechanism-driven stone barrier wall:
> interlocking grey blocks with visible sliding joints and grooves worn by repeated
> movement, faint teal mineral residue in the seams. Flat even lighting, no perspective.

### TEX-08 — The road north `[TEX]`
**Where:** `BuildTheRoadNorth` → `RoadNorth`, the closing walk of the game.
**Output:** 2048², tileable albedo → `Textures/Road/Road_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of an ancient paved mountain road: irregular
> fitted cobbles worn into two smooth wheel-ruts, dust and fine gravel gathered between
> the stones, thin grass in the cracks at the edges. Warm grey-brown. Flat even lighting,
> top-down, no perspective.

### TEX-09 — Road cliff walls `[TEX]`
**Where:** `RoadWallWest`, `RoadWallEast` — the pass the ending walks through.
**Output:** 2048², tileable albedo → `Textures/RoadWall/RoadWall_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a cut rock pass wall: stone that has
> clearly been quarried through by hand, long parallel chisel scars, warmer and lighter
> than natural cliff, catching low sun. Suggests a way out rather than a boundary. Flat
> even lighting, straight-on, no perspective.

## The dungeon — highest priority, currently all flat colour

### TEX-10 — Dungeon wall `[TEX]`
**Where:** `DungeonBuilder.BuildTheShell` — every wall. Currently flat `#54505C`.
**Output:** 2048², tileable albedo → `Textures/DungeonWall/DungeonWall_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of an old underground hall wall built by
> hand: large squared grey-violet blocks `#54505C` in uneven courses, deep dark mortar
> lines, patches of pale salt bloom and damp staining low down. Faint soot near the top.
> No carvings, no runes, no symbols. Flat even lighting, straight-on, no perspective.

### TEX-11 — Dungeon floor `[TEX]`
**Where:** `DungeonFloor`. Currently flat `#42403B`.
**Output:** 2048², tileable albedo → `Textures/DungeonFloor/DungeonFloor_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a worn underground stone floor: large dark
> slabs polished smooth down the middle by centuries of walking, rougher and dustier at
> the edges, fine grit in the joints. Dark warm grey `#42403B`. Flat even lighting,
> top-down, no perspective, no cast shadows.

### TEX-12 — Dungeon ceiling `[TEX]`
**Where:** `DungeonCeiling`.
**Output:** 2048², tileable albedo → `Textures/DungeonCeiling/DungeonCeiling_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a low vaulted undercroft ceiling seen from
> below: rough stone blocks, shallow brick arching, heavy black soot staining in broad
> patches from centuries of torches. Darker than the walls. Flat even lighting, viewed
> straight up, no perspective.

### TEX-13 — Dungeon pillar `[TEX]`
**Where:** `BuildThePillars` — six square columns down the hall.
**Output:** 1024², tileable albedo → `Textures/DungeonPillar/DungeonPillar_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture wrapping a square stone pillar: stacked
> dressed blocks with a simple chamfered edge, soot darkening on one face, chipped
> corners. Plain and structural, no decoration. Flat even lighting, no perspective.

### TEX-14 — Dungeon steps `[TEX]`
**Where:** `DungeonStep0-2`, leading up to the door out.
**Output:** 1024², tileable albedo → `Textures/DungeonStep/DungeonStep_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of worn stone stair treads: each step dished
> in the middle from footfall, rounded front edges, grit collected in the corners. Flat
> even lighting, top-down, no perspective.

### TEX-15 — Dungeon doorway frame `[TEX]`
**Where:** `DoorPostWest`, `DoorPostEast`, `DoorHead`, `DungeonDoorLintel`.
**Output:** 1024², tileable albedo → `Textures/DoorFrame/DoorFrame_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a heavy dressed-stone door surround: paler
> and more finely finished than the surrounding wall, a simple recessed border moulding,
> slight violet mineral discolouration in the recesses as though something magical has
> been leaning against it for a very long time. Flat even lighting, no perspective.

### TEX-16 — Brazier iron `[TEX]`
**Where:** `BrazierStem`, `BrazierBowl` in the dungeon.
**Output:** 1024², tileable albedo → `Textures/BrazierIron/BrazierIron_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of old hand-forged black iron: hammered
> uneven surface, dark grey-black, edges worn to bare bright metal, patches of dull rust
> and heavy soot. Flat even lighting, no perspective.

### TEX-17 — Dungeon door backing stone `[TEX]`
**Where:** `DungeonDoorBacking` — the rock visible through the doorway before it opens.
**Output:** 1024², tileable albedo → `Textures/DoorBacking/DoorBacking_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of raw unworked bedrock sealing a passage:
> rough broken stone face, clearly natural rather than cut, darker and colder than the
> dressed walls around it. Flat even lighting, straight-on, no perspective.

## The Vault

### TEX-18 — Vault wall and dome `[TEX]`
**Where:** `BossArena.fbx`, currently using the Cliff set.
**Output:** 2048², tileable albedo → `Textures/VaultStone/VaultStone_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of the inside of a sealed underground vault:
> enormous precisely fitted dark stone blocks, far better made than anything else in the
> world, hairline joints, faint violet light bleeding out of the cracks as though
> something is contained behind them. Cold, deliberate, built to hold something in. Flat
> even lighting, straight-on, no perspective.

### TEX-19 — Vault floor `[TEX]`
**Where:** the Warden fight floor.
**Output:** 2048², tileable albedo → `Textures/VaultFloor/VaultFloor_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a sealed vault floor: dark polished stone
> in a huge concentric ring pattern, deep gouges and impact craters scored across it,
> faint violet residue collected in the damage. Something enormous has been pacing here
> for a very long time. Flat even lighting, top-down, no perspective.

### TEX-20 — Vault crystal `[TEX]`
**Where:** `BossArenaCrystals.fbx`, the room's own light source.
**Output:** 1024², tileable albedo on **pure black** → `Textures/VaultCrystal/VaultCrystal_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of glowing violet crystal `#8C38F2`: sharp
> angular facets, internal light strongest at the core and fading to translucent at the
> edges, fine internal fractures catching the glow. On a pure black background. Emissive,
> self-lit, no external light source, no shadows.

### TEX-21 — Vault pillar `[TEX]`
**Where:** `VaultPillar.fbx` — the cover the Warden shatters.
**Output:** 1024², tileable albedo → `Textures/VaultPillar/VaultPillar_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture wrapping a tall dark vault column: black
> fitted stone with thin violet seams running vertically through it like veins, already
> cracked and stressed as though it will not survive much more. Flat even lighting, no
> perspective.

## Portals and the shrine

### TEX-22 — Portal frame `[TEX]`
**Where:** `Portal.fbx` — the arch that rises after round four.
**Output:** 1024², tileable albedo → `Textures/PortalFrame/PortalFrame_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of an ancient standing-stone arch: rough grey
> megalithic rock, heavily violet-stained around the inner edge where energy has been
> passing through it for centuries, the stain eating into the stone. Flat even lighting,
> no perspective.

### TEX-23 — Portal surface `[FX]`
**Where:** `PortalSurface.fbx` — the shimmering plane, rotated and pulsed by `Portal.cs`.
**Output:** 1024×1024 on **pure black** → `FX/PortalSurface.png`
**Prompt:**
> [STYLE PREAMBLE] A square seamless texture of a vertical sheet of violet magical energy:
> slow concentric ripples from the centre outward, brightest `#B26BFF` at the middle
> fading to deep `#8C38F2` at the edges, fine filaments of light, faintly translucent. On
> a pure black background. Emissive and self-lit, no external lighting, no frame, no
> border, no objects.

### TEX-24 — Homeward portal surface `[FX]`
**Where:** `HomewardSurface` — the way out of the Vault. **Must read as the opposite of
the Vault:** it is the only green-white light in the game.
**Output:** 1024×1024 on **pure black** → `FX/HomewardSurface.png`
**Prompt:**
> [STYLE PREAMBLE] A square seamless texture of a vertical sheet of pale green-white magical
> energy `#80FFD9`: calm slow vertical drift rather than ripples, soft and welcoming,
> like daylight seen through water. On a pure black background. Emissive and self-lit, no
> external lighting, no frame, no objects.

### TEX-25 — Shrine of Essence `[TEX]`
**Where:** `BuildTheShrine` — currently a glowing teal cube.
**Output:** 1024², tileable albedo → `Textures/Shrine/Shrine_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a shrine obelisk: dark stone with a
> lattice of glowing teal `#6BF2D6` veins running through it like circuitry grown by
> nature, brightest where the veins meet. Faint carved channels guiding the light
> downward. Flat even lighting on the stone, self-lit veins.

### TEX-26 — Shrine base `[TEX]`
**Where:** `ShrineBase` — the low disc it stands on.
**Output:** 1024², tileable albedo → `Textures/ShrineBase/ShrineBase_albedo.jpg`
**Prompt:**
> [STYLE PREAMBLE] A seamless tileable texture of a circular stone shrine platform: worn dark
> flagstones in a radial pattern, teal mineral residue built up in the joints nearest the
> centre, the stone bleached pale where people have stood. Flat even lighting, top-down,
> no perspective.

---

# PART 2 — CHARACTERS

All of these are **REF** sheets — modelling references for Blender, not final art. Use the
LAYOUT addition from 0.3 on every one.

### CHR-01 — The Warden, full turnaround `[REF]`
**Where:** `Warden.fbx`, `WardenBoss.cs`. The boss. Three phases: charging, throwing,
summoning. Roughly three times a human in height.
**Prompt:**
> [STYLE PREAMBLE] [LAYOUT] A colossal armoured stone-and-iron guardian, three times human
> height, built to be imprisoned rather than to serve. Broad hunched shoulders, long heavy
> arms ending in blunt crushing fists, a short thick neck and a featureless helm-like head
> with a single horizontal slot. Body of dark iron plating `#2B2B33` bolted over cracked
> grey stone, with violet `#8C38F2` light burning out from between the plates at the chest
> and joints. Massive, slow, immensely strong. Bold simple readable silhouette, no fine
> ornament.

### CHR-02 — The Warden, phase detail sheet `[REF]`
**Where:** `WardenBoss.cs` — phase 2 opens up, phase 3 opens further.
**Prompt:**
> [STYLE PREAMBLE] A three-panel comparison sheet of the same colossal armoured guardian in
> three states of damage, side by side, identical pose and scale, plain grey background.
> Panel 1: intact, plating closed, faint violet glow in the seams. Panel 2: plating
> cracked and lifting at the chest, violet light much brighter and spilling out. Panel 3:
> plating shattered and hanging, the chest core fully exposed and blazing violet, stone
> body visibly breaking apart. Flat even lighting, no cast shadows.

### CHR-03 — The Warden's chest core `[REF]`
**Where:** where `WardenGem.SpawnAt` drops the eye. This is what the player is hitting.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a large violet crystalline core set deep in a
> broken iron chest cavity, front and side view, plain grey background. A single faceted
> violet stone `#8C38F2` held in an iron cradle by four heavy clamps, cracked casing
> around it, light spilling from the fractures. Flat even lighting.

### CHR-04 — Grunt `[REF]`
**Where:** `Grunt.fbx`, `MakeGrunt`. Slow, heavy, does not flinch. Body height 1.9m.
Carries a club. Glowing yellow eyes.
**Prompt:**
> [STYLE PREAMBLE] [LAYOUT] A slow heavy brute creature for a fantasy game, slightly taller
> than a man and much broader. Thick sloped shoulders, short neck, long heavy arms, squat
> powerful legs. Coarse hide the colour of old leather `#6B5B47`, cracked and calloused
> into thick plates over the shoulders and back. Small deep-set eye sockets with a hot
> yellow `#FFD94D` glow. Blunt, patient, immovable. Bold simple silhouette, low detail.

### CHR-05 — Darter `[REF]`
**Where:** `Darter.fbx`, `MakeDarter`. Fast, commits to a charge and cannot turn.
Retreats after attacking.
**Prompt:**
> [STYLE PREAMBLE] [LAYOUT] A fast lean predatory creature for a fantasy game, slightly
> shorter than a man, built entirely for one straight-line lunge. Long powerful digitigrade
> hind legs, forward-leaning body, narrow head, small forelimbs held tight to the chest.
> Taut rust-red hide `#8C5238` over visible ribs and tendons. Hot yellow `#FFD94D` eye
> glow. Nervous, twitchy, all forward momentum. Bold simple silhouette, unmistakably
> different from a heavy brute.

### CHR-06 — Spitter `[REF]`
**Where:** `MakeSpitter`. **CURRENTLY REUSES THE GRUNT MODEL** — `ValleyBuilder.cs:1520`
passes `"Grunt"` as its model name. This is the single most valuable character asset in
the list: right now the ranged enemy is visually identical to the melee one, which makes
round three read as unfair rather than as a new lesson.
**Prompt:**
> [STYLE PREAMBLE] [LAYOUT] A ranged thrower creature for a fantasy game, roughly human
> height but hunched and asymmetric. One enormously overdeveloped throwing arm far larger
> than the other, a heavy counterweighted tail, and a wide flat head with a large single
> eye. Mottled green-grey hide `#5C8042`. A pouch or sling of rocks slung at the hip.
> Reads instantly as "throws things from a distance" and must be impossible to confuse
> with a heavy melee brute. Bold asymmetric silhouette.

### CHR-07 — Orrin, the Lensmaker `[REF]`
**Where:** `DungeonBuilder.MakeOrrinModel`, `Wizard.cs`. Currently twelve stacked
cylinders. He stands, never walks, and is on screen for both long conversations. About
2.2m tall including the hood.
**Prompt:**
> [STYLE PREAMBLE] [LAYOUT] An old wizard standing still, tall and thin, in heavy layered
> robes that fall all the way to the floor and hide his feet completely. Deep hood pushed
> back off his face so his face is visible. Long deep-violet outer robe `#564785` over a
> paler underlayer, a simple pale trim at the collar, no ornament, no jewellery, no stars
> or moons. Weathered lined face, close-cropped white beard, tired and patient rather than
> wise and twinkling. He has been waiting eleven years. Holds a plain wooden staff. Bold
> simple silhouette dominated by the fall of the robe.

### CHR-08 — Orrin's face, close `[REF]`
**Where:** he is at conversation distance for four minutes of a ten-minute demo.
**Prompt:**
> [STYLE PREAMBLE] An orthographic head reference sheet of an old man, front and side view,
> plain grey background, flat even lighting. Late sixties, gaunt, deeply lined, close
> white beard, hollow cheeks, heavy brows. An expression of patient guilt — a man asking
> someone else to fix what he broke. No hood, no hat, neutral expression, mouth closed.

### CHR-09 — Orrin's staff and orb `[REF]`
**Where:** `OrrinStaff`, `OrrinOrb` — the orb is his light source and it breathes.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a plain wooden wizard's staff, full length
> and a detail of the top, plain grey background. Unornamented dark wood, worn smooth
> where it is gripped, slightly bent. At the top, held in a simple three-pronged iron
> cradle, a glowing violet orb `#B26BFF` the size of a fist. Humble and functional, made
> by a craftsman not a king. Flat even lighting.

### CHR-10 — The player `[REF]`
**Where:** `Player.fbx`. Third-person, seen from behind almost the whole game. 1.77m.
**Prompt:**
> [STYLE PREAMBLE] [LAYOUT] A lightly armoured traveller for a third-person fantasy game,
> ordinary human build, mid-twenties, gender-neutral. Blue-grey padded tunic `#4D6B9E`
> over dark trousers, worn leather belt and boots, a single shoulder guard on the right,
> no helmet, hair tied back. Practical, travel-worn, not heroic and not ornate. Must read
> clearly from BEHIND: distinctive shoulder line and back detail, since that is the view
> for the entire game. Bold simple silhouette.

### CHR-11 — The player, back view emphasis `[REF]`
**Where:** the actual gameplay camera angle.
**Prompt:**
> [STYLE PREAMBLE] A single large orthographic BACK view of the lightly armoured traveller
> from CHR-10, plain grey background, flat even lighting. Focus entirely on what is
> visible from behind at eight metres: the shoulder guard, the belt, the fall of the
> tunic, the boots. Clear separated shapes, strong readable outline, no fine detail.

---

# PART 3 — WEAPONS AND PROJECTILES

All currently built from grey primitive cubes in `BuildThePlayer`. All are held in front
of the camera constantly.

### WPN-01 — Sword `[REF]`
**Where:** `SwordBlade`, `SwordGuard`. Fast, 2.6m reach, 100° arc.
**Prompt:**
> [STYLE PREAMBLE] An orthographic weapon reference sheet, plain grey background, flat even
> lighting: a plain arming sword, side view and edge-on view. Straight double-edged steel
> blade about 80cm, simple straight crossguard, leather-wrapped grip, plain disc pommel.
> Well used, slightly nicked at the edge, no engraving, no jewels, no glow. An honest
> functional weapon.

### WPN-02 — Hammer `[REF]`
**Where:** `HammerHaft`, `HammerHead`, `HammerCollar`. Slow, heavy, 3.4m reach.
**Prompt:**
> [STYLE PREAMBLE] An orthographic weapon reference sheet, plain grey background, flat even
> lighting: a heavy two-handed war maul, side and front view. Short thick wooden haft
> bound with iron, an enormous blunt rectangular iron head with a reinforcing collar where
> it meets the haft. Battered, chipped, obviously enormously heavy. Silhouette must read
> instantly as "slow and crushing" beside a sword.

### WPN-03 — Bow `[REF]`
**Where:** `BowGrip`, `BowUpperLimb`, `BowLowerLimb`, `BowString`. Held across the body.
**Prompt:**
> [STYLE PREAMBLE] An orthographic weapon reference sheet, plain grey background, flat even
> lighting: a plain recurve hunting bow, side view unstrung and strung. Dark laminated
> wood limbs, leather-wrapped grip, pale twisted string. Practical hunting gear, no
> decoration, no carving.

### WPN-04 — The Warden's Edge `[REF]`
**Where:** `EdgeBlade`, `EdgeCore`, `EdgeGuard`, `EdgeGrip`. The reward weapon. 5m reach,
200° arc, glows violet. **Must look obviously superior to the sword at a glance.**
**Prompt:**
> [STYLE PREAMBLE] An orthographic weapon reference sheet, plain grey background: a long
> two-handed greatsword made of the same material as a sealed vault. Blade about 1.4m,
> dark iron with a channel of blazing violet `#B26BFF` crystal running down its centre
> from guard to tip. Heavy angular crossguard with a violet stone set in it. The blade
> looks grown rather than forged, faceted like crystal along the edges. Self-lit and
> emissive along the core channel while the metal stays dark. Clearly a far greater weapon
> than a plain sword, without being ornate.

### WPN-05 — Warden's Edge glow map `[FX]`
**Where:** the emissive channel of WPN-04.
**Output:** on **pure black** → `FX/EdgeGlow.png`
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a long narrow vertical channel of blazing
> violet-white light `#B26BFF`, brightest at the centre line and falling off sharply to
> either side, with fine crystalline fractures branching from it. Emissive, self-lit, no
> object, no metal, no background detail.

### WPN-06 — Arrow `[REF]`
**Where:** `Arrow.cs` — currently a cylinder. Falls under gravity, aimed above targets.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a single hunting arrow, side view, plain grey
> background, flat even lighting. Straight wooden shaft, simple leaf-shaped iron
> broadhead, three grey goose-feather fletchings. Plain, functional, no decoration.

### WPN-07 — Spitter's rock `[REF]`
**Where:** `Projectile.cs` — currently a brown sphere. Slow, must be visibly dodgeable.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a single thrown rock, three angles, plain
> grey background. A rough angular chunk of grey-brown stone about the size of a head,
> chipped and irregular, with faint hot orange cracks through it as though it has been
> heated. Must read as dangerous and slow-moving, not as scenery.

### WPN-08 — Grunt's club `[REF]`
**Where:** `GruntClub.fbx`.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a crude two-handed club, side view, plain
> grey background. A single thick length of dark hardwood, wider and heavier at the
> striking end, bound with rough cord at the grip, studded with a few driven iron nails.
> Made by something with no craft. Bold heavy silhouette.

---

# PART 4 — PROPS AND PICKUPS

### PROP-01 — Essence shard `[REF]`
**Where:** `EssencePickup.SpawnAt` — currently a small teal cube. The player collects
dozens of these.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a small floating crystal shard, three
> angles, plain black background. A sharp angular teal `#6BF2D6` crystal about the size of
> a thumb, translucent, glowing from within, with two or three tiny fragments orbiting it.
> Emissive and self-lit. Reads instantly as "pick me up".

### PROP-02 — The Warden's Eye `[REF]`
**Where:** `WardenGem.SpawnAt` — the reward pickup. The emotional centre of Act III.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a single large magical gemstone, front and
> side view, plain black background. A faceted violet `#8C38F2` stone the size of two
> fists, cut like an eye with a darker vertical slit at its centre, blazing with internal
> light, hairline fractures across the surface. Ancient and slightly wrong to look at.
> Emissive and self-lit.

### PROP-03 — Brazier `[REF]`
**Where:** `MakeOneBrazier` — four in the dungeon, eight in the Vault.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a floor-standing iron brazier, front and side
> view, plain grey background, flat even lighting. A shallow wide bowl on a slender
> three-legged stand, hand-forged black iron, hammer marks visible, rim warped by heat,
> heavy soot staining. Empty — no fire in this image. Simple and old.

### PROP-04 — Health potion `[REF]`
**Where:** `PlayerHealing.cs` — three charges, refilled between rounds. Currently no
model at all, only a HUD number.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of a small glass healing flask, front and side
> view, plain grey background. Squat thick-walled glass bottle, cork stopper sealed with
> wax, a leather carry-strap, filled with a deep red liquid that glows very faintly. Worn
> and practical, not ornate.

### PROP-05 — Dungeon door slab `[REF]`
**Where:** `DoorSurface` — the sealed door Orrin opens.
**Prompt:**
> [STYLE PREAMBLE] An orthographic front view of a sealed stone doorway, plain grey
> background. A single slab of dark stone set into a plain dressed frame, with a faint
> violet seam of light tracing its outline where it is magically sealed. Closed, heavy,
> no handle, no hinges, no keyhole. Flat even lighting.

### PROP-06 — Vault chain and anchor `[REF]`
**Where:** dressing for the Vault. Not in the code yet — worth adding, since the Warden is
described as sealed in and nothing currently shows that.
**Prompt:**
> [STYLE PREAMBLE] An orthographic reference of enormous broken restraint chains and a wall
> anchor plate, plain grey background. Iron links each the size of a man's torso, snapped
> and twisted open, bolted to a massive stone anchor plate with violet residue burned
> around the fixings. Something very large tore free of these.

---

# PART 5 — EFFECTS, PARTICLES AND DECALS

All of these ship as PNGs. Everything glowing goes on **pure black** (additive blending
treats black as transparent). Everything with a hard edge goes on **pure magenta**.

### FX-01 — Flame sheet `[FX]`
**Where:** `BrazierFlame.fbx` in the Vault and the dungeon braziers.
**Output:** 1024×1024, **pure black** background
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a single tall flame, orange `#FF9438` at the
> base rising to pale yellow-white at the tip, soft-edged, with a few sparks lifting off.
> Painted rather than photographic. Emissive and self-lit, no smoke, no brazier, no
> background detail.

### FX-02 — Flame animation strip `[FX]`
**Where:** the same braziers, animated.
**Output:** 4096×1024, four frames in a row, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a horizontal strip of exactly four frames of
> the same flame, evenly spaced, each frame square and the same size. The flame flickers
> and leans slightly differently in each frame but keeps the same overall height, colour
> and position. Orange `#FF9438` base to pale yellow tip. Emissive, no smoke, no
> background.

### FX-03 — Enemy death burst `[FX]`
**Where:** `DeathBurst.SpawnAt` — currently coloured cubes.
**Output:** 1024×1024, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a radial burst of angular shards and embers
> flying outward from a single point, brightest at the centre, fading at the edges,
> irregular and violent. Neutral white so it can be tinted any colour in engine.
> Emissive, no background detail.

### FX-04 — Hit spark `[FX]`
**Where:** `PlayerCombat.DamageEverythingInFront` — currently only a sound.
**Output:** 512×512, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a small sharp impact flash — a bright
> white-yellow core with four to six hard angular spikes radiating outward, asymmetric,
> lasting an instant. Emissive, no background detail.

### FX-05 — Sword swing trail `[FX]`
**Where:** `swingIndicator` — currently an invisible scaled object.
**Output:** 2048×512, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a wide crescent arc sweeping left to right,
> thick and bright at the leading edge and thinning to nothing at the trailing edge, soft
> feathered inner edge. Neutral white so it can be tinted. Emissive, no blade, no hand, no
> background.

### FX-06 — The Edge's 360° trail `[FX]`
**Where:** the heavy attack of the Warden's Edge — a full spin, the payoff moment.
**Output:** 2048×2048, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a complete circular ring of violet
> `#B26BFF` energy seen from above, brightest along the outer rim, trailing inward into
> fine filaments, with a torn ragged outer edge. Emissive and self-lit, no object at the
> centre, no background.

### FX-07 — Warden shockwave ring `[FX]`
**Where:** `WardenBoss.cs` phase 3 — the ground wave the player jumps over.
**Output:** 2048×2048, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: an expanding ground shockwave seen from
> directly above — a bright violet ring with a rough cracked leading edge, dust and stone
> fragments thrown up along it, hollow and dark at the centre. Emissive, top-down, no
> perspective.

### FX-08 — Warden charge telegraph `[FX]`
**Where:** `WardenBoss.cs` phase 1 wind-up — the player must see the charge coming.
**Output:** 1024×2048, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a long tapering warning path marker pointing
> away from the viewer — a bright violet-red band, widest at the near end, narrowing to a
> point, with hard chevron arrows along it and a rough scorched edge. Emissive, reads
> instantly as "something is about to come down this line".

### FX-09 — Essence pickup sparkle `[FX]`
**Where:** `EssencePickup.OnTriggerEnter`.
**Output:** 512×512, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a small soft teal `#6BF2D6` bloom with six
> fine light rays radiating from it and a scatter of tiny glints around it. Gentle and
> rewarding rather than violent. Emissive.

### FX-10 — Portal open burst `[FX]`
**Where:** `Portal.Open` — plays as the arch rises.
**Output:** 1024×1024, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a violet `#8C38F2` energy bloom expanding
> outward in a soft sphere with vertical light filaments streaming upward through it.
> Emissive and self-lit, no frame, no arch, no background.

### FX-11 — Dust and footfall puff `[FX]`
**Where:** footsteps, landing from a jump, the Warden's tread.
**Output:** 512×512, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a soft irregular puff of pale grey dust,
> denser at the base and wispy at the top, no hard edges. Neutral grey so it can be
> tinted. Soft and diffuse.

### FX-12 — Blood/damage vignette `[FX]`
**Where:** taking damage — currently nothing on screen but a bar dropping.
**Output:** 1920×1080, **pure black** centre
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a screen-edge vignette of deep red `#D13840`,
> heaviest and most opaque at the four corners and the edges, fading completely to nothing
> in the middle third of the frame. Irregular organic edge, not a clean gradient. No
> objects, no shapes, no text.

### FX-13 — Enemy eye glow `[FX]`
**Where:** `eyeGlowMaterial` — every enemy has these.
**Output:** 256×256, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a single small hot yellow `#FFD94D` glowing
> eye — a bright core with a soft halo and a faint horizontal slit pupil. Emissive, no
> face, no socket, no background.

### FX-14 — Pillar shatter debris `[FX]`
**Where:** `Pillar.cs` when the Warden destroys cover.
**Output:** 1024×1024, **pure magenta** background
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a scattered collection of about
> twenty broken grey stone fragments of varying sizes, sharp angular breaks, dusty
> surfaces, arranged separately with clear gaps between them so each can be cut out
> individually. Flat even lighting, top-down, no shadows.

---

# PART 6 — USER INTERFACE

Everything here ships as-is and needs no Blender. **This is the highest-value section** —
it is what an investor looks at first and it is currently drawn with Unity's default
programmer GUI.

## The HUD — `HudDisplay.cs`

### UI-01 — Health bar frame `[UI]`
**Output:** 512×64 PNG, **pure magenta** background
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a horizontal game health bar
> FRAME only, empty, no fill. Dark hammered iron border with slightly irregular hand-forged
> edges, subtle rivets at the corners, a dark recessed interior. Long and thin. Clean
> game-UI art, flat, viewed straight on, no perspective, no text, no numbers.

### UI-02 — Health bar fill `[UI]`
**Output:** 512×64 PNG, **pure magenta** background
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a horizontal bar fill only, a
> solid deep red `#D13840` with a subtle vertical gradient, slightly brighter along the
> top edge, and a soft glow at the right-hand end. Plain rectangle, no frame, no border,
> no text.

### UI-03 — Stamina bar fill `[UI]`
**Output:** 512×64 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a horizontal bar fill only, a
> solid warm gold `#E6C24D` with a subtle vertical gradient, slightly brighter along the
> top edge. Plain rectangle, no frame, no border, no text.

### UI-04 — Essence icon `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon of a teal
> `#6BF2D6` crystal shard, angular and faceted, glowing softly, viewed straight on. Flat
> clean icon art with a subtle dark outline for readability. No text, no frame, no
> background elements.

### UI-05 — Potion charge icon, full `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon of a small
> corked flask filled with glowing red liquid, viewed straight on. Flat clean icon art
> with a subtle dark outline. No text, no frame.

### UI-06 — Potion charge icon, empty `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon of a small
> corked flask, EMPTY, the glass dark and grey with no liquid and no glow. Identical shape
> and size to a filled version so the two can be swapped. Flat clean icon art with a
> subtle dark outline. No text.

### UI-07 — Weapon icon, sword `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon of a plain
> arming sword, angled 45 degrees, silhouette-first with minimal internal detail. Flat
> clean monochrome-grey icon art with a strong outline. Must be instantly distinguishable
> in shape from a hammer, a bow and a greatsword. No text, no frame.

### UI-08 — Weapon icon, hammer `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon of a heavy
> two-handed war maul with a huge blunt head, angled 45 degrees, silhouette-first. Flat
> clean monochrome-grey icon art with a strong outline. Bold and top-heavy so it is
> unmistakable beside a sword. No text.

### UI-09 — Weapon icon, bow `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon of a strung
> recurve bow with a nocked arrow, viewed side-on, silhouette-first. Flat clean
> monochrome-grey icon art with a strong outline. No text.

### UI-10 — Weapon icon, the Warden's Edge `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon of a long
> two-handed greatsword with a glowing violet `#B26BFF` core channel down the blade,
> angled 45 degrees, silhouette-first. Same flat clean icon style as the other weapon
> icons but visibly grander and self-lit. No text.

### UI-11 — Boss health bar frame `[UI]`
**Where:** `DrawTheBossBar` — the Warden's bar, spans the top of the screen.
**Output:** 1024×96 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide ornate boss health bar
> FRAME only, empty. Heavy dark iron with violet `#8C38F2` inlay running along the top and
> bottom edges, chipped and battle-worn, with a small angular ornament at each end.
> Grander and heavier than a player health bar. Flat game-UI art, straight on, no
> perspective, no text.

### UI-12 — Boss health bar fill `[UI]`
**Output:** 1024×96 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide bar fill only, violet
> `#8C38F2` with a brighter hot core along the horizontal centre line and darker edges,
> faintly crystalline texture. Plain rectangle, no frame, no text.

### UI-13 — Boss phase pip `[UI]`
**Where:** the Warden has three phases; the bar should show which one.
**Output:** 128×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: two small game UI markers side by
> side — a diamond-shaped iron pip shown lit with violet light, and the same pip shown
> dark and unlit. Identical shape and size. Flat clean icon art. No text.

### UI-14 — Round banner plate `[UI]`
**Where:** `DrawTheRound` — "ROUND 2 — THE PACK" across the middle of the screen.
**Output:** 1536×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide horizontal announcement
> banner plate for a game, empty with no text. Dark weathered stone slab with a thin
> violet light line running along its top and bottom edges, tapering to points at the left
> and right ends, semi-transparent in the middle. Flat game-UI art, straight on, no
> perspective, NO TEXT AT ALL.

### UI-15 — Dialogue box panel `[UI]`
**Where:** `DialogueBox.DrawTheConversation` — every conversation with Orrin.
**Output:** 1536×384 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide rectangular dialogue panel
> for a fantasy game, empty with no text. Very dark near-black semi-transparent interior,
> a thin violet `#B26BFF` glowing line along the top edge, plain hand-cut stone borders at
> the left and right, subtle worn corners. Understated — the text is the point. Flat
> game-UI art, straight on, NO TEXT.

### UI-16 — Speaker nameplate `[UI]`
**Where:** "ORRIN, THE LENSMAKER" above the dialogue text.
**Output:** 768×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a small horizontal nameplate tab
> for a dialogue box, empty with no text. A dark angular plate with a violet underline and
> a clipped corner on the right. Sits above and to the left of a larger panel. Flat
> game-UI art, NO TEXT.

### UI-17 — Continue indicator `[UI]`
**Where:** the "space" prompt at the bottom of a dialogue line.
**Output:** 128×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a small downward-pointing
> triangular game UI arrow, violet `#B26BFF`, softly glowing, with a subtle dark outline.
> Simple, clean, no text, no frame.

### UI-18 — Subtitle bar `[UI]`
**Where:** `DrawTheMurmur` — Orrin's coaching lines during fights.
**Output:** 1536×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide low-profile subtitle strip
> for a game, empty with no text. Very dark semi-transparent band, soft feathered edges
> fading out to nothing at the left and right, no hard border. Unobtrusive — it appears
> during combat and must not draw the eye. Flat game-UI art, NO TEXT.

### UI-19 — Interact prompt key `[UI]`
**Where:** "[E] speak to Orrin".
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI keycap button,
> square with rounded corners, dark iron with a violet edge light and a subtle inner
> shadow, EMPTY with no letter on it. Flat clean icon art, straight on, NO TEXT.

## The shrine

### UI-20 — Shrine panel `[UI]`
**Where:** `DrawTheShrinePrompt` — the three upgrade options.
**Output:** 1024×512 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a vertical fantasy game upgrade
> panel, empty with no text. Dark stone tablet with teal `#6BF2D6` glowing veins tracing
> its border, three empty recessed slots stacked vertically inside it, worn edges. Flat
> game-UI art, straight on, NO TEXT.

### UI-21 — Upgrade icon, Vitality `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon representing
> vitality and maximum health — a stylised heart shape built from angular crystal facets,
> deep red, with a subtle dark outline. Flat clean icon art, no text, no frame.

### UI-22 — Upgrade icon, Strength `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon representing
> attack strength — a clenched angular fist rendered as crystal facets, hot orange, with a
> subtle dark outline. Flat clean icon art matching a heart icon in weight and style. No
> text.

### UI-23 — Upgrade icon, Endurance `[UI]`
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a single game UI icon representing
> stamina and endurance — a stylised swirling wind or breath curl rendered as angular
> crystal facets, warm gold, with a subtle dark outline. Flat clean icon art matching a
> heart and a fist icon in weight and style. No text.

### UI-24 — Bow draw meter `[UI]`
**Where:** `DrawTheBowDraw` — fills as the bow is drawn.
**Output:** 512×512 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a circular game UI draw-strength
> meter, empty. A thin dark ring with a subtle notch at the top marking full draw, and a
> small arrow-shaped marker at the centre. Flat clean UI art, no fill, no text.

## Menus and title — `MainMenu.cs`

### UI-25 — ONE VALLEY wordmark `[UI]`
**Where:** the title screen and the closing title card. **The single most-seen image in
the whole project.**
**Output:** 2048×512 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a game logo wordmark reading
> exactly "ONE VALLEY" in capital letters, on one line. Carved weathered stone lettering
> with chipped edges and a thin violet `#B26BFF` light bleeding from the cracks inside the
> letterforms. Strong, wide, slightly condensed serif-less letters. Nothing else in the
> image — no icons, no border, no subtitle, no extra words.

### UI-26 — Title screen background wash `[UI]`
**Where:** drawn over the live dungeon view, so it must not be opaque.
**Output:** 1920×1080 PNG, **pure black**
**Prompt:**
> [STYLE PREAMBLE] On a pure black background: a soft dark vignette for a game title screen —
> heaviest at the top and bottom edges, fading to fully clear across the middle band of
> the image, with a very faint violet tint in the darkness. Subtle drifting dust motes.
> No objects, no text, no border.

### UI-27 — Menu button, normal `[UI]`
**Output:** 640×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide horizontal game menu
> button, EMPTY with no text. A dark weathered stone bar with a thin iron edge, slightly
> irregular hand-cut top and bottom lines, matte and unlit. Restrained. Flat game-UI art,
> straight on, no perspective, NO TEXT.

### UI-28 — Menu button, hover `[UI]`
**Output:** 640×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide horizontal game menu
> button, EMPTY with no text, in a highlighted state. Identical shape, size and position
> to an unlit stone menu button, but with a violet `#B26BFF` glow along its edges and a
> faint inner light. Flat game-UI art, NO TEXT.

### UI-29 — Menu button, pressed `[UI]`
**Output:** 640×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide horizontal game menu
> button, EMPTY with no text, in a pressed state. Identical shape and size to an unlit
> stone menu button but visibly recessed, darker, with the violet edge light dimmed and a
> shadow across the top inner edge. Flat game-UI art, NO TEXT.

### UI-30 — Menu button, disabled `[UI]`
**Where:** the Continue button when there is no save.
**Output:** 640×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide horizontal game menu
> button, EMPTY with no text, in a disabled state. Identical shape and size to an unlit
> stone menu button but faded, greyed, cracked across the middle, with no edge light at
> all. Clearly unavailable. Flat game-UI art, NO TEXT.

### UI-31 — Pause screen panel `[UI]`
**Output:** 1024×768 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a tall rectangular pause menu
> panel for a fantasy game, empty with no text and no buttons. Dark semi-transparent
> interior with a hand-cut stone border, a violet light line along the top edge, and
> slightly worn corners. Flat game-UI art, straight on, NO TEXT.

### UI-32 — Lens indicator plate `[UI]`
**Where:** `EndingSequence.DrawTheLensName` — "LENS — NEON". Also worth showing whenever
Tab is pressed. **This is the platform pitch made visible; give it real care.**
**Output:** 768×128 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a small horizontal game UI plate,
> empty with no text, with a stylised icon at its left end of a faceted lens or prism
> splitting a single white beam into four coloured beams. Dark plate, violet edge light.
> Flat clean UI art. NO TEXT anywhere in the image.

### UI-33 — Death message plate `[UI]`
**Where:** "YOU DIED".
**Output:** 1536×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a wide horizontal message plate
> for a game death screen, empty with no text. A dark cracked stone slab with deep red
> `#D13840` light bleeding from the fractures, tapering at both ends. Grim. Flat game-UI
> art, NO TEXT.

### UI-34 — Crosshair `[UI]`
**Where:** aiming the bow. Currently none at all.
**Output:** 256×256 PNG, **pure magenta**
**Prompt:**
> [STYLE PREAMBLE] On a pure magenta `#FF00FF` background: a minimal game crosshair — four
> short tapered marks arranged around an empty centre, with a single small dot in the
> middle, pale off-white with a thin dark outline for readability against any background.
> Flat clean UI art, no circle, no text.

---

# PART 7 — SKIES AND BACKDROPS

Each lens has its own sky colour (`StyleLens.cs`). A flat colour works; a real sky is far
better, and this is cheap to generate.

### SKY-01 — NATURAL sky `[TEX]`
**Where:** `natural.skyAndFogColour = #9EA199`.
**Output:** 4096×2048 equirectangular panorama
**Prompt:**
> [STYLE PREAMBLE] A seamless 360-degree equirectangular panoramic sky, 2:1 aspect ratio,
> for a game skybox. Overcast high-altitude daylight: flat pale grey-white cloud cover with
> faint breaks of cold blue, distant hazy mountain silhouettes along the bottom edge.
> Cold, still, before weather. No sun disc, no birds, no text.

### SKY-02 — NOIR sky `[TEX]`
**Where:** `noir.skyAndFogColour = #0D0D0F`, fog 12–90m.
**Output:** 4096×2048 equirectangular
**Prompt:**
> [STYLE PREAMBLE] A seamless 360-degree equirectangular panoramic sky, 2:1 aspect ratio,
> for a game skybox. Almost entirely black with a single hard bright break in the cloud
> throwing stark white light, extreme contrast, no mid-tones, heavy black haze along the
> horizon. Film-noir. Monochrome only, no colour, no text.

### SKY-03 — NEON sky `[TEX]`
**Where:** `neon.skyAndFogColour = #080312`.
**Output:** 4096×2048 equirectangular
**Prompt:**
> [STYLE PREAMBLE] A seamless 360-degree equirectangular panoramic sky, 2:1 aspect ratio,
> for a game skybox. Near-black deep violet void with a fine glowing magenta and cyan grid
> receding to the horizon, a few hard-edged neon horizontal light bands, no clouds, no
> stars, no sun. Flat, graphic, synthetic. No text.

### SKY-04 — CHALK sky `[TEX]`
**Where:** `chalk.skyAndFogColour = #F0EDE6`.
**Output:** 4096×2048 equirectangular
**Prompt:**
> [STYLE PREAMBLE] A seamless 360-degree equirectangular panoramic sky, 2:1 aspect ratio,
> for a game skybox. Plain warm off-white paper with a very faint blue-grey pencil hatching
> suggesting cloud, like the background of an architectural drawing. Almost blank. No sun,
> no colour beyond the faintest grey, no text.

### SKY-05 — Vault interior backdrop `[TEX]`
**Where:** seen past the Vault dome edges.
**Output:** 2048×2048
**Prompt:**
> [STYLE PREAMBLE] A dark seamless backdrop of a vast underground cavern beyond a vault wall:
> almost entirely black, with a few distant violet crystal glows and the faint suggestion
> of enormous stone ribs receding into the dark. No detail, no focal point — this is
> background depth only. No text.

---

# PART 8 — PER-LENS VARIANTS

Optional, and the first thing to cut. But this is the platform's headline feature, and
hand-authored per-lens art is *far* more convincing than a runtime colour filter.

The rule: generate these **only for the four surfaces the camera sees most** — arena
floor, cliff face, dungeon wall, Vault wall.

### LENS-01 to LENS-04 — NOIR variants `[TEX]`
**Prompt pattern** (substitute the surface):
> [STYLE PREAMBLE] A seamless tileable texture of `<SURFACE>` rendered in high-contrast
> black and white film-noir: pure blacks, blown highlights, no mid-greys, heavy grain,
> hard-edged shadow shapes. No colour anywhere in the image. Flat even lighting, no
> perspective.

### LENS-05 to LENS-08 — CHALK variants `[TEX]`
**Prompt pattern:**
> [STYLE PREAMBLE] A seamless tileable texture of `<SURFACE>` drawn as a technical chalk
> illustration on off-white paper: fine blue-grey pencil hatching describing the form, no
> shading or fill, visible construction lines, the paper showing through everywhere. Flat,
> no perspective.

**NEON needs no texture variants** — that lens draws flat unlit colour and discards
surface detail entirely. Spend the effort on silhouettes instead (see 0.5).

---

# PART 9 — WHAT IS NOT IN THIS LIST

Said plainly so nothing is assumed to be covered.

- **Animation.** No walk cycles, attack animations or rigs. ChatGPT cannot produce these;
  they are Blender work. The characters are currently unrigged and unanimated, and that is
  the single biggest visual gap in the project after textures.
- **Audio.** 39 clips already exist in `Resources/Audio` and are wired up. No new sound is
  needed for the demo.
- **Fonts.** UI-25 is a fixed wordmark image. Body text stays in Unity's built-in font
  unless you licence a typeface separately.
- **The valley terrain mesh.** `ValleyTerrain.fbx` already exists and is sculpted. It needs
  a texture (TEX-01), not a remodel.
- **Normal, roughness and AO maps.** Derived from the albedos, not generated. See 0.2.
