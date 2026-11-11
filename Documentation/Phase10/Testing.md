# Phase 10: testing

Only runs that actually took place are recorded. Baseline (end of Phase 9, `27a5261`): .NET 520 passed;
Unity EditMode 520 passed; PlayMode 70 passed and 10 skipped (capture sets without `RK_CAPTURE_DIR`).

## Results

| Suite | Before | After | Failed | Skipped | Notes |
|---|---|---|---|---|---|
| .NET (`dotnet test Tools/DotNet/Realmkeeper.Verification.sln -c Release`) | 520 | **552** | 0 | 0 | Build: 0 warnings (warnings are errors). |
| Unity EditMode (batch mode, 6000.0.47f1) | 520 | **552** | 0 | 0 | No compiler warnings (the CS0105 duplicate `using` was removed). |
| Unity PlayMode (batch mode) | 70 + 10 skipped | **74 passed** of 88 | 0 | 14 | The skips are the capture sets (Phases 4–10), which run only with `RK_CAPTURE_DIR` and a graphics device, by design. 0 inconclusive. |
| Phase 10 captures (`RK_CAPTURE_DIR` set, not batch mode) | — | 4 capture tests | 0 | 0 | 15 screenshots and a performance log. |

## New tests

| Fixture | Tests | What they prove |
|---|---|---|
| `Living/SettlementLayoutTests` | 12 | <ul><li>One sound plan per settlement.</li><li>Determinism.</li><li>25 seeds × 3 settlements valid and sized to type.</li><li>Site keys and functions kept.</li><li>Water crossed only by bridges; buildings on buildable ground.</li><li>The plan saves exactly and is never regenerated (an edited plan loads as edited).</li><li>A corrupt plan is refused.</li><li>A v11 save gains its plans once, and loading again changes nothing.</li><li>The explored settlement is the plan; every metre of the way to every site door avoids buildings.</li><li>Herds graze real pastures.</li><li>Generation time.</li><li>The validator names each of 15 kinds of problem.</li></ul> |
| `Living/LivingWorldTests` | 11 | <ul><li>Every named person has a record; guards hold sound sheets.</li><li>Records are deterministic and survive save and load.</li><li>Promotion keeps identity and happens once.</li><li>Experience counts once per key; advancement uses the shared rules.</li><li>A damaged record is refused.</li><li>The bestiary validates.</li><li>Undead Fortitude fails against radiant damage.</li><li>A troll regenerates unless burnt, and dies at 0 when burnt.</li><li>Wildlife is deterministic and bounded.</li><li>Breeding stays under capacity; wolves take deer; hungry towns cull herds.</li><li>Impossible wildlife is refused.</li></ul> |
| `Living/ReactionTests` | 6 | <ul><li>An opportunity attack is asked and the move waits; declining lets it go.</li><li>Accepting equals answering at once.</li><li>Questions are answered once; stale answers are refused.</li><li>Shield spends a slot and raises AC.</li><li>Nothing is offered without its cost.</li><li>Computer reactors (opportunity attacks, Parry) decide through the same path.</li></ul> |
| `Living/EncounterGenerationTests` | 3 | <ul><li>Plans are deterministic for a world state and depend on the place.</li><li>Impossible plans are refused.</li><li>Outings draw persistent individuals.</li><li>The aftermath applies once: through commands, after save and load, and when resolved again directly.</li></ul> |
| `PlayMode/Phase10PlayModeTests` | 4 | Through the real UI: <ul><li>the inspector's Character section and the DM's promotion;</li><li>an outing from the HUD with the bestiary in the combat inspector, played to its end and settled once;</li><li>Shield asked through the prompt, accepted and declined;</li><li>the planned town on its terrain (ground collider, bridge decks, water); walking on the ground; residents at standing height; both views; save and load inside; return to the map; the strategic settlement drawn from the plan; the inspector's identity line.</li></ul> |
| `PlayMode/Phase10CaptureTests` | 4 | Evidence only: 15 screenshots and the scene performance measurements. |

## Existing tests changed, and why

