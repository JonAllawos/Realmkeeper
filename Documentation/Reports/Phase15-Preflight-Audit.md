# Phase 15 preflight: what exists for companions and parties

Date: 2026-11-12. Base: `4324084` (pushed), plus the uncommitted Phase 15 art addendum (`Reports/Phase15-Art-Animation-Report.md`). Every finding comes from the current source.

## Repository state

| Item | State |
|---|---|
| Branch | `feature/phase-0-1-foundation` = `origin` at `4324084` |
| Owner's local change | `ProjectSettings/GraphicsSettings.asset` (unstaged; preserved) |
| Ignored owner files | `Documentation/Screenshots/dev-*.png`, `Assets/InitTestScene*`, and `Art/ThirdParty/QuaterniusBestiary/` (QAL, local only) |
| Uncommitted | The art addendum (models, shared animation controllers, the exploration speed change). It is committed with Phase 15, because the brief folds it in. |
| Save schema | **v16** (`WorldData.CurrentSchemaVersion`); migrations v1→v16 in `SaveMigrations.cs` |

## Reuse: what already does the job

| Need | Existing source | Verdict |
|---|---|---|
| Persistent people | `NpcData` (identity, home, faction, role, tier, personality, schedule, goals, memories, knowledge, `PlayerRegard`, optional `Character`) | **Reuse.** A companion *is* an `NpcData`. |
| Character sheets for people | `NpcCharacterData.Sheet` (`CharacterSheetData`); `NpcCharacterGenesis.Make/BuildSheet/AdvanceTo` (deterministic, rules-valid); `CharacterRules` evaluates player and NPC sheets alike | **Reuse.** Promote a recruit without a sheet through `NpcCharacterGenesis`. |
| Taking a person out of their schedule | `PlacePresence.IsAway` (`NpcActivity` asks it); `EscortData.OverridesSchedule` | **Extend:** party members are "away" while with the player; the schedule itself is untouched. |
| Following in places | `PlaceFollowers.Enter/Follow/Leave/Waiting` and `PlaceRoutes.Find(..., Mover.Follower)` for escorts: real passages, wait at locked doors | **Extend** to party members (same routes and rules). |
| Player's regard | `PlayerRegardData`: Familiarity, Affinity (liking), Trust, Respect, Fear (+ history) | **Reuse** for the player–companion relationship. **Add** only loyalty and a once-only approval log. |
| Person–person relationships | `NpcRelationData`: Affinity, Trust, Respect, Fear, Rivalry, causes | **Reuse** for companion-to-companion affinity. |
| Memories and knowledge | `NpcMind.Remember`, `NpcMind.Learn`; crimes recorded as knowledge (Phase 14) | **Reuse** as the evidence for approval: thefts, killings and decisions the companion saw. |
| Items | `ItemStackData` instances with `ItemStackId`, provenance and state. The player has `Inventory` plus `Equipment` (slot → stack); containers hold stacks. `NpcGearData` holds **item ID strings** (not instances). | **Extend:** companions gain `Inventory` and `Equipment` of real instances (the player's shape). Gear strings stay for people who are not companions. One owner per stack is enforced by the validator. |
| Loadout for the rules | `CharacterRules.LoadoutOf(PlayerCharacterData)` | **Extend** with an overload for a companion's equipment, so the same evaluator computes AC and attacks. |
| Combat | `CombatantKind.Npc` participants with sheets (the guard already joins practice fights). Sheets are used live (`SheetOf(c)`), so HP, slots and conditions persist; `ReturnLasting` returns conditions. | **Reuse:** party members join as `Npc` participants on the player's side. |
| Combat AI | `CombatAi.Next` (deterministic, the engine validates everything), `CombatAi.Spells`, creature behaviours, `CombatAiProfile` | **Extend:** a companion layer (heal, stabilise, Rage, Second Wind, stay at range) in front of `Next`, with recorded reasons. No second AI. |
| Rest | `WorldSession.Rest` → `RestRules.Check` → `FinishRestCommand` for the player's sheet | **Extend:** the same rest applies each companion's sheet once (keyed). |
| Quests | Bound templates (`QuestCatalog`, roles, objectives, `QuestRuntime`), dialogue consequences (`AcceptQuest`, `Regard`, `Decision`, …), the Chronicle | **Reuse:** one personal quest template bound to a companion; recruitment and dismissal as new dialogue consequences. |
| Exploration movement | The real-time `PlayerCharacter` controller; tactical movement only inside `CombatEngine` | **Already separate.** The art addendum removed the last exploration slowdown (`SpeedMultiplier = 1`). Combat movement is unchanged. |
| Bodies and animation | `CombatantBodies`, `CombatantAnimator`, humanoid bodies with `PersonCombatant` (art addendum) | **Reuse** for companions in fights and in exploration. |

## Gaps (must be built)

1. **A party model:** members, order, leader, active/reserve, follow/wait, formation slot, follow distance, temporary terms; capacity as a game rule.
2. **Recruitment rules:** refusal reasons from personality, regard, faction, responsibilities, danger and quest state; recruit and dismiss commands; dialogue consequences.
3. **Companion item instances** (inventory and equipment), transfers player ↔ companion, equip and unequip through the same rules.
4. **Companion AI layer** and its diagnostics.
5. **Party rest.**
6. **Loyalty and approval:** event-driven, once per cause, explained; departure and return.
7. **Personal quest scenario** (one original template).
8. **Exploration following in settlements:** real-time bodies that follow the player. Today only places have room-based following.
9. **Party UI:** a HUD strip and a Hub section.
10. **Save v17** for the party, companion items and loyalty.

## Conflicts and hazards to avoid

- **Escorts vs companions.** Escorts (captives being taken home) remain a separate concept. A freed captive is not automatically a party member. The validator must forbid a person being both an escort and a party member.
- **Gear strings vs instances.** A companion's `NpcGearData` must not be shown as owned items. On recruitment their gear strings become real instances once (keyed by person, so there is no second copy on a re-recruit), and the strings are cleared while they hold instances.
- **Duplicates:** party members must never be instantiated twice in exploration (as a resident and as a follower). The resident layer must skip anyone who is away.
- **Resources:** one long rest restores each sheet once (keyed); XP from a fight is awarded once (the existing keyed awards).

## Documentation conflict noted

The Game Design Bible describes a party RPG. The Product Foundation describes a narrower simulation-first product. Phase 15 implements parties as the roadmap now directs; the Foundation's statement is recorded as superseded *for this feature only* in the Phase 15 report, without rewriting the product direction.
