# Animation spec — segmented characters

Written 30 Aug 2026 by the Blender session, for the Unity/C# session.

Part 1 is the segmentation handoff: exactly what each model contains. Part 2 is the
animation spec: what every action should look like, in parts and degrees and seconds.

Two rules run through the whole document.

**Timings come from the gameplay fields, never from new numbers.** `EnemyBrain` already
has `windUpSeconds`, `strikeSeconds` and `damageLandsAfterSeconds`; `PlayerMovement`
already has `dodgeLastsSeconds`. An animation that invents its own duration will drift out
of step with the hitbox it is supposed to be selling, and the player will read that as the
game lying to them. Read the existing field.

**Never write to the animator's own transform.** `EnemyBrain` leans and sinks the model
root for wind-ups; `ProceduralAnimator` owns that root's CHILDREN. That is the whole
reason the two compose without fighting.

---

# PART 1 — What is in each model

All five carry **11 of 11** parts that `ProceduralAnimator.FindTheParts` looks for:

```
Hips, Torso, Head, ThighL, ThighR, ShinL, ShinR,
UpperArmL, UpperArmR, ForearmL, ForearmR
```

Every other part is parented into the tree and rides along, but is never posed. Those are
listed below because several are worth animating later, and they are already named and
hooked into the hierarchy for it.

| Model | Parts | Carried but never posed |
|---|---|---|
| `GruntSegmented` | 16 | Shoulders, FistL/R, FootL/R |
| `SpitterSegmented` | 21 | Shoulders, Eye, FistL/R, FootL/R, **Tail1–3**, RockPouch |
| `DarterSegmented` | 18 | EyeL/R, FootL/R, **Tail1–3** |
| `WardenSegmented` | 17 | Shoulders, HeadSlot, FistL/R, FootL/R |
| `PlayerSegmented` | 29 | Shoulders, ShoulderGuard, Belt, BeltBuckle, TunicSkirt, Collar, ChestStrap, Brow, Nose, Hair, BracerL/R, HandL/R, BootCuffL/R, FootL/R |

Hierarchy is the same shape everywhere:

```
Hips
├── ThighL → ShinL → FootL
├── ThighR → ShinR → FootR
└── Torso
    ├── Head
    └── Shoulders
        ├── UpperArmL → ForearmL → (FistL / HandL)
        └── UpperArmR → ForearmR → (FistR / HandR)
```

Note that **Shoulders sits between Torso and the arms** and is not in the animated set.
Rotating it would swing both arms together — useful for a shrug or a Warden wind-up, and
free if you want it.

**Sign convention:** `SetPitch` rotates about local X. Which way is "forward" survives the
FBX export inconsistently, which is why `forwardSwingSign` is exposed. Everything below is
written as *forward* or *back*; resolve the actual sign once with that field and every
animation follows.

---

# PART 2 — The animations

## What exists today

**Updated 30 Aug, after the Unity session's second pass.** `ProceduralAnimator` is now 577
lines and already has more than this document originally assumed. Check here before
building anything.

**Updated again 30 Aug, after the single combined session.** Everything on the old
"still missing" list is now built and compiles clean. NONE of it has been seen running -
Unity was unfocused for the whole session - so all of it is unverified on screen.

DONE and working:
- The **additive pose stack** - `ClearThePose()`, one accumulator per part,
  `ApplyThePose()` at the end of `LateUpdate`. Every animation contributes offsets that
  sum, rather than assigning rotations and clobbering each other.
- **Pitch, yaw AND roll.** The stack used to carry one pitch per part, which could not
  express a dodge roll or a shoulder yaw. It now carries yaw and roll where they are
  needed, and the hips carry a full offset vector rather than a height.
- **Enemy attack** - `ShowWindUp`, `ShowStrike`, `ClearAttack`, driven from `EnemyBrain`.
- **Death** - `PlayDeath()` called from `EnemyBrain`, with a 1 s collapse before the body
  is switched off.
- **`ValleyBuilder.AttachSegmentedModel`** - wraps the imported model so a child called
  `Hips` exists for the animator to find. Unity promotes a lone FBX root to be the asset
  root and renames it after the file, so nothing called "Hips" survived the import.
- **Per-part materials** - `ValleyBuilder.MaterialForPart`, the same name rules
  `Tools/preview_coloured.py` uses, with the hex values from ASSET_BIBLE 0.4.
- **Every player action** - `PlayerAnimator.cs` reads the player scripts and drives the
  animator. Sprint, jump (launch / airborne / landing absorb), dodge, light and heavy
  swing, surge, potion, weapon swap and hit reaction.
