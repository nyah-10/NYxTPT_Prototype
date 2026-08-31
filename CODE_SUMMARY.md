# Code Summary

## Development environment files

- `AGENTS.md`: Defines project-wide Codex behavior, including automatic task-scoped commits and normal pushes after successful file-changing work, while prohibiting automatic force pushes.
- `.gitignore`: Excludes Unity-generated caches, build output, IDE state, and temporary files from Git while retaining source assets, packages, and project settings.
- `.stignore`: Excludes the same machine-local Unity/IDE output plus `.git`, so each PC keeps independent Git metadata and exchanges commits through GitHub.

## Runtime scripts

- `Assets/Scripts/HexGame/HexGridManager.cs`: Generates and stores the axial-coordinate hex grid. It preserves the `HexTile` prefab's stone texture at normal state; each `HexTile` creates a translucent range-highlight overlay. `SetSelectedHighlight()` layers a brighter target selection over the current range without losing the other highlighted tiles.
- `Assets/Scripts/HexGame/PlayerController.cs`: Owns the player's hex coordinate and exposes `IsMoving`. Confirmed movement skills animate with time-based `SmoothStep`; ordered skill execution waits for movement arrival before resolving a following action.
- `Assets/Scripts/HexGame/EnemyController.cs`: Runs the easy enemy's turn. It can move up to two hexes when outside attack range, then selects either a 3-range/1-damage ranged main skill or a 1-range/4-damage close main skill.
- `Assets/Scripts/HexGame/TurnManager.cs`: Runs three round phases: simultaneous card selection, initiative reveal/sort, then execution. Chosen card initiatives sort ascending; ties resolve player first, then instance order. Each queued unit is revalidated immediately before its action.
- `Assets/Scripts/HexGame/ActionController.cs`: Owns one main AP and one sub AP per turn. `UseMainSkill()` spends main AP; `UseMoveAction()` spends sub AP.
- `Assets/Scripts/HexGame/UnitStats.cs`: Owns `MaxHP`, `CurrentHP`, and `IsDead`. `TakeDamage()` reduces HP and triggers the `Died` event at zero; `RestoreHealth()` restores up to `MaxHP`.
- `Assets/Scripts/HexGame/SkillDefinition.cs`: ScriptableObject template for a skill's display name, `initiative`, optional Sprite or Resources icon path, formatted description, slot, targeting data, area, and effect list. Lower initiative values act first.
- `Assets/Scripts/HexGame/SkillLoadout.cs`: Holds configurable main/sub skills and an ordered per-turn action plan. `Plan()` reserves one action of each slot without changing game state; `GetPlanningSource()` projects earlier movement, and `ExecutePlan()` spends AP and resolves both actions in their selected order.
- `Assets/Scripts/HexGame/SkillParticleEffects.cs`: Creates short-lived runtime `ParticleSystem` effects for the four example skills. Sword Strike emits layered slash arcs, Arcane Bolt sends plasma motes from source to target, First Aid lifts green pulse particles around the caster, and Leap bursts impact shards at the destination. Each system destroys its generated material when finished.
- `Assets/Scripts/HexGame/SkillActionUI.cs`: Builds the FHD-scaled card hand and handles ordered action planning. Full-size cards expose their slot, initiative, art, description, range, and primary effect; reserved movement creates a translucent player ghost and subsequent targeting uses that projected coordinate.
- `Assets/Scripts/HexGame/SkillCardView.cs`: Animates card hover/selection lift, scale, focus ordering, accent outline, and disabled presentation with unscaled time.
- `Assets/Scripts/HexGame/InitiativeOrderUI.cs`: Shows the revealed round order and highlights the currently executing unit in a top-screen bar.
- `Assets/Resources/SkillIcons/*.png`: Four generated square example icons for 검격, 마력탄, 응급 처치, and 도약, loaded through each skill's `iconResourcePath`.
- `Assets/Resources/UI/skill_hud_panel.png`: Original ornamental HUD panel retained as an available UI resource; the compact skill bar uses a code-rendered dark panel to avoid aspect distortion.
- `Assets/Resources/SkillEffects/*.png`: Four generated transparent particle sprites used by `SkillParticleEffects` for Sword Strike, Arcane Bolt, First Aid, and Leap.
- `Assets/Scripts/HexGame/UnitHealthBar.cs`: Displays a larger world-space HP bar and `CurrentHP / MaxHP` text above each unit. The fill changes from green to yellow to red as health falls.
- `Assets/Editor/SkillExampleAssets.cs`: Editor utility that creates four editable example skill assets in `Assets/Skills/Examples`: Sword Strike, Arcane Bolt, First Aid, and Leap.

## Skill flow

1. Run **Tools > Hex Roguelike > Create Example Skills** to create Sword Strike, Arcane Bolt, First Aid, and Leap assets.
2. Assign two `Main` and two `Sub` skills to `SkillLoadout`. The scene setup supplies all four examples.
3. Select either a main or sub skill, choose its highlighted target, and press Confirm to reserve it. Reserving movement creates a translucent ghost at the destination.
4. Select the other action. Its range origin reflects movement only when that movement was reserved earlier, so main-sub and sub-main plans resolve differently.
5. Once actions are reserved, press Confirm to reveal monster cards and build the round queue. The queue executes lower initiative cards first; a tied player acts before a monster.
6. Damage, healing, shield, stun, immobilize, movement/jump, push, and pull are resolved by `SkillLoadout`.
7. A successful immediate or planned commit calls `SkillParticleEffects.Play()` once after gameplay effects resolve.

Push, pull, movement, jump, shield, stun, and immobilize are represented in skill data now; their board/status resolution is intentionally pending until their corresponding gameplay systems exist.
