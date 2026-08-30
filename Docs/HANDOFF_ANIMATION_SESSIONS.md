# Two session briefs — animation work

Copy ONE of the two blocks below as the first message of a new session. They are split by
which files they own, so they cannot overwrite each other.

Read `Docs/ANIMATION_SPEC.md` first in both cases — it has the per-action detail, and its
status list was corrected on 30 Aug after a lot of it turned out to be already built.

---

# SESSION A — Player animations and tail follow

You are the **player animation session** for One Valley. Read `CLAUDE.md` first.

## You own exactly two things

**`Assets/Scripts/ProceduralAnimator.cs`** and a new **`Assets/Scripts/PlayerAnimator.cs`**.

Do not edit `ValleyBuilder.cs`, `WardenBoss.cs` or `EnemyBrain.cs` — another session owns
those. If you need a hook in one of them, say so and it will be added for you. Do not
touch Blender, `Tools/*.py`, or any `.fbx`.

## Where things stand

`ProceduralAnimator.cs` is 577 lines and already good. It has an **additive pose stack**:
`ClearThePose()` zeroes one float accumulator per part, each pose function *adds* its
offset, and `ApplyThePose()` writes them once at the end of `LateUpdate`. Work with that
pattern — never assign `localRotation` directly, or you will erase every other animation
running that frame.

Walk, idle, enemy attack and death all work. **No Player script references the animator at
all**, which is why the player is motionless apart from walking.

## Your tasks

1. **`PlayerAnimator.cs`** — the counterpart to `EnemyBrain`'s driving of the animator.
   It reads player state and calls into `ProceduralAnimator`. `PlayerMovement` already
   exposes `IsCurrentlyDodging()`, `IsAirborne()` and `VerticalSpeed()`; the other actions
   live in `PlayerCombat`, `PlayerSurge`, `PlayerHealing` and `PlayerWeapons`.

2. **Seven animations**, specced in `ANIMATION_SPEC.md` under "Player": sprint, jump,
   dodge, light attack, heavy attack, surge, potion, weapon swap, hit reaction.

   Start with **dodge**. It is the most visible, it has a real duration already
   (`dodgeLastsSeconds` 0.35), and `dodgeSpeed` 16 against `walkingSpeed` 5.5 means it
   currently looks like the player teleports.

3. **Tail follow** for Spitter and Darter. Both models carry `Tail1`, `Tail2`, `Tail3`,
   correctly parented, and the animator does not know the names. Each segment lags the one
   before it by a frame or two. About fifteen lines, and it will make both creatures look
   more alive than any amount of extra geometry would.

## Rules

- **Timings come from existing fields, never new numbers.** `dodgeLastsSeconds`,
  `windUpSeconds`, `strikeSeconds`. An animation that invents its own duration drifts out
  of step with the hitbox it is selling, and players read that as the game lying to them.
- **Never write to the animator's own transform.** It owns its root's CHILDREN. The root
  belongs to `EnemyBrain` / `PlayerMovement`.
- **`forwardSwingSign`** exists because the pitch sign survives FBX export inconsistently.
  Resolve it once; do not add a second sign flag.

---

# SESSION B — Materials and the Warden

You are the **materials and boss session** for One Valley. Read `CLAUDE.md` first.

## You own exactly these

**`Assets/Scripts/ValleyBuilder.cs`**, **`Assets/Scripts/WardenBoss.cs`**, and
**`Assets/Scripts/EnemyBrain.cs`**.

Do not edit `ProceduralAnimator.cs` or `PlayerAnimator.cs` — another session owns those.
If you need a new pose function, ask for it rather than adding one. Do not touch Blender,
`Tools/*.py`, or any `.fbx`.

## Task 1 — per-part materials. Do this first; it is the biggest visual win available.

`ValleyBuilder.AttachModel` currently does this to every renderer on a character:

```csharp
surfaces[surfaceIndex].material = material;
```