- **Tail follow** for Spitter and Darter - three segments, each lagging the one in front.
- **The Warden's four moves** - charge, throw, summon, slam.

WHAT IS LEFT:
- **Look at all of it.** Nothing here has been seen. Every sign convention is resolved
  through `Forward()` and every magnitude is an inspector field, so a wrong-looking
  animation should be a field to flip rather than code to rewrite.
- **The anticipatory jump crouch** in the table below is NOT built. It needs the crouch to
  begin 0.10 s before the launch, which cannot be done without delaying the jump itself
  by that much. The launch beat starts from a slight crouch instead.
- **The Grunt's club may not line up with its fist** now that the arm swings. The club
  hangs off a separate `WeaponPivot` that `EnemyBrain` rotates on its own, which was fine
  when the arm never moved. Parenting it to `FistL` would make the two one motion.
- **Non-uniform scale shears the arms on big swings.** `GruntSegmented` has `Shoulders` at
  `(1, 0.62, 1)` with `UpperArmL/R` at `(1, 1.613, 1)`, and `WardenSegmented` the same at
  `0.66`/`1.515`. Rotating a part whose ancestor carries non-uniform scale shears it - the
  walk's 20 degree swing barely shows it, a 118 degree attack will. Applying scale before
  export fixes it. Spitter, Darter and Player are already clean.

---

## Player

### Idle — EXISTS
Hips bob ±1.2 cm at 0.35 Hz, torso ±1.5°, arms ±2°. Fine as-is.

### Walk — EXISTS
Stride driven by measured speed. Fine as-is.

### Sprint — NEW
`PlayerMovement.sprintingSpeed` is 8.5 against `walkingSpeed` 5.5, so speed already
lengthens the stride on its own. What is missing is that sprinting should look *different*,
not just faster:

- Torso pitched **forward 12°**, held for the duration
- Arm swing amplitude **×1.6**
- Hip bob **×2.0**
- Elbows tucked: forearms bent **35°** rather than the walk's 15°

Blend in over 0.2 s as speed crosses 7 m/s so it does not pop.

### Jump — NEW
Drive from `IsAirborne()` and `VerticalSpeed()`. Four beats:

| Beat | When | Pose |
|---|---|---|
| Crouch | 0.10 s before launch | Hips drop 8 cm, thighs forward 18°, shins back 30°, torso forward 10° |
| Launch | `VerticalSpeed() > 0` | Legs snap straight, arms swing back then up 40° |
| Airborne | `VerticalSpeed() < 0` | Legs tuck: thighs forward 25°, shins back 45°. Arms out 20° |
| Land | on ground contact | Hips drop 12 cm over 0.12 s, recover over 0.20 s |

The landing absorb is the beat that makes a jump feel like it has weight. Do not skip it.

### Dodge — NEW
`IsCurrentlyDodging()`, duration `dodgeLastsSeconds` = 0.35 s. `dodgeSpeed` is 16 — nearly
three times walking — so this needs to read as a committed throw of the body:

- 0.00–0.08 s: torso rolls **22° into the dodge direction**, hips drop 6 cm
- 0.08–0.24 s: legs tuck (thighs forward 30°, shins back 50°), arms pull in tight to chest
- 0.24–0.35 s: leading leg extends to plant, torso rights itself

Roll is about the **forward axis**, not pitch. A dodge that only pitches looks like a stumble.

### Light attack — NEW
`PlayerCombat`. Three beats, and the damage window must land on the strike:

- **Anticipation** 0.08 s — weapon arm pulls back, Shoulders yaw **away 18°**, forearm cocks to 60°
- **Strike** 0.10 s — Shoulders yaw sweeps **through −35°**, upper arm swings 90°, forearm extends to 10°
- **Recovery** 0.17 s — ease to neutral

### Heavy attack — NEW
Hold-click. Slower and bigger, and the wind-up is the tell the player reads:

- **Wind-up** 0.35 s — arm raises overhead (upper arm back 120°), Shoulders yaw −30°, torso leans back 12°
- **Slam** 0.12 s — torso pitches **forward 25°**, arm comes down through 130°, hips drop 10 cm
- **Recovery** 0.28 s — slow, weight settles

### Surge / boost — NEW
`PlayerSurge.cs`. Whatever its duration field says:

- Arms flung back 45°, torso arched back 15° on the burst
- Hips rise 5 cm
- Hold the arched pose for the duration rather than animating through it — a held extreme
  reads as power; a moving one reads as a wobble

