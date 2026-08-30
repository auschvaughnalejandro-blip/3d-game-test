# Session brief — Unity and C#

Paste this whole file as the first message of the new session.

---

You are the **Unity and C# session** for One Valley. A second Claude Code session is
running in parallel and owns Blender. Read `CLAUDE.md` first — it holds the project's
hard-won rules and they are all still in force.

## Your boundary — this matters more than anything else below

**You own `Assets/Scripts/`.** All C#, all Unity editor work, all play-testing.

**You must not touch:**
- Blender, or the socket on port 9876. Not to check, not to peek. The other session has
  a live scene open and every build script starts with `clear_scene()`.
- `Tools/*.py` — those are the other session's build scripts.
- `Assets/Resources/Models/*.fbx` — if a mesh is wrong, **say so to the user** and they
  will route it. Do not rebuild it yourself.

We ran two sessions in parallel earlier tonight without this boundary and lost hours:
one session edited the other's shared helper file mid-build, and a finished component sat
unwired for hours because each session assumed the other would do it.

## Where things actually stand

**Animation exists but is barely wired.** `Assets/Scripts/ProceduralAnimator.cs` animates a
segmented character with no bones — body parts are plain child Transforms, rotated in
`LateUpdate`. It covers **walk, idle and death only**. Stride is driven by measured speed
from the transform delta, so knockback and lunges drive the legs correctly.

**`ValleyBuilder.AttachModel` now prefers `Models/<Name>Segmented` over `Models/<Name>`**
and attaches `ProceduralAnimator` when it loads one. Choice is by file presence, so each
new segmented export lights up with no code change.

**Only `GruntSegmented.fbx` exists so far.** The other session is building Spitter, Darter,
Warden and Player. Everything else still loads the old single-mesh model and behaves
exactly as before.

**A rotation fix has just been made and is UNVERIFIED.** The first play-test showed the
Grunts lying flat on the ground doing a swimming motion. Cause: segmented meshes are
exported with `bake_space_transform=False` (mandatory — baking it destroys the per-part
joint origins), so Blender writes a compensating −90° rotation onto the model root, and
`AttachModel` was clearing it with `localRotation = Quaternion.identity`. It now preserves
the prefab's rotation instead. **Confirming this is your first job.**

## The interface contract with the Blender session

Do not change either side of this without telling the user.

- Segmented meshes are named **`<Name>Segmented.fbx`** in `Assets/Resources/Models/`.
- `ProceduralAnimator` finds parts **by name**: `Hips`, `Torso`, `Head`, `ThighL`,
  `ThighR`, `ShinL`, `ShinR`, `UpperArmL`, `UpperArmR`, `ForearmL`, `ForearmR`. A missing
  part is guarded and simply does nothing, so a partial model animates as far as it can.
- Segmented exports use `bake_space_transform=False`. **Never force `localRotation` to
  identity on a segmented model**, and never flatten its children's `localScale` — those
  children are the body parts and their transforms are load-bearing.

## Your tasks, in priority order

**1. Verify the rotation fix.** Stop play mode, let Unity recompile, play, look at a Grunt.
   - Standing and walking → done, move on.
   - Standing but walking *backwards* → flip `forwardSwingSign` from `-1` to `1` in the
     `ProceduralAnimator` inspector. It is exposed for exactly this; Blender and Unity
     disagree about handedness and the pitch sign can survive the export inverted.
   - Still flat → the compensating rotation is on the `Hips` child rather than the model
     root. Query the imported hierarchy rather than guessing.

**2. Attack animation. This is the biggest gap in the game.** `ProceduralAnimator` has no
attack motion at all, so every enemy swing currently has no movement behind it. `EnemyBrain`
already owns the wind-up and strike by leaning the model root — your animation must layer
*under* that by posing the root's children, exactly as the walk does. Never write to the
animator's own transform; that is `EnemyBrain`'s.

**3. `PlayDeath()` is public and nothing calls it.** Wire it to actual enemy death.

**4. The weapons are modelled but nothing loads them.** `Sword.fbx`, `Hammer.fbx`,
`Bow.fbx`, `Arrow.fbx` and `WardensEdge.fbx` are all in `Assets/Resources/Models/`, but
`BuildThePlayer` still assembles every weapon from grey primitive cubes — `SwordBlade`,
`HammerHaft`, `BowGrip` and the rest. Swap them over.

   **`WardensEdge.fbx` contains two meshes on purpose:** `WardensEdge` (dark iron) and
   `WardensEdgeCore` (the channel and the guard stone). The core needs an **emissive violet
   `#B26BFF`** material while the iron stays dark. That separation is the whole reason it
   was exported as a group — do not merge them.

**5. The Spitter still passes `"Grunt"` as its model name** at roughly
`ValleyBuilder.cs:1653`, so the ranged enemy is visually identical to the melee one and
round three reads as unfair. Leave the line alone until `SpitterSegmented.fbx` lands, then
point it at the Spitter.

## Verifying without Unity

Compile-check any C# change through Unity's Roslyn without needing the editor focused,
unlocked or out of play mode. Takes about a minute and catches real errors:

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

Expect **0 errors and 42 warnings** — the warnings are all pre-existing
`FindFirstObjectByType` deprecation noise. Do not reference `Managed/UnityEditor.dll`; it
collides with `UnityEditor.CoreModule.dll` and fakes a CS0433 on `EditorApplication`.

## Two rules learned the hard way tonight

**Never edit a `.cs` file while a play test is running.** Auto-refresh compiles it mid-play,
the script domain reloads, every scene object is destroyed under the running game and
singletons go null. It throws once per frame and looks exactly like a catastrophic game bug.
Announce your play-tests so the other session does not edit under you.

**Get one thing past the user's eye before building ten.** This session's parallel partner
produced six finished assets before the user saw a single one, and none of them were what
was actually wanted. Show your first result and wait.
