# One Valley — Unity 3D RPG vertical slice

An investor demo, not a shipping game. Second demo alongside the browser sandbox
(`Desktop/games`). Started 23 Aug 2026.

## Layout

- Unity **6000.5.9f1**, **URP**, single scene `Assets/Scenes/SampleScene.unity`.
- Gameplay scripts: `Assets/Scripts/*.cs` (this is exactly `Assembly-CSharp`).
- Editor-only scripts: `Assets/Editor/*.cs` (a *separate* assembly).
- Design docs Claude wrote: `Docs/` — `ASSET_BIBLE.md`, `STORY_PLAN.md`,
  `PATHFINDING_MATHS.md`, `BLENDER_ASSET_PLAN.md`, `plan.md`.
- Saves: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project/onevalley-save.json`.
  Delete it to get a clean "no saved game" state.

## Code style

Plain, verbose C# the user can read and verify line by line. No clever one-liners,
no dense LINQ chains. Name things for what they do.

## Hard-won rules — these are silent failures, not tooling bugs

**Input**

- The project uses the **new Input System package**. `UnityEngine.Input` throws at
  runtime. All input goes through `Assets/Scripts/GameInput.cs`.
- Anything reading the keyboard must ask `PlayerControl.IsBlocked()` — never
  `DialogueBox.ConversationIsOpen()` directly.

**API gotchas**

- `Object.GetInstanceID()` is a compile error in this Unity version — use `GetEntityId()`.
- Unity's cylinder primitive carries a **capsule** collider. Flattened into a wide disc
  it becomes a huge invisible dome that launches anything standing on it into the air.
  Strip colliders from decorative cylinders.

**Editing while Unity runs**

- **Never edit a `.cs` file while a play test is running.** Auto-refresh compiles it
  mid-play, Unity reloads the script domain, every scene object is destroyed under the
  running game, singletons like `RoundDirector.instance` go null, and it throws once per
  frame. It looks exactly like a game bug and is not one.
- Unity stops auto-refreshing while unfocused, so edited `.cs` files are simply never
  compiled. Call `AssetDatabase.Refresh`, then wait on the DLL timestamp.
- The MCP bridge drops on every domain reload. Wait until
  `Library/ScriptAssemblies/Assembly-CSharp.dll` is newer than the edited `.cs`, then retry.

**Compile-checking without Unity** — the way around every "bridge is down / editor is
unfocused / editor is mid-play" problem above. Unity ships Roslyn at
`Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll`; run it with `dotnet.exe`.
Compile `Assets/Scripts/*.cs` + `Assets/TutorialInfo/Scripts/*.cs` with
`-target:library -nostdlib+ -noconfig`, referencing `Editor/Data/Managed/UnityEngine/*.dll`,
`Editor/Data/NetStandard/ref/2.1.0/*.dll`, and every `Library/ScriptAssemblies/*.dll`
**except** `Assembly-CSharp*.dll`. Do **not** also reference `Managed/UnityEditor.dll` —
it collides with `UnityEditor.CoreModule.dll` and fakes a CS0433 on `EditorApplication`.
Takes about a minute and catches real errors.

**Physics / the falling-through-the-floor family**

- Every imported floor is a **one-sided mesh collider** — an infinitely thin sheet with
  nothing behind it. Fixed 25 Aug 2026 in layers: `EnemyBrain.HoldAboveTheFloor` and the
  matching block in `PlayerMovement.LateUpdate` lift any body whose feet are >0.6 m under
  `ValleyBuilder.TryFindFloorUnder`; `ValleyBuilder.AddBedrockUnder` puts an invisible slab
  4 m below each mesh floor (`ignoreFromBuild`, so the navmesh baker does not lay a second
  walkable floor down there); controllers get `skinWidth = radius * 0.1` and
  `minMoveDistance = 0`. The quiet lift must be tried **before** any teleport.
- `EnemyBrain` no longer deletes enemies that fall through. That delete was what the user
  saw as "enemies despawning".
- Do **not** detect a fallen body by comparing against ground height. A downward ray near
  the gate misses the terrain and finds the buried portal frame. The valley portal sits at
  **y = -7.85**, ~8 m under the terrain surface (~y = 0.15 there), and `SelfTest` walks the
  player in by standing them on it — so the player legitimately spends a few frames under
  the floor at z ≈ 30 on every run.
- Ground mesh bounds: min `(-32, -0.37, -47)`, max `(32, 9.93, 39)`; one collider piece.
- Colliders created by `ValleyBuilder.BuildTheValley()` are **not queryable in the same
  call**. A raycast right after the rebuild falls through the new geometry. Call
  `Physics.SyncTransforms()` first, or probe in a separate command.

**Rendering / lighting**

- The `OneValley/ProceduralRock` shader barely responds to ambient or point lights — it is
  written for a place with a sun in it. Anything built **indoors** with `MakeRockMaterial`
  renders nearly black however far the lights are pushed. Use `ValleyBuilder.MakeMaterial`
  (plain URP Lit) for interiors.
- URP point-light intensities here are on a scale where **~50 lights a room**. The Vault's
  braziers are 40 over a range of 42. Values like 3 (built-in renderer scale) do nothing.
- The Game view does not render while the editor is unfocused, so
  `ScreenCapture.CaptureScreenshot` silently writes nothing. Render the camera to a
  `RenderTexture` and `EncodeToPNG` instead.

**UI**

- `MainMenu.cs` is where any full-screen UI belongs. It is a plain IMGUI `OnGUI` with a
  `screen` int (0 title, 1 playing, 2 paused, 3 dead). Adding a screen there gets input
  gating, the free mouse, the stopped clock and a hidden HUD for free, because
  `IsShowing()` answers true for anything that is not `ScreenPlaying`, and `PlayerControl`,
  `CursorControl` and `HudDisplay` all already ask it. Do **not** build a parallel UI.
- Boot goes to a title screen — Continue / New Game / Quit. Escape pauses. Saves are
  checkpoints written at each round start, plain JSON.
- Dying shows `ScreenDead` ("YOU ARE DEAD") — Load Last Checkpoint / Back to Main Menu.
  `GameDirector.Update` watches `PlayerIsDead()` itself rather than trusting
  `OnPlayerDied()`. Back to Main Menu deliberately does **not** save, or it would overwrite
  the checkpoint with the moment of death. `RoundDirector.RestartCurrentRound` is currently
  unused — it is where a "Retry round" button would hook in.
- **`CursorControl.cs` is the only file allowed to write `Cursor.lockState` or
  `Cursor.visible`.** It decides once a frame in `LateUpdate` from `MainMenu.IsShowing()`
  and `DialogueBox.ConversationIsOpen()`. Previously the camera, dialogue box and menu each
  set the cursor and contradicted each other — the title screen opened with no pointer, and
  menus rendered visible but unclickable. The symptom looks like broken UI, not a cursor
  problem. Do not reintroduce a cursor write anywhere else.

## Testing

- Automated play-through: `Assets/Scripts/SelfTest.cs`, launched by the
  **One Valley → Run Self Test** menu item. Plays all five rounds, walks into the portal,
  fights the Warden through its three phases, writes `selftest.log` and a
  `selftest_done.txt` marker. Only runs when that menu item armed it, so a harness left in
  the scene is harmless. About 35 seconds.
- For anything that takes seconds: editor `RunCommand` fires once, so write a throwaway
  MonoBehaviour that drives it across frames and writes a log + done-marker, then poll for
  the marker. **Gate its steps on conditions, not fixed times** — a fixed-time version
  killed the player before the title screen had handed over, New Game revived them, and a
  working feature read as broken. Reflection reads private state (`MainMenu.screen`) and
  presses private buttons.

## Asset generation

- Unity's AI asset generation returns **NoSubscription** —
  `Unity_AssetGeneration_GenerateAsset` cannot make models, textures or sprites. Art is
  primitives, free Asset Store packs, or the user's own photo-to-sprite tool.
- **blender-mcp** talks to Blender's addon over plain JSON-over-TCP on port 9876. If the
  MCP tools are not loaded (session resumed rather than restarted), talk to that socket
  straight from Bash: send `{"type": "<command>", "params": {...}}` — `execute_code`,
  `get_scene_info`, `get_viewport_screenshot`, `get_polyhaven_status`.
- **Poly Haven** has no rock *models* but excellent CC0 *textures*. Download from
  `https://api.polyhaven.com/files/<id>` with **curl** — urllib gets HTTP 403, and curl
  needs a real `-A` user agent plus POSIX-style `-o` paths (Windows `C:/...` paths
  silently write nothing).