One material for the whole creature. The Player has **29 parts** — belt, buckle, tunic
skirt, collar, chest strap, bracers, boot cuffs, shoulder guard, hair — and every one of
them is currently the same flat `#4D6B9E` as the skin. All of that detail is invisible.
It is worse in NEON and CHALK, which draw flat unlit colour with no shading, so even the
shape difference disappears.

Assign by part name instead. The palette is in `ASSET_BIBLE.md` section 0.4:

| Part name contains | Material |
|---|---|
| `Eye` | enemy eye `#FFD94D`, emissive |
| `HeadSlot` | Vault violet `#8C38F2`, emissive |
| `Hair` | dark brown |
| `Buckle` | pale metal |
| `Guard`, `Slot` | dark iron `#2B2B33` |
| `Belt`, `Strap`, `Bracer`, `Cuff`, `Pouch`, `Foot`, `Boot` | leather |
| `Head`, `Hand`, `Brow`, `Nose` (player only) | skin |
| everything else | the creature's body colour |

`Tools/preview_coloured.py` already implements exactly this mapping in Python — read it
for the rules and the hex values rather than re-deriving them.

**Keep it compatible with the style lens.** `StyleLens.cs` recolours materials per lens;
check how it enumerates renderers before you multiply their number.

## Task 2 — the Warden's four moves

Specced in `ANIMATION_SPEC.md` under "Warden": charge, throw, summon, slam. Drive them
from `WardenBoss.cs` through the animator hooks that already exist — `ShowWindUp(progress)`
and `ShowStrike(progress)` — with boss-appropriate timings, rather than adding new pose
functions.

The one that matters most: **on the slam, drop `Hips` 25 cm over 0.1 s and recover over
0.4 s.** `slamRadius` is 5.5, and that hip drop is what makes the shockwave feel like it
came from the Warden rather than merely appearing near it.

## Rules

- Timings come from existing `EnemyBrain` fields: `windUpSeconds`, `strikeSeconds`,
  `damageLandsAfterSeconds`, `slamRadius`.
- `CursorControl.cs` is still the only file allowed to write `Cursor.lockState` or
  `Cursor.visible`. Nothing here should go near it.

---

# For both sessions

**Never edit a `.cs` file while a play test is running.** Auto-refresh compiles it
mid-play, the domain reloads, every scene object is destroyed under the running game and
singletons go null. It throws once per frame and looks like a catastrophic bug. Announce
your play-tests so the other session does not edit under you.

**Compile-check without Unity** — works with the editor unfocused, locked or mid-play, and
takes about a minute:

```bash
PROJ="C:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project"
UNITY="C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Data"
RSP=/tmp/compile.rsp

{
  echo "-target:library"; echo "-nostdlib+"; echo "-noconfig"; echo "-out:/tmp/Check.dll"
  for d in "$UNITY/Managed/UnityEngine/"*.dll;      do echo "-r:\"$d\""; done
  for d in "$UNITY/NetStandard/ref/2.1.0/"*.dll;    do echo "-r:\"$d\""; done
  for d in "$PROJ/Library/ScriptAssemblies/"*.dll;  do
    case "$(basename "$d")" in Assembly-CSharp*) continue;; esac
    echo "-r:\"$d\""
  done
  for s in "$PROJ/Assets/Scripts/"*.cs "$PROJ/Assets/TutorialInfo/Scripts/"*.cs; do
    [ -e "$s" ] && echo "\"$s\""
  done
} > "$RSP"

"/c/Program Files/dotnet/dotnet" \
  "$UNITY/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" "@$RSP"
```

Expect **0 errors**; the warnings are all pre-existing `FindFirstObjectByType` deprecation
noise. Do not reference `Managed/UnityEditor.dll` — it collides with
`UnityEditor.CoreModule.dll` and fakes a CS0433 on `EditorApplication`.

**Show one thing before building ten.** Earlier in this project a session produced six
finished assets before the user saw a single one, and none of them were wanted.
# Two session briefs — animation work

