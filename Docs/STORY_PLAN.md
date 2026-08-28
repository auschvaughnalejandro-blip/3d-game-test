# One Valley — the story around the arena

The five rounds wrapped in a beginning and an ending, so the demo plays as a journey
rather than a fight that starts on its own.

Written 24 Aug 2026 as a plan. **Built the same day** — all four acts are in and the
automated play-through walks the whole thing with no failures. What follows is the design
as it was written; the notes marked BUILT record what actually happened.

> **BUILT.** The whole story plays end to end in 58 seconds under
> `One Valley -> Run Self Test`: dungeon, refusal, acceptance, four rounds with coaching,
> the Warden, the eye, the Warden's Edge, the walk home, the last conversation, the road,
> the four-lens finale and the title card. Zero FAIL lines.
>
> Everything is still grey-box: primitives and flat colours, no Blender art yet. That was
> the plan — art is the part that degrades gracefully.

---

## The shape

Four acts. All four now exist.

| Act | Where | What happens | Built |
|---|---|---|---|
| I — The Dungeon | `DungeonBuilder`, z = −200 | Meet Orrin. He asks. You answer. | yes |
| II — The Valley | the arena we had | Rounds 1–4, with Orrin talking you through them | yes |
| III — The Vault | the room we had | The Warden, three phases, then the eye | yes |
| IV — The Road | valley again, then out | The new blade, the last words, the ending image | yes |

---

## Filling the first gap: who is the wizard, and why does he ask?

He needed a name and a reason, so here they are.

**Orrin, the Lensmaker.** He is the one who sealed the Warden in the Vault, a long time
ago, and he has not been able to unseal it since — the seal only opens from the outside
for someone who is not him. That is why he has to ask rather than simply go. It also
explains why he waits at the door and never follows you in.

The reason he is called the Lensmaker matters more than the reason he is a wizard: **he
is the one who can change how the world looks.** In the dungeon he keeps a brazier, and
standing near it and pressing the style key re-skins the world around you while he
remarks on it.

That is worth doing deliberately. The style toggle is the one thing this project has that
Roblox and RPG Maker do not, and right now it is a debug key with no story reason to
exist. Giving it to a character turns the platform's headline feature into something the
investor watches a character *do*, which is a much better thirty seconds than "and there's
also a button that changes the art."

---

## Act I — The Dungeon

### Where it goes

The valley runs from z = −46 (south) to z = +35 (north). The player currently spawns at
(0, 0.83, −32), near the south end. The Vault already lives far away at z = 182 as a
separate sealed room.

The dungeon copies that trick: its own room at its own origin, well away from everything
else, built from code the way the Vault is. Call the origin `DungeonOrigin`. The player
now starts *there* instead of in the valley.

Leaving the dungeon puts you at the valley's south end — the exact spot the player spawns
at today. So Act II begins in precisely the state we have already tested.

### How you get from the dungeon to the valley

Reuse the `Portal` script. It already rises, shimmers, detects the player, holds for a
beat and carries them somewhere — all built and all tested. Give it a different look (a
stone door and a stair, not a purple ring) so it reads as walking out of a dungeon rather
than teleporting.

This is a deliberate cheapness. Building a real walkable tunnel between two rooms is a day
we do not have, and nobody watching will care.

### The conversation

Walk near Orrin, a prompt appears — `E — speak`. Press it and the world's first dialogue
box opens.

> **ORRIN** — "You came down here armed. That tells me you already know what is under
> this valley."
>
> **ORRIN** — "The Warden. I put him there. I was younger and I thought sealing a thing
> was the same as solving it."
>
> **ORRIN** — "The seal opens from the outside, and never for the hand that made it. So I
> cannot go. But you can."
>
> **ORRIN** — "Will you go down and finish what I started?"
>
> `[Y] I'll go.`   `[N] Not today.`

### What happens if the player says no

This gap matters more than it looks, because **an investor will press No specifically to
see what happens.** So refusing must never dead-end the demo.

> **ORRIN** — "Then the valley keeps its dead a while longer. I will be here."

The door stays shut. The player keeps walking around the dungeon, can play with the
brazier, and can talk to Orrin again — the prompt re-arms. Accepting after refusing gets a
different, warmer line:

> **ORRIN** — "You went away and thought about it. Good. The ones who say yes straight
> away are the ones I bury."

Cheap to build, and it makes the dialogue feel like it has a memory rather than a script.

