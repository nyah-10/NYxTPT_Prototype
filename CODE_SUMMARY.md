# Code Summary

## Development environment files

- `AGENTS.md`: Defines project-wide Codex behavior, including automatic task-scoped commits and normal pushes after successful file-changing work, while prohibiting automatic force pushes.
- `.gitignore`: Excludes Unity-generated caches, build output, IDE state, and temporary files from Git while retaining source assets, packages, and project settings.
- `.stignore`: Excludes the same machine-local Unity/IDE output plus `.git`, so each PC keeps independent Git metadata and exchanges commits through GitHub.

## Runtime scripts

- `Assets/Scripts/HexGame/TileData.cs`: Defines extensible terrain ScriptableObjects (type, movement cost, LOS/movement blocking, elevation, enter effects, trigger persistence, and destructible-wall HP/fallback terrain).
- `Assets/Scripts/HexGame/RoomTemplate.cs`: Defines hand-authored room assets with a grid size, serialized coordinate-to-`TileData` layout, edge entry points, and filter tags.
- `Assets/Scripts/HexGame/RunMapGenerator.cs`: Selects 3-6 weighted room templates, physically places their painted cells without overlap by matching opposite entry points, normalizes the combined dungeon coordinates, chooses valid first/last-room unit spawns, and transfers the complete layout into Combat.
- `Assets/Scripts/HexGame/MainMenuController.cs`: Builds an FHD-scaled bilingual start menu, creates a fresh run on Start click, validates the Combat scene, and selects the first generated room before scene loading.
- `Assets/Scripts/HexGame/HexGridManager.cs`: Generates only explicitly painted cells from a selected room or physically combined dungeon, leaves erased cells empty, frames the camera once after generation, preserves each terrain's base color through highlight cycles, and exposes generated player/enemy spawn coordinates.
- `Assets/Scripts/HexGame/PlayerController.cs`: Owns the player's hex coordinate and exposes `IsMoving`. Confirmed movement skills animate with time-based `SmoothStep`; ordered skill execution waits for movement arrival before resolving a following action.
- `Assets/Scripts/HexGame/EnemyController.cs`: Draws and publicly exposes a monster ability card each round. Movement acquires live players across the entire connected dungeon without requiring line of sight, follows the grid's weighted shortest path up to the card budget, and avoids occupied cells; attacks still require their configured range and LOS. Target rules resolve from the live board immediately before each action.
- `Assets/Scripts/HexGame/TurnManager.cs`: Registers every player and monster, owns combat reset, three round phases, multi-player card submission, the authoritative initiative queue, per-entry pending/acting/completed/skipped state, and short combat-feedback events. Chosen card initiatives sort ascending; the isolated `CompareQueueEntries()` tie-break resolves players first, then registration order. Each queued unit is revalidated immediately before its action, then waits `turnTransitionDelay` before the next entry begins.
- `Assets/Scripts/HexGame/ActionController.cs`: Owns one main AP and one sub AP per turn. `UseMainSkill()` spends main AP; `UseMoveAction()` spends sub AP.
- `Assets/Scripts/HexGame/UnitStats.cs`: Owns HP, death, status durations, combat role, and damage-source threat totals. `TakeDamage()` applies shield mitigation, records actual damage by source, and triggers `Died` at zero.
- `Assets/Scripts/HexGame/SkillDefinition.cs`: ScriptableObject template for a skill's display name, `initiative`, optional Sprite or Resources icon path, formatted description, slot, targeting data, area, and effect list. Lower initiative values act first.
- `Assets/Scripts/HexGame/SkillLoadout.cs`: Holds configurable main/sub skills, per-round card reservations, execution-order choice state, and one selected leading card. `Plan()` reserves cards; the leading card alone supplies initiative, while the player chooses which reserved card resolves first when their queue entry begins.
- `Assets/Scripts/HexGame/SkillParticleEffects.cs`: Creates short-lived runtime `ParticleSystem` effects for the four example skills. Sword Strike emits layered slash arcs, Arcane Bolt sends plasma motes from source to target, First Aid lifts green pulse particles around the caster, and Leap bursts impact shards at the destination. Each system destroys its generated material when finished.
- `Assets/Scripts/HexGame/SkillActionUI.cs`: Builds the FHD-scaled card presentation and shows a non-interactive movement/attack/self range preview as soon as a card is selected. During the player's execution entry it re-enables only reserved cards so the player can choose their first action against the current board state; target input and no-target feedback resolve afterwards.
- `Assets/Scripts/HexGame/SkillHandLayout.cs`: Reflows any hand size in a straight horizontal row using adaptive overlap, hover neighbor separation, and live drag reordering. Cards remain above the HUD bottom edge and do not use a fan curve or resting rotation.
- `Assets/Scripts/HexGame/SkillCardView.cs`: Separates card input from visual motion, keeps nested card canvases above the parent HUD, and spring-animates layout targets, hover lift, selection, drag following, inertia tilt, focus sorting, and disabled presentation with unscaled time. Reserved cards remain clickable for leading-card designation.
- `Assets/Scripts/HexGame/InitiativeOrderUI.cs`: Renders player/monster queue badges from `TurnManager` snapshots, marks the active unit with `▶`, dims completed entries, labels dead/disabled skips, shows monster ability cards before reveal, and displays short combat messages.
- `Assets/Resources/SkillIcons/*.png`: Four generated square example icons for 검격, 마력탄, 응급 처치, and 도약, loaded through each skill's `iconResourcePath`.
- `Assets/Resources/Skills/Bonus/*.asset`: Runtime-loaded bonus hand cards. `ShieldBash` is a main action that deals 1 damage and pushes 1 tile; `GuardStance` is a sub action that grants 3 shield for one turn. `SkillActionUI` appends them after their matching configured slot groups without changing the scene.
- `Assets/Resources/UI/skill_hud_panel.png`: Original ornamental HUD panel retained as an available UI resource; the compact skill bar uses a code-rendered dark panel to avoid aspect distortion.
- `Assets/Resources/SkillEffects/*.png`: Four generated transparent particle sprites used by `SkillParticleEffects` for Sword Strike, Arcane Bolt, First Aid, and Leap.
- `Assets/Scripts/HexGame/UnitHealthBar.cs`: Displays a larger world-space HP bar and `CurrentHP / MaxHP` text above each unit. The fill changes from green to yellow to red as health falls.
- `Assets/Editor/SkillExampleAssets.cs`: Editor utility that creates four editable example skill assets in `Assets/Skills/Examples`: Sword Strike, Arcane Bolt, First Aid, and Leap.
- `Assets/Editor/RoomTemplateEditorWindow.cs`: Provides a room grid painter with tile brushes and edge-entry editing, plus a menu command that creates a terrain palette and six editable sample room assets.
- `Assets/Editor/BakedGridCleanup.cs`: Permanently removes obsolete pre-generated tile children from the Combat scene when it opens or scripts reload; the same cleanup is available from the Hex Roguelike Tools menu.