Copy ONE of the two blocks below as the first message of a new session. They are split by
which files they own, so they cannot overwrite each other.

Read `Docs/ANIMATION_SPEC.md` first in both cases — it has the per-action detail, and its
status list was corrected on 30 Aug after a lot of it turned out to be already built.

---

# SESSION A — Player animations and tail follow

You are the **player animation session** for One Valley. Read `CLAUDE.md` first.

## You own exactly two things

**`Assets/Scripts/ProceduralAnimator.cs`** and a new **`Assets/Scripts/PlayerAnimator.cs`**.

Do not edit `ValleyBuilder.cs`, `WardenBoss.cs` or `EnemyBrain.cs` — another session owns
those. If you need a hook in one of them, say so and it will be added for you. Do not
touch Blender, `Tools/*.py`, or any `.fbx`.

## Where things stand

`ProceduralAnimator.cs` is 577 lines and already good. It has an **additive pose stack**:
`ClearThePose()` zeroes one float accumulator per part, each pose function *adds* its
offset, and `ApplyThePose()` writes them once at the end of `LateUpdate`. Work with that
pattern — never assign `localRotation` directly, or you will erase every other animation
running that frame.

Walk, idle, enemy attack and death all work. **No Player script references the animator at
all**, which is why the player is motionless apart from walking.

## Your tasks

1. **`PlayerAnimator.cs`** — the counterpart to `EnemyBrain`'s driving of the animator.
   It reads player state and calls into `ProceduralAnimator`. `PlayerMovement` already
   exposes `IsCurrentlyDodging()`, `IsAirborne()` and `VerticalSpeed()`; the other actions
   live in `PlayerCombat`, `PlayerSurge`, `PlayerHealing` and `PlayerWeapons`.

2. **Seven animations**, specced in `ANIMATION_SPEC.md` under "Player": sprint, jump,
   dodge, light attack, heavy attack, surge, potion, weapon swap, hit reaction.

   Start with **dodge**. It is the most visible, it has a real duration already
   (`dodgeLastsSeconds` 0.35), and `dodgeSpeed` 16 against `walkingSpeed` 5.5 means it
   currently looks like the player teleports.

3. **Tail follow** for Spitter and Darter. Both models carry `Tail1`, `Tail2`, `Tail3`,
   correctly parented, and the animator does not know the names. Each segment lags the one
   before it by a frame or two. About fifteen lines, and it will make both creatures look
   more alive than any amount of extra geometry would.

## Rules

- **Timings come from existing fields, never new numbers.** `dodgeLastsSeconds`,
  `windUpSeconds`, `strikeSeconds`. An animation that invents its own duration drifts out
  of step with the hitbox it is selling, and players read that as the game lying to them.
- **Never write to the animator's own transform.** It owns its root's CHILDREN. The root
  belongs to `EnemyBrain` / `PlayerMovement`.
- **`forwardSwingSign`** exists because the pitch sign survives FBX export inconsistently.
  Resolve it once; do not add a second sign flag.

---

# SESSION B — Materials and the Warden

You are the **materials and boss session** for One Valley. Read `CLAUDE.md` first.

## You own exactly these

**`Assets/Scripts/ValleyBuilder.cs`**, **`Assets/Scripts/WardenBoss.cs`**, and
**`Assets/Scripts/EnemyBrain.cs`**.

Do not edit `ProceduralAnimator.cs` or `PlayerAnimator.cs` — another session owns those.
If you need a new pose function, ask for it rather than adding one. Do not touch Blender,
`Tools/*.py`, or any `.fbx`.

## Task 1 — per-part materials. Do this first; it is the biggest visual win available.

`ValleyBuilder.AttachModel` currently does this to every renderer on a character:

```csharp
surfaces[surfaceIndex].material = material;
```

