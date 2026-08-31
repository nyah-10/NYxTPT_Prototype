# Code Summary

## Development environment files

- `AGENTS.md`: Defines project-wide Codex behavior, including automatic task-scoped commits and normal pushes after successful file-changing work, while prohibiting automatic force pushes.
- `.gitignore`: Excludes Unity-generated caches, build output, IDE state, and temporary files from Git while retaining source assets, packages, and project settings.
- `.stignore`: Excludes the same machine-local Unity/IDE output plus `.git`, so each PC keeps independent Git metadata and exchanges commits through GitHub.

## Runtime scripts

- `Assets/Scripts/HexGame/HexGridManager.cs`: Generates and stores the axial-coordinate hex grid. It preserves the `HexTile` prefab's stone texture at normal state; each `HexTile` creates a translucent range-highlight overlay. `SetSelectedHighlight()` layers a brighter target selection over the current range without losing the other highlighted tiles.
- `Assets/Scripts/HexGame/PlayerController.cs`: Owns the player's hex coordinate and exposes `IsMoving`. Confirmed movement skills animate with time-based `SmoothStep`; ordered skill execution waits for movement arrival before resolving a following action.
- `Assets/Scripts/HexGame/EnemyController.cs`: Draws and publicly exposes a monster ability card each round. Each card owns initiative, move/attack values, range, execution order, and a target rule; nearest, lowest-HP, accumulated threat, and fixed role priority are resolved from the live board immediately before movement or attack, with equal scores falling back to nearest. A live attack with no valid target reports a short combat message through `TurnManager`.
- `Assets/Scripts/HexGame/TurnManager.cs`: Registers every player and monster, owns combat reset, three round phases, multi-player card submission, the authoritative initiative queue, per-entry pending/acting/completed/skipped state, and short combat-feedback events. Chosen card initiatives sort ascending; the isolated `CompareQueueEntries()` tie-break resolves players first, then registration order. Each queued unit is revalidated immediately before its action, then waits `turnTransitionDelay` before the next entry begins.
- `Assets/Scripts/HexGame/ActionController.cs`: Owns one main AP and one sub AP per turn. `UseMainSkill()` spends main AP; `UseMoveAction()` spends sub AP.
- `Assets/Scripts/HexGame/UnitStats.cs`: Owns HP, death, status durations, combat role, and damage-source threat totals. `TakeDamage()` applies shield mitigation, records actual damage by source, and triggers `Died` at zero.
- `Assets/Scripts/HexGame/SkillDefinition.cs`: ScriptableObject template for a skill's display name, `initiative`, optional Sprite or Resources icon path, formatted description, slot, targeting data, area, and effect list. Lower initiative values act first.
- `Assets/Scripts/HexGame/SkillLoadout.cs`: Holds configurable main/sub skills and an ordered per-round action plan. `Plan()` reserves only cards and their order; `ExecutePlan()` asks for movement destinations and attack targets from the live board when each action executes, skipping unavailable actions with HUD feedback.
- `Assets/Scripts/HexGame/SkillParticleEffects.cs`: Creates short-lived runtime `ParticleSystem` effects for the four example skills. Sword Strike emits layered slash arcs, Arcane Bolt sends plasma motes from source to target, First Aid lifts green pulse particles around the caster, and Leap bursts impact shards at the destination. Each system destroys its generated material when finished.
- `Assets/Scripts/HexGame/SkillActionUI.cs`: Builds the existing FHD-scaled card presentation, registers cards with the dynamic hand, and keeps the hand background non-interactive so it cannot intercept card input. Board clicks are ignored during card selection; movement destinations, attack targets, and no-target feedback appear only during execution.
- `Assets/Scripts/HexGame/SkillHandLayout.cs`: Reflows any hand size in a straight horizontal row using adaptive overlap, hover neighbor separation, and live drag reordering. Cards remain above the HUD bottom edge and do not use a fan curve or resting rotation.
- `Assets/Scripts/HexGame/SkillCardView.cs`: Separates card input from visual motion, keeps nested card canvases above the parent HUD, and spring-animates layout targets, hover lift, selection, drag following, inertia tilt, focus sorting, and disabled presentation with unscaled time.
- `Assets/Scripts/HexGame/InitiativeOrderUI.cs`: Renders player/monster queue badges from `TurnManager` snapshots, marks the active unit with `▶`, dims completed entries, labels dead/disabled skips, shows monster ability cards before reveal, and displays short combat messages.
- `Assets/Resources/SkillIcons/*.png`: Four generated square example icons for 검격, 마력탄, 응급 처치, and 도약, loaded through each skill's `iconResourcePath`.
- `Assets/Resources/Skills/Bonus/*.asset`: Runtime-loaded bonus hand cards. `ShieldBash` is a main action that deals 1 damage and pushes 1 tile; `GuardStance` is a sub action that grants 3 shield for one turn. `SkillActionUI` appends them after their matching configured slot groups without changing the scene.
- `Assets/Resources/UI/skill_hud_panel.png`: Original ornamental HUD panel retained as an available UI resource; the compact skill bar uses a code-rendered dark panel to avoid aspect distortion.
- `Assets/Resources/SkillEffects/*.png`: Four generated transparent particle sprites used by `SkillParticleEffects` for Sword Strike, Arcane Bolt, First Aid, and Leap.
- `Assets/Scripts/HexGame/UnitHealthBar.cs`: Displays a larger world-space HP bar and `CurrentHP / MaxHP` text above each unit. The fill changes from green to yellow to red as health falls.
- `Assets/Editor/SkillExampleAssets.cs`: Editor utility that creates four editable example skill assets in `Assets/Skills/Examples`: Sword Strike, Arcane Bolt, First Aid, and Leap.

## Skill flow

1. Run **Tools > Hex Roguelike > Create Example Skills** to create Sword Strike, Arcane Bolt, First Aid, and Leap assets.
2. Assign two `Main` and two `Sub` skills to `SkillLoadout`. The scene setup supplies all four examples.
3. Select either a main or sub skill and press Confirm to reserve only the card. Board tiles and units cannot be selected during planning.
4. Select the other action. Selection order determines whether main or sub resolves first.
5. Once actions are reserved, press Confirm to reveal monster cards and build the round queue. The queue executes lower initiative cards first; a tied player acts before a monster.
6. When movement executes, the player chooses a currently free in-range tile. When an attack executes, candidates are recalculated; one is automatic, several wait for selection, and zero produces `타겟 없음` feedback.
7. Damage, healing, shield, stun, immobilize, movement/jump, push, and pull are resolved by `SkillLoadout`.
8. A successful immediate or planned commit calls `SkillParticleEffects.Play()` once after gameplay effects resolve.

Push, pull, movement, jump, shield, stun, and immobilize are represented in skill data now; their board/status resolution is intentionally pending until their corresponding gameplay systems exist.
