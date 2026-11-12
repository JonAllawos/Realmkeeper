# Phase 14 preflight: Phase 13 integration audit

Date: 2026-11-11. This audit was written before any Phase 14 code was changed. Every finding comes from reading the source on `cda9da0`, not only the Phase 13 report.

## 1. Repository state

| Check | Result |
|---|---|
| Branch | `feature/phase-0-1-foundation`, tracking `origin/feature/phase-0-1-foundation` |
| HEAD | `cda9da0` "Phase 13: persistent places, dungeons, interiors and exploration" |
| Remote | `origin/feature/phase-0-1-foundation` = `cda9da0` (fetched); 0 ahead, 0 behind |
| Recent history | `cda9da0` Phase 13 · `1e593ae` Phase 12 · `7b2831b` Phase 11 · `1707805` Phase 10 · `27a5261` Phase 9 |
| Uncommitted | `ProjectSettings/GraphicsSettings.asset` only (+3 −6 lines, the owner's formatting change). Preserved; never staged. |
| Untracked (not ignored) | none |
| Ignored, owner's | `Documentation/Screenshots/dev-*.png` (developer screenshots) and the Unity-generated `Assets/InitTestScene*`. Left alone. |

**Documentation discrepancy.** The Phase 13 report, the Roadmap and `Phase13/Testing.md` say Phase 13 was "committed locally and not pushed". It was pushed at `cda9da0` after the review. Phase 14 corrects this in its normal documentation update.

## 2. Baseline (before any change)

Hardware: Intel Core i9-14900HX, 32 GB, Windows 11 Pro; .NET SDK 8.0.427; Unity 6000.0.47f1.

| Suite | Phase 13 reported | Measured now |
|---|---|---|
| .NET build | 0 warnings | **0 warnings, 0 errors** |
| .NET tests | 629 / 629 | **629 passed, 0 failed** |
| Unity EditMode | 629 / 629 | **629 / 629 passed** |
| Unity PlayMode | 87 passed, 0 failed, 18 skipped | **105 tests: 87 passed, 18 skipped (capture tests), 0 failed** |
| Unity compile warnings | 0 | **0** |
| Phase 13 mutants | 12 / 12 caught | 12 / 12 (the scripts in `mutants/p13.sh` are unchanged since that run) |
| Performance | `PlacePerformanceTests` (Phase 13 report §R) | rerun with the .NET suite; figures within run-to-run noise of the report |

## 3. Persistent location architecture (13.1)

| Area | Code-level state | Verdict |
|---|---|---|
| Identity | `PlaceData.PlaceId` (string, `place:{definition}` for wilderness places, `interior:{settlement}:{building key}` for interiors). Stable across save and load, never regenerated: `PlaceGenesis.Ensure` returns early when any region place exists. | Complete |
| Hierarchy | `PlaceParentKind` Region or Settlement; `SettlementId` and `BuildingKey` link interiors to their building; layouts hold floors, rooms, connections, features and groups. | Complete |
| Save and migration | Schema 15. `MigrationV14ToV15` adds empty lists, and places come with the living-world genesis on load. **Finding:** `PlaceGenesis.Ensure` is all-or-nothing: once any region place exists it adds nothing, so new definitions can never reach an existing world through it. Phase 14 needs a per-definition ensure. | Complete for v14 → v15; needs extending |
| Room discovery | `ExploredRooms` set on arrival; the place map lists explored rooms and known connections only. | Complete |
| Persistent state | Doors (`DoorStates`), features (`FeatureStateData` flags: found, opened, disarmed, sprung, used), groups (`GroupStateData`: Present, Fighting, Defeated, Peaceful, Survived), escorts, place variables (visit-scoped attempt counters), creature instances (`world.Creatures`, keyed `{place}:{group}#{n}`). | Complete |
| Buildings and wilderness | Interiors are planned on first entry (`InteriorGenerator`), wilderness places at genesis. | Complete |
| Transitions | `EnterPlaceCommand` (from the map), `EnterBuildingCommand` (street door), `ExitPlaceCommand` (from the entrance room) with `ExitPose`; one environment loaded at a time. | Complete |
| Maps | Strategic map markers and Places list (`RealmkeeperHud.Places`), the in-place map (N) showing explored rooms only. | Complete |

## 4. Combat and spatial rules (13.2)

| Area | Code-level state | Verdict |
|---|---|---|
| Grid | `GridPos(X, Y, Z)`, 5 ft squares, Chebyshev distance (`DistanceFeet`). In places, 1 cell = 1.5 m = 1 square; place cells map to `GridPos(x, z)`. | Complete |
| Movement and walls | `MoveProblem` refuses non-adjacent steps, `Blocked` squares, hostile occupants, frightened approach and over-budget moves. `StepCost` gives difficult terrain and crawling. | Complete |
| Doors in fights | `PlaceEncounters.Start` turns closed doors into `Blocked` squares when the fight begins. **Finding:** a door cannot be opened or closed during a fight, and its state is not tied back to the place. | Partial |
| Pathfinding | `CombatAi.PathAround` (Phase 13): breadth-first search round walls; `CombatAi.Step` greedy stepping. | Complete |
| Melee targeting | `Unavailable`: reach by Chebyshev distance. **No line check:** a diagonal reach across a wall corner is allowed. | Partial |
| Ranged targeting | Range and long range only (`d > o.LongRange`). **No line of sight:** a bow shot through a solid wall is accepted. | Gap (the Phase 13 limitation, confirmed) |
| Spell targeting | `SpellProblem` / `TargetProblem`: range, target type, count. **No line of effect** for creature spells or area points. | Gap |
| Area effects | `AreaSquares` produces squares by shape on an open grid. **Areas ignore walls:** a Fireball's sphere reaches squares behind a wall. | Gap |
| Line of sight | **None exists** anywhere in Domain, Simulation or Application (searched for sight, line-of-sight and cover). | Not implemented |
| Cover | **None exists.** The SRD cover categories are not modelled; no +2 / +5 to AC or Dexterity saves. | Not implemented |
| AI and sight | `CombatAi` picks ranged targets by distance alone; only one branch (`Behaviors.cs:170`) consults `Unavailable`. Once the engine refuses shots through walls, the AI must consult sight or it would waste turns. | Must change with line of sight |
| Lifecycle | `Begin` → turns → `CheckEnd` → `Finish` (ammunition returned, regenerators finished, conditions end with the fight). Place fights map group state back on the end (Defeated or Survived). | Complete |

## 5. Conditions, damage and recovery (13.3)

| Area | Code-level state | Verdict |
|---|---|---|
| Condition definitions | `ConditionType`: the 15 SRD conditions, with `ConditionInstance` (source, reason, duration in turns, exhaustion level, escape DC, repeat save). Effects are read live by the engine (speed, disadvantage, incapacitation, automatic failures). | Complete **inside fights only** |
| Persistence of conditions | `ConditionInstance` lives on `CombatantData`; `Finish` documents "conditions belong to the encounter and end with it". **The character sheet has no conditions.** | Gap |
| Out-of-fight conditions | Traps and hazards print "You are left poisoned for a while." (`PlaceRules.Meet`, `PlaceCommands` spring) but **apply nothing**: text only. | Gap (the Phase 13 limitation, confirmed) |
| Disease | No disease definitions exist in the rules data or catalogs. | Not supported by existing data |
| Damage in fights | `CombatEngine.ApplyDamage`: immunity, resistance, vulnerability (each once), temporary HP first (on the sheet for the player), massive damage, death saves, concentration checks. | Complete |
| Damage outside fights | **`PlaceRules.Hurt` is a separate pipeline**: dice rolled, halved on a save, subtracted from `CurrentHp` directly. No resistance, immunity, vulnerability or temporary HP; the damage type is printed but not used. It floors at 1 HP (the ADR-0064 fairness rule). | Gap: bypasses the shared rules |
| Saves outside fights | `PlaceRules.Save` uses the sheet's save bonus; ignores exhaustion and conditions. `PlaceRules.Check` ignores poisoned and exhaustion. | Partial |
| Death and stabilization | Fights only. Outside fights the 1 HP floor means no out-of-fight dying state exists. | By design (ADR-0064) |
| Short rest | `WorldSession.Rest`: advance 60 min (`AdvanceTimeCommand`), then `FinishRestCommand` → `CharacterRest.ShortRest` (hit dice + Constitution, short-rest resources, Arcane Recovery). | Complete |
| Long rest | Advance 480 min, then full HP, every resource and hit die, preparation reopens; one per 24 h (`LongRestGapMinutes` 16 h after the last ended), needs 1 HP. | Complete |
| Rest and exhaustion | `LongRest` does **not** reduce exhaustion (exhaustion never exists outside fights). | Gap once exhaustion persists |
| Rest location | **No restriction:** a long rest in the foul-air tomb, beside the spiders' den or inside someone's bedroom is allowed. Only an active encounter blocks rest. | Gap |
| Rest interruption | `AdvanceTimeCommand` failing is the only interruption path; nothing in a place interrupts. | Gap |
| Time and simulation | `AdvanceTimeCommand` runs the scheduler (NPC schedules, daily systems), so rest moves the world consistently. | Complete |
| Temporary effects and save/load | Fight effects (`ActiveEffectData`) and conditions survive save/load with the encounter (Phase 9 round-trip tests). Nothing exists outside fights to survive. | Complete for fights |

## 6. NPCs, inhabitants and trespassing (13.4)

| Area | Code-level state | Verdict |
|---|---|---|
| Resident identity | `NpcData.NpcId`, one record; interiors show residents by reference (`FigureView.NpcId`). | Complete |
| Building assignment | `HomeLocationId`, workplace location; interiors planned from the building's function. | Complete |
| Schedules | `NpcActivityPlan.At(world, npc, date, minute)`: deterministic activity and location by day part and role. | Complete |
| Indoor placement | `PlaceQueries.OccupantRoom`: room by activity (bedroom for sleeping, workshop for working …), `OccupantCell` hashed cell. **Residents are placed, never moved:** when the activity changes the figure is rebuilt in the new room. No route, no doors. | Partial (the Phase 13 limitation, confirmed) |
| Factions and standing | Phase 12 `Reputation.Change` with keyed, once-only changes on settlement reputation, faction standing and NPC regard. | Complete |
| Crime | **None:** no crime records, no theft. Household containers are refused ("theft is not part of Phase 13", `InteriorGenerator`). | Not implemented |
| Witnesses | NPC memories (`NpcMemoryData` with source Witnessed, Experienced or Learned) and knowledge (`PlayerDeed` topic, Known, Believed or Rumor). Daily `NpcSystem.Talk` passes one fact as hearsay between people who share a location. This is a geographically constrained gossip channel that crime can reuse. | Foundation exists |
| Guards and trespass | `PlaceRules.Trespass`: only `GroupBehaviour.Guard` groups react, always with a fight and −10 faction standing (keyed per visit). Household restricted rooms (bedrooms) are marked but nobody reacts. | Partial |
| NPC death | `NpcSystem.RecordDeath` is shared by every cause; fights add `ResidentDeaths`. | Complete |

## 7. Phase 13 unused content (13.5)

| Item | Definition | Why it was never placed | Functional state |
|---|---|---|---|
| Catacomb | `ArchetypeCatalog` "catacomb" (2–3 floors, niches, ossuary, skeletons and zombies) | `PlaceCatalog` has **no `PlaceDefinition`** that uses it, and `PlaceGenesis.Ensure` calls `Make` for the five original definitions only | The generator and validator handle it (250 seeds tested). **Data-only in the world.** |
| Temple | "temple" (shrine, nave, cult) | Same | Data-only |
| Bandit hideout | "bandit_hideout" (bandits, captain, stores) | Same | Data-only |
| Underground works | "underground" (2–3 floors, water) | Same | Data-only |
| Abandoned hamlet | "abandoned_settlement" (large rooms, clues) | Same | Data-only |
| Wake-on creatures | `PlaceGroup.WakesOn`; the hook in `PlaceActions` (Open and Use) starts a fight. **Finding:** the fight is started in the *group's* room even when the player is elsewhere, so a group could wake and fight through walls. No archetype or definition sets `WakesOn`. | Not set by any content | Partial: code path exists, untested with content, has the room bug |
| Bridges | `ConnectionKind.Bridge` enum value only; the generator never emits it. Settlement layouts have `BridgeData` (road decks over waterways) with no state. | Never generated | Data-only (no traversal rules) |
| Fire hazard | `HazardCatalog["fire"]` (Dexterity DC 12, 1d10 fire) | No archetype lists it | Data-only; would also use the bypassing `Hurt` |
| Generated-place keys | Generated locked doors set `PickDc` and `ForceDc`; only the authored fort has a key item (`fort_key`). | No key placement in the generator | Not implemented |

## 8. Conclusions for Phase 14

1. **Line of sight and cover** are absent and form the main combat-integrity gap. They need one shared, deterministic grid function used by attacks, spells, areas, the AI, witnesses and the scene's targeting.
2. **Out-of-fight harm** must go through the same defences as fights. Conditions must live on the character sheet, persist, expire with world time, and carry into and out of fights.
3. **Rest** needs place rules (safe, camp, dangerous, occupied or hostile) and interruption, and exhaustion recovery once exhaustion persists.
4. **Crime** can be built on existing pieces: keyed reputation changes, NPC memory and `PlayerDeed` knowledge, and daily co-located hearsay. It needs records, perception rules and local consequences.
5. **Place genesis** must ensure each definition separately, so new archetypes reach existing saves exactly once.
6. **Wake-on** must start the fight only where the player can perceive it.
7. **Bridges and fire** need generator support and shared rules.
8. **Indoor movement** needs a room-graph route that respects doors. Followers need a room of their own and the same routing.

No Phase 13 claim was found to be false at code level. The "Partial" and "Data-only" items in `Phase13/Limitations.md` are accurate. One finding was not listed there: the wake-on room bug.