---

## Act II — The Valley, with a voice

The rounds already work. What is missing is that nothing tells the player what to do, and
the whole point of a demo is that a stranger can play it without you leaning over their
shoulder.

Orrin's voice carries into the valley. Every line below is triggered by something the code
*already does* — no new game logic, just a line attached to an event that already fires.

| When | Fires from | Line |
|---|---|---|
| You step out into the valley | door crossed | "The Approach. They come from every side here. Do not back away — turn." |
| Round 1 begins | `StartRound(1)` | "Grunts. Slow, heavy, and they do not flinch. The HAMMER breaks them — press F to change weapon." |
| First essence drops | first `CollectEssence` | "That light is essence. Take it. It buys what you will not survive without." |
| Round 1 cleared | intermission after round 1 | "The shrine, behind you. Stand near it. **1** for vigour, **2** for strength, **3** for wind." |
| Round 2 begins | `StartRound(2)` | "Darters. Once they commit to a charge they cannot turn. Step ASIDE. Never backwards." |
| First Spitter appears | first Spitter spawned | "Spitters, up on the high stone. No blade reaches them. The BOW — hold to draw, release to loose, and aim ABOVE them, not at them." |
| Pillars rise | `RaisePillars` | "The stone rises for you. Keep it between yourself and their arc." |
| Round 4 begins | `StartRound(4)` | "All of them at once. There is no lesson left in this one. Only what you kept." |
| Portal opens | `portal.Open()` | "That is the Vault. He is behind it. Go, or do not — but I cannot follow you in." |
| Warden hits phase 2 | `EnterPhase(2)` | "He is hurt, and hurt has made him clever. He throws now. Keep stone at your shoulder." |
| Warden hits phase 3 | `EnterPhase(3)` | "He is calling them to him. Kill *him*. Ignore the rest." |
| Warden dies | `theWardenIsDead` | "It is done. Take what is in his chest. It was never his to begin with." |

Two things to notice about this table. First, it teaches all three weapons at the moment
each one becomes necessary, rather than in a tutorial nobody reads. Second, it costs
almost nothing to build — it is one small script listening to events that already exist.

**One real problem here — FIXED.** The gap between rounds was 10 seconds, which is not
enough time for a first-time player to hear "go to the shrine", find it, walk over and read
three upgrade options. A timer that runs out mid-explanation is exactly the kind of thing
that makes a live demonstration go badly.

`RoundDirector` now refuses to end an intermission while the player is still standing at
the shrine. Walking away is what says they are finished, which needs no explaining to
anybody.

---

## Act III — The gem

The Warden dies. Where he falls, **the Warden's Eye** is left behind — a purple stone,
hovering and turning, lit the same violet as the portal so it reads as belonging to the
Vault rather than to him.

Walk into it and the fight music stops. The screen holds. Then:

> **WARDEN'S EDGE** — *a blade of the Vault's own light*

### What the blade actually does

You asked for greater range, sweeping attacks, and more power. Here is how that lands
mechanically, and one of these is a genuine change to how combat works:

| | Sword | Hammer | Warden's Edge |
|---|---|---|---|
| Damage | 20 | high | ~40 |
| Reach | 2.6 | 3.2 | **5.0** |
| Swing arc | ~100° | ~110° | **200°** |
| Heavy attack | harder hit | harder hit | **full 360° spin** |

The arc is the interesting one. Right now `PlayerCombat` finds targets with a sphere
placed in front of the player and hits *everything inside it* — there is no notion of
which direction the swing went. Adding an arc angle to each weapon does two things at
once: it makes the sword and hammer slightly more precise and honest, and it makes the
Edge feel genuinely different, because one swing catches a whole ring of enemies standing
around you instead of the two in front.

That is the moment worth building for. After four rounds of being surrounded and having to
pick them off, you swing once and clear the circle.

### Teach it before the door opens

The exit does not open the instant you pick up the gem. It opens after you have swung the
blade once. It is a two-second delay that guarantees nobody leaves the Vault without
having felt the new weapon — which is otherwise very easy to miss in a demo where somebody
else is driving.

---

## Act IV — The road out

This was the biggest gap in the brief. "He continues further in his journey" needs to
actually be something the investor sees, or the demo just stops.

1. A second portal opens at the far end of the Vault and returns you to the valley.
2. **The valley has changed.** No enemies. No barriers. The Gate at z = 33 — a solid wall
   through the whole demo — is standing open, and it uses the same rising-barrier code
   already written for the zone barriers, so this is nearly free.
