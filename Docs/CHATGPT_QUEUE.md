# ChatGPT generation queue — 29 Aug 2026

Assembled from `ASSET_BIBLE.md`. The style preamble is already pasted into every prompt
below, so each block is copy-paste ready. Generate in the order given.

**Save everything into `Docs/refs/`.** Name files by their ID — `CHR-06.png`, `UI-25.png`.
That is all I need to pick them up; do not rename them descriptively.

---

## One change from the bible's LAYOUT rule — read this

Section 0.3 asks for "front, side and back views in a row". Do **not** use that for the
characters below. ChatGPT will draw the three views at quietly different proportions —
the side view's shoulder height will not match the front view's — and a modelling
reference whose views disagree is worse than one view, because I will build the
disagreement into the mesh.

Generate **one view per image**, as separate generations, repeating the measurements
verbatim each time. Front first. Only ask for the side once you are happy with the front.

---

# BATCH 1 — the four models I am blocked on

These are the ones where I genuinely cannot invent the proportions. Highest value first.

## CHR-06 — Spitter  ← START HERE

The bible flags this as the single most valuable character asset: the ranged enemy
currently reuses the Grunt mesh (`ValleyBuilder.cs:1520`), so round three reads as unfair
rather than as a new lesson. Roughly human height.

> **STYLE:** Stylised low-fantasy realism for a 3D game. Weathered hand-cut stone, not
> ornate high fantasy. Cold grey-violet rock, warm orange firelight, and a single magical
> violet that belongs only to the Vault. Muted, desaturated base palette with saturated
> light sources. Strong readable silhouettes and clear large forms — small fussy detail is
> lost at gameplay distance. No text, no watermarks, no logos, no UI elements.
>
> **LAYOUT:** A single flat orthographic FRONT view, plain flat mid-grey background, even
> flat lighting, no cast shadows, no rim light, neutral standing stance. A modelling
> reference, not an illustration.
>
> A ranged thrower creature for a fantasy game, roughly human height but hunched and
> asymmetric. One enormously overdeveloped throwing arm far larger than the other, a heavy
> counterweighted tail, and a wide flat head with a large single eye. Mottled green-grey
> hide `#5C8042`. A pouch or sling of rocks slung at the hip. Reads instantly as "throws
> things from a distance" and must be impossible to confuse with a heavy melee brute. Bold
> asymmetric silhouette.

## CHR-01 — The Warden

**Answer one question before generating.** You already have a Warden reference from 23 Aug
(the violet stone golem with the crystal chest). It is a good image, but it disagrees with
the bible: CHR-01 specifies *dark iron plating `#2B2B33` bolted over cracked grey stone*,
whereas your existing image is stone throughout with no ironwork.

- **Happy with the stone golem?** Skip this prompt. I will model from the image you have.
- **Want the iron-over-stone version?** Generate the prompt below and the old one is dead.

Three times human height.

> **STYLE:** Stylised low-fantasy realism for a 3D game. Weathered hand-cut stone, not
> ornate high fantasy. Cold grey-violet rock, warm orange firelight, and a single magical
> violet that belongs only to the Vault. Muted, desaturated base palette with saturated
> light sources. Strong readable silhouettes and clear large forms. No text, no watermarks,
> no logos, no UI elements.
>
> **LAYOUT:** A single flat orthographic FRONT view, plain flat mid-grey background, even
> flat lighting, no cast shadows, no rim light, neutral T-pose. A modelling reference, not
> an illustration.
>
> A colossal armoured stone-and-iron guardian, three times human height, built to be
> imprisoned rather than to serve. Broad hunched shoulders, long heavy arms ending in blunt
> crushing fists, a short thick neck and a featureless helm-like head with a single
> horizontal slot. Body of dark iron plating `#2B2B33` bolted over cracked grey stone, with
> violet `#8C38F2` light burning out from between the plates at the chest and joints.
> Massive, slow, immensely strong. Bold simple readable silhouette, no fine ornament.

## CHR-04 — Grunt

Replaces the existing `Grunt.fbx`. Body height 1.9 m, slow and heavy, does not flinch.

> **STYLE:** Stylised low-fantasy realism for a 3D game. Weathered hand-cut stone, not
> ornate high fantasy. Muted, desaturated base palette with saturated light sources. Strong
> readable silhouettes and clear large forms — small fussy detail is lost at gameplay
> distance. No text, no watermarks, no logos, no UI elements.
>
> **LAYOUT:** A single flat orthographic FRONT view, plain flat mid-grey background, even
> flat lighting, no cast shadows, no rim light, neutral standing stance. A modelling
> reference, not an illustration.
>
> A slow heavy brute creature for a fantasy game, slightly taller than a man and much
> broader. Thick sloped shoulders, short neck, long heavy arms, squat powerful legs. Coarse
> hide the colour of old leather `#6B5B47`, cracked and calloused into thick plates over the
> shoulders and back. Small deep-set eye sockets with a hot yellow `#FFD94D` glow. Blunt,
> patient, immovable. Bold simple silhouette, low detail.

## CHR-05 — Darter

Replaces the existing `Darter.fbx`. Shorter than a man, built for one straight-line lunge.
Its silhouette must be unmistakable against the Grunt's at a glance.