No test was deleted or weakened. Each change is commented in the test itself.

| Test | Change | Reason |
|---|---|---|
| `NpcCommandTests`, `NpcEngineTests`, `RegionIdentityTests`, `PoliticalWorldTests`; `NpcPlayModeTests` (2) | Named people 15 → 19; town 6 → 9 | Phase 10 fills the posts the places call for: a captain, a guard and a priest in the town, a warden in the hamlet. |
| Parity tests in `CharacterTests` (v7), `CommerceTests` (v6), `NpcCommandTests` (v5), `RegionIdentityTests` (v2), `PoliticalWorldTests` (v4) | Compare old saves against `Phase2Worlds.GeneratedBeforeLivingWorld` | An old save cannot hold Phase 10 data. The deserializer alone adds none; the session's load does (tested in `SettlementLayoutTests`). |
| `Phase2CompletionTests` | The system order includes `realmkeeper.fauna` | A new system. |
| `SpellTests.FailedCasts` | Shield is refused with "cast as a reaction"; `detect_magic` is the "cannot be cast yet" example | Shield became a reaction spell. |
| `CombatIntegrationTests.PracticeEncounter…` | The expected ally is the rank-and-file guard before the captain | Practice fights bring the guard on duty, not the new captain. |
| `ExplorationPlayModeTests.Character_Walks…`, `RealScenePlayModeTests.ClickingACity…` | Distance walked is measured on the ground; "grounded" means on the plan's ground | A planned town's way in faces along its road, not north, and its ground is not flat. |
| `GraphicsPlayModeTests.EnteringATown…` | Terrain colliders are allowed; detail still has no mesh colliders | A planned town's ground is a terrain mesh with its own collider. |

## Fault injection (mutants)

Each mutant was applied to the source, then the whole .NET suite was built and run, then the source was restored
(`scratchpad/mutants/run.sh`).

| Mutant | Result |
|---|---|
| The validator ignores buildings on roads | Caught (`ACorruptPlan_IsRefused_NotRepaired`) |
| The validator accepts a bridge with one wet end | Survived at first. A test was added (`TheValidator_NamesEachKindOfProblem`), and then it was caught. |
| Regeneration is never blocked by fire | Caught (`ATroll_Regenerates…`) |
| The layout mapper drops `Foundation` | Caught (8 tests: save and load round trips) |
| The road graph joins only coincident points | Survived at first. The route test now samples every metre, and then it was caught. |
| A recorded reaction answer is ignored (always "yes") | Caught (`AnOpportunityAttack_IsAsked…`) |
| The experience-key check is removed | **Equivalent:** the world validator also refuses a duplicate key, so the command still fails. |
| The encounter's resolved flag is ignored | **Equivalent:** every consequence is itself keyed (dead stay dead, XP by key), so a second resolution changes nothing. The new direct test confirms it. |
| The fauna capacity clamp is removed | **Equivalent:** logistic growth cannot overshoot capacity. |
| The v12 migration omits `creaturePopulations` | **Equivalent:** the mapper treats a missing list as empty. |

## Bugs found and fixed during Phase 10 testing

- Planned towns doubled the per-day simulation cost (13.7 ms). Fixed by sharing immutable plans and caching their
  checks (1.88 ms). Unity's 15 ms/day budget tests now pass.
- An NPC's armour class showed a type name in the inspector. Fixed, and the test asserts on the text.
- Structure signs floated or were buried on sloping ground. They now hang at the building's floor height.
- Practice fights could bring the new captain, who ended fights before the Phase 9 casting test could cast twice
  (it went inconclusive). Practice now brings the guard on duty.
- Outings could field a band of twice the party's budget at level 1. Groups now stay within the moderate budget, and
  a leader joins only within the high budget.
- Settlement generation:
  - rivers through the square;
  - lanes through the hall;
  - bridges ending in water;
  - farm tracks through houses;
  - steep hilltops;
  - duplicate main roads from shared exits;
  - zero-length streets;
  - the ring street running along a stream.
  All are fixed, and checked over 40 seeds × 3 settlements with zero validator errors.