3. Orrin is waiting where you first came out.

> **ORRIN** — "You are carrying his eye. I can see it from here."
>
> **ORRIN** — "That was one valley. There are others, and they are not all like this one."
>
> **ORRIN** — "Where will you go?"
>
> `[Y] North.`   `[N] Wherever it's quiet.`

Both answers lead the same way — the question is there for the character, not the branch.

4. Walk north through the open gate onto a short road, and the camera lifts.
5. **The world re-skins itself, once through each lens**, and holds on the last one.
6. Title card: **ONE VALLEY** — *the first of many.*

That closing line is doing real work at the investor meeting. "The first of many" is the
platform pitch said out loud by the game itself, and the re-skin right before it is the
proof. It costs one line of text and a scripted call into `StyleLens`, which is already
written.

---

## What has to be built

### New scripts

| File | Job |
|---|---|
| `DialogueBox.cs` | One speaker, one line, an optional yes/no. Drawn in `OnGUI` alongside the existing HUD. |
| `StoryDirector.cs` | Which act we are in. Remembers whether Orrin was refused. |
| `Wizard.cs` | Proximity prompt, hands lines to the dialogue box. Mirrors `ShrineOfEssence`. |
| `CoachLines.cs` | The trigger table above, and nothing else. |
| `WardenGem.cs` | The pickup. Unlocks the blade. Mirrors `EssencePickup`. |
| `DungeonBuilder.cs` | Builds the dungeon room from code, the way `ValleyBuilder` builds the valley. |

### Existing scripts that change