### Drink potion — NEW
`PlayerHealing`. 0.60 s. Off-hand (`ForearmL`) raises to head, head tilts **back 15°**,
hold 0.2 s, lower. Left arm only — the weapon stays up.

### Weapon swap — NEW
`PlayerWeapons`. 0.30 s, both forearms cross at belt height and return. Short and cheap;
it exists only so the swap is not instantaneous.

### Hit reaction — NEW
0.25 s: torso pitches **back 12°**, head snaps back 15°, arms rise slightly, hips shift
away from the hit direction. Must be interruptible — a stunlock that cannot be cancelled
is worse than no reaction.

### Death — DONE
`PlayDeath()` is now called from `EnemyBrain.cs:1566`. If it needs improving: legs
buckle over 0.3 s, torso pitches forward 70° over 0.5 s, whole body sinks and settles over
1.2 s total.

---

## Grunt

### Attack — DONE
Overhead club swing, already implemented via `ShowWindUp` / `ShowStrike`. Retune only.
Original spec kept below for reference. Use `windUpSeconds` (0.6) and `strikeSeconds` (0.25) directly, with
damage at `damageLandsAfterSeconds` (0.08) into the strike:

- **Wind-up** 0.60 s — both arms raise overhead through 130°, torso leans back 15°, one foot
  plants forward. Slow and readable: this long tell is the Grunt's entire design.
- **Strike** 0.25 s — arms drive down through 150°, torso pitches forward 22°
- **Recover** — arms hang, torso rights over 0.5 s

`EnemyBrain.windUpLeanDegrees` is 26 and already leans the root. Your arm motion layers
under it.

---

## Darter

### Lunge — PARTLY EXISTS
`lungeSpeed` 22, range 4–11 m. The existing whole-body translation already reads well —
that is why this is the best-looking creature in the game today. Add only:

- **Crouch** before launch: thighs fold to 45°, hips drop 10 cm, head lowers
- **Extension** during: hind legs trail straight back, torso stretches level, head thrusts forward
- **Tail1–3 straighten** behind — a lunging animal's tail goes rigid as a counterweight

### Retreat — NEW
`retreatSeconds` 1.1, `retreatSpeedMultiplier` 1.4. Reverse the walk cycle, head held high
and turned back toward the player. Nervous, per the brief.

---

## Spitter

### Throw — NEW
This is the creature's whole identity, and `throwHeight` is 1.2 with `projectileSpeed` 14.

- **Wind-up** — `UpperArmR` swings **back and up 110°**, forearm cocks, torso yaws away 25°,
  **Tail1–3 swing opposite** as counterweight. This is what the tail is for.
- **Release** — arm whips forward through 140°, torso yaws through, tail snaps back
- **Recover** — arm drops, tail settles over 0.6 s

The big arm and the tail must move in opposition. That single detail is what sells the
whole creature.

---

## Warden

Three phases, per `WardenBoss.cs`.

### Charge — NEW
Torso forward 20°, arms pump in a heavy slow counter-swing at half the Grunt's rate,
Shoulders roll ±8° with each step. Massive and slow.

### Throw — NEW
Both arms raise overhead 140° over a long wind-up, torso arches back 18°, then a two-handed
hurl forward. Longer wind-up than the Grunt's — it is a boss tell.

### Summon — NEW
Arms spread wide 70°, head tilts **back 25°** so `HeadSlot` faces up, hold for the summon
duration. A held pose, not a cycle.

### Slam — NEW
`slamRadius` 5.5. Both fists raise then drive into the ground; on impact drop Hips 25 cm
over 0.1 s and recover over 0.4 s. That hip drop is what makes the shockwave feel like it
came from the Warden rather than appearing near it.

---

# Two things worth doing before any of this

**1. Enemies share one material.** `ValleyBuilder.AttachModel` assigns a single material to
every renderer on a character, so all 29 of the Player's parts are one flat colour. Belts,
bracers, tunic and skin are indistinguishable, and in NEON and CHALK — which draw flat
unlit colour — the added detail is invisible entirely. Assigning per-part materials by
name would make every clothing part built so far actually visible. That is a bigger visual
win than any animation on this list.

**2. Tails are not animated.** Spitter and Darter both carry `Tail1–3`, correctly parented
and ready. `ProceduralAnimator` does not know the names. Adding a lagged follow — each
segment trailing the one before by a frame or two — is about fifteen lines and would make
both creatures look substantially more alive than any amount of extra geometry.