One material for the whole creature. The Player has **29 parts** — belt, buckle, tunic
skirt, collar, chest strap, bracers, boot cuffs, shoulder guard, hair — and every one of
them is currently the same flat `#4D6B9E` as the skin. All of that detail is invisible.
It is worse in NEON and CHALK, which draw flat unlit colour with no shading, so even the
shape difference disappears.

Assign by part name instead. The palette is in `ASSET_BIBLE.md` section 0.4:

| Part name contains | Material |
|---|---|
| `Eye` | enemy eye `#FFD94D`, emissive |
| `HeadSlot` | Vault violet `#8C38F2`, emissive |
| `Hair` | dark brown |
| `Buckle` | pale metal |
| `Guard`, `Slot` | dark iron `#2B2B33` |
| `Belt`, `Strap`, `Bracer`, `Cuff`, `Pouch`, `Foot`, `Boot` | leather |
| `Head`, `Hand`, `Brow`, `Nose` (player only) | skin |
| everything else | the creature's body colour |

`Tools/preview_coloured.py` already implements exactly this mapping in Python — read it
for the rules and the hex values rather than re-deriving them.

**Keep it compatible with the style lens.** `StyleLens.cs` recolours materials per lens;
check how it enumerates renderers before you multiply their number.

## Task 2 — the Warden's four moves

Specced in `ANIMATION_SPEC.md` under "Warden": charge, throw, summon, slam. Drive them
from `WardenBoss.cs` through the animator hooks that already exist — `ShowWindUp(progress)`
and `ShowStrike(progress)` — with boss-appropriate timings, rather than adding new pose
functions.

The one that matters most: **on the slam, drop `Hips` 25 cm over 0.1 s and recover over
0.4 s.** `slamRadius` is 5.5, and that hip drop is what makes the shockwave feel like it
came from the Warden rather than merely appearing near it.

## Rules

- Timings come from existing `EnemyBrain` fields: `windUpSeconds`, `strikeSeconds`,
  `damageLandsAfterSeconds`, `slamRadius`.
- `CursorControl.cs` is still the only file allowed to write `Cursor.lockState` or
  `Cursor.visible`. Nothing here should go near it.

---

# For both sessions

**Never edit a `.cs` file while a play test is running.** Auto-refresh compiles it
mid-play, the domain reloads, every scene object is destroyed under the running game and
singletons go null. It throws once per frame and looks like a catastrophic bug. Announce
your play-tests so the other session does not edit under you.

**Compile-check without Unity** — works with the editor unfocused, locked or mid-play, and
takes about a minute:

```bash
PROJ="C:/Users/Mark Alejandro/OneValley/OneValley-Transfer/unity-project"
UNITY="C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Data"
RSP=/tmp/compile.rsp

{
  echo "-target:library"; echo "-nostdlib+"; echo "-noconfig"; echo "-out:/tmp/Check.dll"
  for d in "$UNITY/Managed/UnityEngine/"*.dll;      do echo "-r:\"$d\""; done
  for d in "$UNITY/NetStandard/ref/2.1.0/"*.dll;    do echo "-r:\"$d\""; done
  for d in "$PROJ/Library/ScriptAssemblies/"*.dll;  do
    case "$(basename "$d")" in Assembly-CSharp*) continue;; esac
    echo "-r:\"$d\""
  done
  for s in "$PROJ/Assets/Scripts/"*.cs "$PROJ/Assets/TutorialInfo/Scripts/"*.cs; do
    [ -e "$s" ] && echo "\"$s\""
  done
} > "$RSP"

"/c/Program Files/dotnet/dotnet" \
  "$UNITY/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" "@$RSP"
```

Expect **0 errors**; the warnings are all pre-existing `FindFirstObjectByType` deprecation
noise. Do not reference `Managed/UnityEditor.dll` — it collides with
`UnityEditor.CoreModule.dll` and fakes a CS0433 on `EditorApplication`.

**Show one thing before building ten.** Earlier in this project a session produced six
finished assets before the user saw a single one, and none of them were wanted.