| File | Change |
|---|---|
| `GameInput.cs` | Add `InteractWasPressed()` on **E**, and yes/no on **Y**/**N**. Same shape as the existing key checks. |
| `RoundDirector.cs` | Stop auto-starting round 1 after 3 seconds. Wait to be told. Hold the first intermission until the player leaves the shrine. |
| `PlayerWeapons.cs` | A fourth `WeaponKind`, locked until the gem, plus `swingArcDegrees` on all four. |
| `PlayerCombat.cs` | Respect the swing arc instead of hitting everything in the sphere. |
| `HudDisplay.cs` | Draw the dialogue box and the interact prompt. |
| `ValleyBuilder.cs` | Call the dungeon builder, open the north gate at the end, build the road. |
| `SelfTest.cs` | Cover the whole new path, both dialogue answers included. |

Nothing here needs a new UI framework, a save system, or an animation system. That is
deliberate — every piece leans on something already proven in this project.

---

## What Blender is for

Unity's own asset generation returns `NoSubscription`, so Blender is the art pipeline.
Priority order, because this is the part most likely to run out of days:

1. **Orrin** — robed figure with a staff. **Do not rig him.** A static posed model with a
   slow float and a glow reads fine at conversation distance and saves a day.
2. **Warden's Edge** — the blade. Must attach the same way the sword, hammer and bow do
   (the `modelPartName` pattern), or it will not appear in the player's hand.
3. **The Warden's Eye** — a small faceted stone. An hour's work, and it is the emotional
   centre of Act III.
4. **Dungeon kit** — archway, wall panel, pillar, stair block, brazier, door slab. Six
   modular pieces repeated, not a bespoke room.

Poly Haven is already enabled and has excellent CC0 textures (no rock *models*), and the
existing materials already pull from `Textures/Cliff`, so the dungeon can borrow the
valley's stone and still look like a different place.

---

## One thing that has to be fixed first

The self-test is currently failing, intermittently, and it will kill a live demo:

```
run at 15:42 — ROUND 2 FAIL: Grunt(-2.81, -2.33, 31.08) → (-2.81, -3.11, 31.08)
run at 15:46 — ROUND 4 FAIL: Darter(-3.73, -2.28, 34.87) → (-3.73, -2.92, 34.87)
             — 1 enemy dropped below the world
```

An enemy falls out of the world and never dies, so **the round never ends.** It moved from
round 2 to round 4 between two runs, which means it is random, not tied to a round.

The leading hypothesis, from the coordinates: both failures are at x ≈ −3, z ≈ 31–35. The
Gate is a solid box at z = 33 spanning x = −5 to +5, and the portal sits at z = 30. Both
dead enemies were inside that footprint. Something — knockback, a charge, or a spawn — is
pushing enemies into or behind the Gate, where there is no navigation mesh and, apparently,
no floor.

I have not confirmed this; it needs a play session with the MCP bridge to watch it happen.
But it should be fixed before any story work goes on top, because a round that never ends
means the demo stops dead with an investor watching, and no amount of dialogue survives
that.

---

## Order of work

Five days. Ordered so that stopping early still leaves something showable.

| | Work | Status |
|---|---|---|
| 0 | Fix the falling enemy | **DONE** — see below |
| 1 | Dialogue box, interact key, StoryDirector, Orrin | **DONE** |
| 2 | Coach lines in the arena | **DONE** — 12 lines, all on existing events |
| 3 | Gem, Warden's Edge, the swing arc | **DONE** — arc added to all four weapons |
| 4 | Road out, gate opening, lens finale, title card | **DONE** |
| 5 | Blender art, in the priority order above | **NOT STARTED** — the remaining work |
| 6 | Extend the self-test over the whole path | **DONE** — including the refusal branch |

### What the falling enemy actually was

Not a hole in the ground. The valley floor is a **one-sided mesh collider**, and enemies
crowded against the gate get shoved through it. Underneath there is nothing to stand on
and no way back up, so they sink slowly at about y = -3 for the rest of the round.

The first attempt compared each creature against the height of the ground above it. That
does not work here: a ray fired downwards at that spot misses the terrain and finds the
**buried portal frame**, so the floor appears to be metres below the creature and it never
looks buried at all. The working test asks the floor mesh for its own lowest point, and
the rescue puts the creature in the middle of the arena rather than back into the hole
that swallowed it.

The principle behind that order: **build the entire story grey-box first.** A complete
journey in untextured boxes is a far better investor demo than a beautiful dungeon
attached to a fight that still begins on its own after three seconds. Art is the only part
that degrades gracefully.

---

## Menus and saving

Added after the story was finished, so the demo boots like a game rather than dropping
straight into a dungeon.

**The title screen is drawn over the living scene.** The camera is already sitting behind
the player looking down the hall at the lit doorway, so the menu is a darkening layer and
some text over the top of that. No second scene, nothing to keep in step, and a better
title card than anything that could be painted.

| Button | What it does |
|---|---|
| Continue | Loads the last checkpoint. Greyed out, and labelled *no saved game yet*, when there is nothing to load. Otherwise it says where you were and when: *Round 3 - CROSSFIRE  -  24 Aug 2026, 18:33* |
| New Game | Deletes the save, resets health, damage, stamina and essence, and puts you back in the dungeon |
| Quit | Closes the game |

**Escape now pauses** rather than just releasing the mouse. Resume, *Save and Quit to
Title*, and *Quit to Desktop* - the last of which saves on the way out, so closing the
window is never the thing that loses a run.

### What gets saved, and when

Checkpoints, not a snapshot of the world. Saving where every enemy was standing would be a
lot of work, would break whenever the valley changed, and would let somebody reload into
the middle of a fight they were already losing.

Written at: accepting the task, **the start of every round**, the Warden dying, and coming
home. So quitting never costs more than the round you were in.

Kept: which act, whether Orrin was refused, which round, essence, the three shrine
upgrades, whether the Edge has been won, whether the Warden is dead.

Deliberately **not** kept: current health and stamina. Coming back to a checkpoint on four
health would be a punishment for having stopped playing.

The file is plain JSON at
`C:/Users/HP/AppData/LocalLow/DefaultCompany/My project/onevalley-save.json`, so it can be
opened in Notepad - which matters the first time a save goes wrong. Deleting it is the
same as never having played.

### One thing worth knowing

Every script that reads the keyboard now asks `PlayerControl.IsBlocked()` rather than
asking the dialogue box directly. Before this there were six separate places checking "is
somebody talking", and adding "or is a menu up" to all six by hand is exactly the sort of
job that gets half done and leaves the player able to swing a sword through the title
screen.

---

## Decisions I made that you should overrule if they are wrong

1. **Refusing Orrin is flavour only** — no stat penalty, no locked content. I assumed you
   want a choice that characterises the player, not one that punishes a demo viewer.
2. **Text only, no voice acting.** Five days.
3. **The blade is a fourth weapon, not a replacement.** You keep sword, hammer and bow.
4. **Dungeon to valley is a disguised teleport**, not a walkable tunnel.
5. **Orrin is not rigged and does not walk.** He stands, floats slightly, and glows.
