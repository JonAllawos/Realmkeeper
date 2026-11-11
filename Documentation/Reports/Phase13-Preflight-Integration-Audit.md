# Phase 13 preflight: integration audit of Phases 10–12

Date: 2026-11-11. Written before any Phase 13 implementation. Everything below was checked against the code, data, tests and runs, not taken from earlier reports. Where a finding repeats an earlier report, it says what was verified.

## 1. Git state at the start

| Item | State |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| Upstream | `origin/feature/phase-0-1-foundation`, level (0 ahead, 0 behind) |
| HEAD | `1e593ae` Phase 12: quests, dialogue, reputation and world interaction |
| Phase 10 | `1707805`, an ancestor of HEAD, on `origin` |
| Phase 11 | `7b2831b`, an ancestor of HEAD, on `origin` |
| Phase 12 | `1e593ae` (HEAD), on `origin` (pushed on the owner's instruction after review) |
| Working tree | One owner change: `ProjectSettings/GraphicsSettings.asset` (3 insertions, 6 deletions). Preserved: never staged, never discarded. No stashes, no untracked files. |

All three phases are present in the branch.

## 2. Baseline tests (before implementation)

Hardware: Intel Core i9-14900HX, 32 GB RAM, Windows 11 Pro. .NET SDK 8.0.427. Unity 6000.0.47f1 (Editor, batch mode).

| Suite | Result | Warnings |
|---|---|---|
| .NET harness (Release, `--no-incremental` build) | **603 passed, 0 failed, 0 skipped** | 0 warnings, 0 errors |
| Unity EditMode | **603 passed, 0 failed, 0 skipped** | 1 (CS0067, see section 6). This baseline reused compiled scripts, so the warning was not re-emitted; it was recorded at the last full compile (Phase 12 final run). |
| Unity PlayMode | **100: 83 passed, 0 failed, 17 skipped** | — |

The skipped PlayMode tests are the screenshot capture sets (Phases 5–12), which run only when `RK_CAPTURE_DIR` is set. There were no pre-existing failures.

## 3. Phase 10: bestiary, NPC systems, reactions, settlement generation

| Area | Finding | Verified by |
|---|---|---|
| Persistent NPC identities | Complete. `NpcData` with a stable `NpcId`; one body per identity in a loaded scene (`ExplorationController.LoadEnvironment` skips duplicates). | Code |
| Character sheets and tiers | Complete. `NpcCharacterData.Sheet` and `NpcTier`; archetypes give class, level range, gear and combat profile. | Code |
| NPC equipment, inventory, spellcasting | Complete as data and rules: `NpcGearData` (body, shield, main and off hand, carried items), prepared spells per archetype. | Code |
| Schedules and routines | Complete. `NpcScheduleData` gives a place per weekday; `NpcActivity` gives place and activity by minute (sleeping, eating, working, trading, guarding, socialising, worship). | Code |
| Death and continuity | Complete. `NpcSystem.RecordDeath`; the dead keep their records. | Code and Phase 12 tests |
| Memory and knowledge | Complete (Phase 4, extended in Phase 12). | Code and tests |
| Bestiary | 43 creature definitions (30 SRD bestiary, 13 earlier), each with habitat, behaviour profile and loot. | Code |
| Creature populations | Complete. `CreaturePopulationData` by region, habitat and centre; encounters draw individuals with lasting records (`CreatureInstanceData`). | Code |
| Encounter generation | Complete for outings from a settlement only (`WorldEncounters.Preview` refuses unless the player is in a settlement). | Code |
| Encounter consequences, XP and loot idempotency | Complete. The origin's `Resolved` flag and keyed experience; loot keyed per encounter. | Code and tests |
| Combat space | **Gap relevant to Phase 13.** The combat grid has no walls or obstacles: only difficult terrain. Movement and AI paths ignore geometry. Dungeon rooms and corridors cannot constrain a fight without extending the engine. | Code (`CombatEngine.MoveProblem`, `CombatAi.PathToward`) |
| Settlement diversity | **Partial.** Each settlement gets its own terrain (hills, slope, river from the region map), districts and buildings, and one of 5 layout archetypes in practice: PlannedTown, Hillside, RiverCrossing, OrganicVillage, Farmstead. Two declared archetypes, **Roadside and Fortified, are never chosen** (they appear only in description text). There is no test that checks archetype variety across seeds; one test checks that house styles vary. Geography differs genuinely (terrain and water come from the region), but the plans fall into a small number of patterns. | Code and tests |
| Strategic and local consistency | Complete. The local layout derives from the settlement's region cell, rivers and roads. | Code |

**Relevant to Phase 13:** the missing combat obstacles (fights in rooms), the NPC whereabouts model (interior occupancy can use it directly), and creature definitions (dungeon inhabitants). Settlement archetype variety is outside Phase 13's scope and is deferred (section 7).

## 4. Phase 11: inventory, loot, equipment, crafting

| Area | Finding |
|---|---|
| Items, instances, ownership, transactions | Complete. Each stack has one holder; transfers are validated and atomic. |
| Containers | Complete as records (`ContainerData`: personal chest, chest, settlement store, cache, loot, camp). In the scene only the personal chest and the cache flagstone exist (`ContainerInteractable`). |
| Outdoor loot | **Confirmed gap.** Loot from a fight is a container with no place in the world. It is reached through the Storage section of the Player Hub, not as a physical pile. |
| Equipment rules, condition, magic items, attunement | Complete (verified by Phase 11 tests in the current suite). |
| Crafting, gathering, repair, merchants | Complete for the 11 recipes that exist: iron bar, iron dagger, arrows, cured leather, leather jerkin, sling, quarterstaff, traveller's cloak, potion of healing, hunter's stew, smoked fish. **No metal armour recipes and no enchanting.** |
| Inventory UI | **Confirmed gaps.** "Split" halves a stack; there is no quantity choice and no drag and drop. |
| Equipment visuals | Weapons and shields are drawn on the character (`ItemModels`, `PlayerBodyBuilder`). |

**Relevant to Phase 13:** containers need a place inside locations (chests and loot piles in rooms). Recipes and inventory UI are not Phase 13 work and stay deferred.

## 5. Phase 12: quests, dialogue, reputation

All features listed in the brief exist and are tested. Inspection of the ten reported gaps:

| Gap | Actual state | Affects Phase 13 |
|---|---|---|
| 1. Escort | `ObjectiveKind.Escort` and its matching exist; **nothing emits `Escorted`**. | Yes: Phase 13 must emit it. |
| 2. Rescue | Same: matching exists, **no emitter**. | Yes |
| 3. Protect | Matching exists; encounters already support a protected NPC on the player's side (`EncounterObjective.Protect`), but no quest event is emitted. | Yes |
| 4. Crime and theft | No player theft or assault actions. | Indirectly: trespass in restricted rooms needs a hook. Not a full crime system. |
| 5. Object examination | One inspect command, for two keys (ledger, notice board). | Yes: Phase 13 generalises it. |
| 6. Wolf hunts | Raised only when a wolf population is within reach of a town. | No |
| 7. Map markers | None for places other than settlements. | Yes |
| 8. Content-settings screen | Rules exist; no screen. | No |
| 9. Repeatable quests | The state machine allows it; no repeatable content. | No |
| 10. Reward-claim guard | `QuestRuntime.Complete` refuses a quest already rewarded, but nothing public can reach that state, so the Phase 12 mutant survived. | Fix: direct test (section 6). |

## 6. Outstanding issues and fixes before implementation

| Issue | Action |
|---|---|
| Reward-claim guard untested | `QuestRuntime.Complete` becomes `internal` (the test assembly already sees internals). A new test calls it twice on the same quest and checks one payment. The Phase 12 mutant m1 is rerun and must be caught. |
| Compiler warning | **Still present in Unity:** CS0067, `PlayerHubWindow.CombineRequested` is declared and subscribed (`ExplorationController.Living.cs:105`) but never raised. The .NET harness does not compile presentation, so it shows 0 warnings. Fix: remove the dead event and its subscription. The `CombineStacksCommand` stays (it is tested in `CommerceTests`); no button ever raised the event, so nothing the player could do changes. |
| Documentation saying Phase 12 is "not pushed" | Corrected: the Roadmap rows for Phases 9 and 12 and the Phase 12 report's header and Git section now say they were pushed. These are documentation changes in the Phase 13 commit; no extra push. |

## 7. Architecture conflicts and integration risks

1. **"Location" is already taken.** `LocationData` (Phase 4) is a place inside a settlement that NPC schedules point at (a hall, house-3, the plaza). Phase 13's explorable locations will be called **places** in code (`PlaceData`, `PlaceId`) to avoid two meanings of one word. A settlement interior place refers to its `LocationId`, so schedules and interiors agree.
2. **The player is either on the map or in a settlement.** `CharacterLocationKind` has two values. A third, *in a place*, is needed, with the place, floor and room. Every reader of `IsInSettlement` must stay correct, including encounters, shops, conversations and saving.
3. **One loaded environment at a time.** `LocalEnvironmentStreamer` loads one settlement layout and refuses a second. Places load through it, so a settlement and a place are never loaded together (no duplicate NPC bodies).
4. **Existing interiors are open buildings in the settlement scene** (the hall and the shops). Enterable interiors become separate places reached by a door; the open hall stays as it is.
5. **Combat has no geometry.** Blocked squares must be added to the engine's movement and AI paths (an extension, not a second engine).
6. **Encounters start only from settlements.** Place encounters need their own start path, with fixed inhabitants, using the same `CombatCommandHandlers.StartGenerated` and `WorldEncounters.Resolve`.
7. **Save size.** Generated layouts will be stored, not regenerated, so a generator update cannot change an existing place. Each layout must stay small (rooms, connections and features, not geometry).
8. **Risk: scale.** Phase 13 touches persistence, the player's location, combat, quests, the map and the scene. Each step is tested in .NET before Unity work starts.

## 8. Deferred (not Phase 13)

- Settlement archetypes Roadside and Fortified (never chosen), and a diversity test.
- Metal armour and enchanting recipes; quantity choice and drag and drop in the inventory.
- A crime system, repeatable quests and a content-settings screen.
- Wolf hunts beyond the nearest populations.

## 9. Fixes applied before implementation (results)

| Fix | Result |
|---|---|
| Reward-claim guard | `QuestRuntime.Complete` is now `internal`. New test `ARewardAlreadyClaimed_IsNeverPaidAgain_EvenIfCompletionRunsTwice` passes. **Mutant m1 rerun: caught** (604 tests, 1 failed under the mutant: the new test). |
| CS0067 | The unused `PlayerHubWindow.CombineRequested` event and its subscription were removed. Confirmed in the Phase 13 Unity compile (see the Phase 13 report). |
| Push wording | Roadmap and Phase 12 report corrected. |

The audit is complete; implementation starts after this point.