> **STYLE:** Stylised low-fantasy realism for a 3D game. Weathered hand-cut stone, not
> ornate high fantasy. Muted, desaturated base palette with saturated light sources. Strong
> readable silhouettes and clear large forms — small fussy detail is lost at gameplay
> distance. No text, no watermarks, no logos, no UI elements.
>
> **LAYOUT:** A single flat orthographic SIDE view, plain flat mid-grey background, even
> flat lighting, no cast shadows, no rim light, neutral standing stance. A modelling
> reference, not an illustration.
>
> A fast lean predatory creature for a fantasy game, slightly shorter than a man, built
> entirely for one straight-line lunge. Long powerful digitigrade hind legs, forward-leaning
> body, narrow head, small forelimbs held tight to the chest. Taut rust-red hide `#8C5238`
> over visible ribs and tendons. Hot yellow `#FFD94D` eye glow. Nervous, twitchy, all
> forward momentum. Bold simple silhouette, unmistakably different from a heavy brute.

*(Side view is deliberate here — a digitigrade lunger is defined by its leg angles, which
the front view hides completely.)*

---

# BATCH 2 — the title screen, ships with no Blender at all

Section 0.7 of the bible ranks these **priority #1 of the whole list** — first thing an
investor sees, and they drop straight into `MainMenu.cs` as PNGs. Nothing of mine blocks
on them, so do these once Batch 1 is away.

Everything here goes on **pure magenta `#FF00FF`** except UI-26, which goes on **pure
black**. Do not skip that — it is how the background gets keyed out later.

## UI-25 — ONE VALLEY wordmark — 2048×512, magenta

The single most-seen image in the project.

> **STYLE:** Stylised low-fantasy realism for a 3D game. Weathered hand-cut stone. Cold
> grey-violet rock and a single magical violet. Muted, desaturated base palette. Strong
> readable silhouettes.
>
> On a pure magenta `#FF00FF` background: a game logo wordmark reading exactly "ONE VALLEY"
> in capital letters, on one line. Carved weathered stone lettering with chipped edges and a
> thin violet `#B26BFF` light bleeding from the cracks inside the letterforms. Strong, wide,
> slightly condensed serif-less letters. Nothing else in the image — no icons, no border, no
> subtitle, no extra words.

**Check the spelling before you save it.** Image models misspell wordmarks constantly and
this one is on screen more than any other asset.

## UI-26 — Title background wash — 1920×1080, **pure black**

> **STYLE:** Stylised low-fantasy realism for a 3D game. Cold grey-violet tones, muted and
> desaturated. No text, no watermarks, no logos.
>
> On a pure black background: a soft dark vignette for a game title screen — heaviest at the
> top and bottom edges, fading to fully clear across the middle band of the image, with a
> very faint violet tint in the darkness. Subtle drifting dust motes. No objects, no text,
> no border.

## UI-27 / 28 / 29 / 30 — Menu buttons — 640×128 each, magenta

Generate all four **in one conversation, back to back**, so the shape stays identical
between states. If you start a fresh chat for each, you get four differently-shaped buttons
and the menu will jitter as the mouse moves across it.

Shared opening for all four:

> **STYLE:** Stylised low-fantasy realism for a 3D game. Weathered hand-cut stone. Muted and
> desaturated with a single magical violet. No text, no watermarks, no logos.
>
> On a pure magenta `#FF00FF` background: a wide horizontal game menu button, EMPTY with no
> text. Flat game-UI art, straight on, no perspective, NO TEXT.

Then, per state:

- **UI-27 normal** — "A dark weathered stone bar with a thin iron edge, slightly irregular hand-cut top and bottom lines, matte and unlit. Restrained."
- **UI-28 hover** — "Identical shape, size and position to the previous button, but with a violet `#B26BFF` glow along its edges and a faint inner light."
- **UI-29 pressed** — "Identical shape and size to the previous button but visibly recessed, darker, with the violet edge light dimmed and a shadow across the top inner edge."
- **UI-30 disabled** — "Identical shape and size to the previous button but faded, greyed, cracked across the middle, with no edge light at all. Clearly unavailable." *(This is the Continue button when there is no save.)*

---

# What you do NOT need to generate

I am modelling these from the bible's written spec directly — they are lathe, array and
displacement work where a reference adds nothing and costs you an evening:

WPN-01 sword · WPN-02 hammer · WPN-03 bow · WPN-06 arrow · WPN-07 spitter's rock ·
WPN-08 grunt's club · PROP-01 essence shard · PROP-02 Warden's Eye · PROP-03 brazier ·
PROP-04 potion · PROP-05 door slab · PROP-06 vault chain · CHR-03 Warden's core ·
CHR-09 Orrin's staff

The one exception is **WPN-04, the Warden's Edge** — it is the reward weapon and its entire
job is to look obviously superior to the plain sword at a glance. If you have spare
capacity after Batch 2, that one is worth an image. Its prompt is unchanged in the bible.

And do not generate Orrin (CHR-07/08) or the player (CHR-10/11). A lined human face at
conversation distance is not something I can build, and no reference fixes that. Those come
from Mixamo, which is free and arrives already rigged and animated — as
`BLENDER_ASSET_PLAN.md` said in its last section before we ignored it.