## Skill flow

1. Run **Tools > Hex Roguelike > Create Example Skills** to create Sword Strike, Arcane Bolt, First Aid, and Leap assets.
2. Assign two `Main` and two `Sub` skills to `SkillLoadout`. The scene setup supplies all four examples.
3. Select either a main or sub skill to preview its reachable or targetable tiles, then press Confirm to reserve only the card. The preview does not lock a target during planning.
4. Select the other action. Reservation order does not determine resolution order.
5. Click one reserved card again to mark it as the leading card. Confirm reveals cards and builds the queue using that card's initiative; lower values act first and a tied player acts before a monster.
6. When the player's initiative entry begins, choose either reserved card as the first action. Movement then asks for a currently free in-range tile; attacks recalculate candidates against the latest board state, resolve automatically for one target, wait for a target choice when several are valid, and skip when none are valid. The remaining reserved card resolves afterwards.
7. Damage, healing, shield, stun, immobilize, movement/jump, push, and pull are resolved by `SkillLoadout`.
8. A successful immediate or planned commit calls `SkillParticleEffects.Play()` once after gameplay effects resolve.

## Terrain flow

1. Create terrain assets with **Create > Hex Roguelike > Tile Data**, then assign a default and coordinate overrides on `HexGridManager.terrainPlacements` in the Inspector.
2. `HexGridManager` uses weighted pathfinding for movement budgets, rejects movement-blocking tiles, traces axial LOS, and grants one range when the attacker is above the target.
3. Player and enemy movement walks the selected path and invokes `HexTile.ApplyEnterEffect()` on every entered tile; one-shot traps retain consumed state on that runtime tile.
4. Skills with `canDestroyTerrain` apply their Damage effects to destructible walls. At zero HP the wall uses `destroyedTile`, or behaves as a Normal non-blocking tile when no fallback is assigned.

## Room map flow

1. Build index 0 opens the `Main` scene. `MainMenuController` creates the 1920x1080-scaled start screen at runtime.
2. Start click asks `RunMapGenerator` to choose 3-6 rooms, matches opposite entry points, rejects overlapping placements, and produces one physical dungeon layout containing all selected rooms.
3. `HexGridManager.Awake()` consumes the combined layout and generates only explicitly painted cells; erased template cells remain actual void. The camera frames the complete dungeon and units use valid cells in its first and last rooms.
4. Later map UI can call `SelectNode(nodeId)` for subsequent nodes; Turn and combat managers remain independent.

Push, pull, movement, jump, shield, stun, and immobilize are represented in skill data now; their board/status resolution is intentionally pending until their corresponding gameplay systems exist.
