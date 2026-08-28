# RPG Sandbox — project direction

> **STATUS: BRAINSTORM / EXPLORATORY.**
> Last updated 2026-08-21.
>
> Nothing below is locked except the code that already exists. The scope, the genre,
> the feature list and the long-term vision are all still being argued about. If you
> are a fresh session reading this: **do not treat anything here as a settled
> requirement, and do not start building without asking.** The user has said
> explicitly, more than once, that they want to think things through before code
> gets written.

---

## What this actually is

Not a game. A **sandbox platform** where people build their own worlds *and* author
the visual style those worlds are drawn in.

The distinctive idea — the thing nobody else is doing — is that **content and
appearance are fully separated, and both are user-authorable**. The same world can be
re-skinned instantly into a completely different visual language. The user calls these
styles **lenses**.

The user's own original framing: an 8-bit look and a stick-figure look have different
"mathematical shapes," and the engine should be able to render the same objects
through either. That intuition turned out to be correct and is now the architecture.

## Why it exists right now

A potential investor asked to be shown "something" by roughly **29 Aug 2026**. He is
interested in MMORPG. The user is interested in a scalable platform.

The agreed pitch framing: **"RPG is the first genre the platform supports."** That
answers his stated interest without abandoning the platform story. Multiplayer is
deliberately excluded — it cannot be evaluated in a laptop demo and is months of work.

Longer-term vision (aspirational, not planned): hackathons where participants build
games on the platform, then a physical arcade in Qatar featuring the winners. The
hackathon doubles as an education story, which opens Gulf institutional funding that a
"UGC platform" framing does not.

---

## What exists today (working, verified)

A browser RPG sandbox. Open `index.html` directly — no server, no build step, no
dependencies. Plain `<script>` tags on purpose.

One file per architectural layer:

| File | Layer | Job |
|---|---|---|
| `js/kinds.js` | Vocabulary | Every kind of thing and every constant. Pure data. |
| `js/world.js` | World | Grid, actors, save/load, snapshot |
| `js/rules.js` | Rules | Movement, collision, effects, combat, projectiles, death |
| `js/quest.js` | Quest | Condition, counter, reward |
| `js/render.js` | Render | Camera, the Frame, drawing primitives |
| `js/lens-flat.js`, `js/lens-outline.js` | Lenses | Two complete visual styles |
| `js/editor.js` | Tools | Paint, place, inspector |
| `js/main.js` | — | Loop and wiring |

Working: tile painting, actor placement with per-instance stat overrides, grid
movement, wall collision, water (slows), poison gas (damage on entry + per second +
slow), automatic melee combat, ranged monsters firing projectiles, projectiles blocked
by walls, death, a defeat-N quest with a reward, save/load to browser storage and file,
and a live style toggle.

Verified by 25 headless checks (rules engine) and by screenshotting both lenses.
**Not** verified interactively: mouse painting, inspector panel, keyboard play,
save/load buttons.

Style can be preselected via `index.html#lens=1`.

---

## Core concepts (settled — do not casually redesign these)

- **Cells, not pixels.** The world is measured in cells. The lens decides how many
  pixels a cell becomes. Storing pixels would bake one art style into the data
  permanently and destroy the whole idea.
- **Traits + values.** Objects are defined by composition — which trait sets they
  belong to — plus numeric fields. Not by rigid types.
- **Resources vs attributes.** `health` is a resource: stored, stays spent. `moveSpeed`
  and friends are attributes: never stored, rebuilt from base every frame. This is why
  walking out of poison gas needs zero cleanup.
- **Speed is steps-per-second**, not seconds-per-step, so multiplicative effects scale
  the right way.
- **Effects have timing**: `onEnter`, `everySecond`, `whileTouching`.
- **Additive vs multiplicative modes.** Multiplicative keeps a debuff meaningful
  against an upgraded character. (This was the user's own insight.)
- **Events are differences between states**, never stored. "Entered" and "died" are
  computed by comparing frames.
- **Lenses are pure.** Frame in, drawing out. No memory, no mutation. A bad lens can be
  ugly but can never break the game — which is what makes user-authored styles safe.
- **Lens fallback chain**: exact kind → trait → default. A lens can draw objects
  invented after it was written.
- **Play runs on a copy** of the world and throws it away on Stop, so playtesting never
  damages the level being built.
- **New content = new data, not new code.** The `brute` monster exists purely to prove
  this: a full second monster, zero lines of brute-specific code.

Full spec: `C:\Users\HP\.claude\plans\no-not-yet-you-cached-cerf.md`

---

## Open / unresolved (the actual brainstorm)

**The user's reaction to the first build: it works but looks too basic to show an
investor.** That is the live problem. Ranked options discussed, none chosen:

1. Juice and visual overhaul — hit flash, knockback, screen shake, damage numbers,
   death bursts, autotiled walls, lighting. Pure presentation, no rules change.
   Highest impact per hour.
2. Photo → sprite pipeline. The user **already owns** a tool that downsamples an image
   into averaged RGB squares. That is literally an 8-bit lens. It would fix the art
   problem and is a strong differentiator. Needs palette snapping and transparency.
3. More game — dash, drops, a three-phase boss with telegraphed attacks.
4. More lenses plus an in-app lens editor.

**Multi-genre vision (very early).** Could one system host Pong, Street Fighter,
Terraria, racing games? Analysis so far: the skeleton is identical across all of them
(bodies, contact, stats, tick, input); only five knobs vary — space model, gravity,
camera, what contact means, win condition. Proposed extension: add two more swappable
axes alongside Lens —

- **Motion** (grid-step / platformer / free-floating / vehicle)
- **Goal** (score / elimination / survival / laps / open-ended)

A game then becomes a *combination*, not a codebase. **2D only** — 3D is explicitly out
of reach. This is not planned work; it informs where the seams go.

**Design principles adopted during discussion:** constraint is the product, not the
limitation; design for the median user; composition over enumeration; Bitsy beat Dreams
because accessibility beats power.

---

## Working with this user

- **They are not a programmer** and say so plainly. They direct, they do not type code.
  Their strength is system modelling — they have independently derived trait systems,
  stat modifiers, and composition-over-types during conversation, and have corrected
  the assistant's design more than once.
- **Code style is a hard requirement.** Long descriptive names, `while` loops with
  explicit counters, no `!`, `continue`, `Set`, arrow functions, or spread. Comment the
  *why*. They must be able to read every file. Comments go in the same file as the
  code — never a separate English mirror file, which drifts and then lies.
- **They want to understand before building.** They have stopped work twice to insist on
  conceptual clarity first. Honour that. A walkthrough of `kinds.js` was done; the
  other files are still pending.
- **Explain, do not just deliver.** They want to be able to explain this project to an
  investor themselves rather than say "AI made it."
- Be straight about what is verified versus assumed, and about what will not work.
