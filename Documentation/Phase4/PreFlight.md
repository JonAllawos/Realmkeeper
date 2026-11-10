# Phase 4 pre-flight: NPC Intelligence Engine

Written 2026-11-10, on top of `b0545b5` (graphics pass), before any Phase 4 code.

## What exists

- **`NpcData`** (Domain): `NpcId`, `Name`, `HomeSettlementId`, `FactionId`, `Role` (8 roles),
  `MetPlayerOn` and `ConversationCount`. Fifteen named residents in the default world (6, 5 and 4
  per settlement), created by `WorldPopulationInitializer.EnsureNpcs` with deterministic IDs. They
  are **members of** the settlement population, not additions: the validator checks named ≤ population.
- **Faction leaders** are `FactionData.LeaderNpcId`, the same NPC IDs.
- **Chronicle:** `HistoricalEvent` has settlement IDs, faction IDs (v5) and a facts dictionary.
  Events have a "source" fact (`player` or `simulation`).
- **Simulation:** daily systems run inside one copy → validate → commit transaction (ADR-0008),
  in this order: labour 50, production 100, trade 150, consumption 200, wellbeing 300,
  population 400, politics 450, chronicle 500. Politics shows the weekly-tick pattern
  (day number % 7).
- **Commands:** typed commands with handlers registered on the session (`PoliticalCommandHandlers`
  is the template). `context.Edit()` gives the working copy; `context.ReportEvent` records events.
- **Persistence:** JSON DTOs with stepwise migrations (v1 → v5). After mapping, once-only
  initializers fill data that cannot come from JSON (`PoliticalInitializer.Ensure`).
- **Exploration:** `LocalEnvironmentLayout` places one spot per NPC by role. `ResidentView` is
  keyed by `NpcId`; its look is derived (`NpcAppearanceResolver`, graphics pass).
  `TalkToResidentCommand` produces one generated line.
- **HUD:** UI Toolkit with code-built sections. The settlement inspector and the faction inspector
  follow the partial-class pattern (`RealmkeeperHud.Politics.cs`).

## What Phase 4 extends (no parallel system)

| Area | Extension |
|---|---|
| Identity | `NpcData` gains sex, birth date, tier, residence, workplace, current location, deceased |
| Personality, skills, condition | Bounded 0–100 values on `NpcData` |
| Goals | `NpcData.Goals`: typed, prioritised, with progress and status |
| Relationships | `WorldData.NpcRelations`: one record per related pair, created by a cause, never all-pairs |
| Memories | `NpcData.Memories`: bounded, decaying, linked to chronicle events |
| Knowledge | `NpcData.Knowledge`: known, believed or rumour, kept separate from the world's truth |
| Locations | `WorldData.Locations`: persistent places per settlement, mapped onto layout building keys |
| Schedules | A weekly pattern of location IDs per NPC; the daily system moves NPCs |
| Simulation | `NpcSystem` at order 420, between population and politics: tier 1 daily, tier 2 staggered weekly, tier 3 is the remaining population (aggregate) |
| Commands | Ask-about-topic for the player; edit and add commands for the DM; all atomic, sourced and chronicled |
| Persistence | Schema v6, `MigrationV5ToV6` (structural) and `NpcInitializer.Ensure` (once only, no invented history) |
| Exploration | Spots come from each NPC's authoritative current location; topics are answered from the NPC's own knowledge |
| UI | NPC list with filters, an NPC inspector with ten sections, and residents in the settlement inspector |

## Risks and how they are handled

1. **Double counting people:** named NPCs stay members of the population. Tier 3 is computed as
   population minus living named residents, never stored. If starvation or emigration takes the
   population below the named count, named residents die (lowest tier first), and the invariant
   is enforced by the validator.
2. **Cost:** relationships are created only by causes (household, workplace, conversation, shared
   events), and capped per NPC. Interactions are limited to residents sharing a location.
   Performance is measured at 15, about 100 and about 300 NPCs, including save and load.
3. **Determinism:** no shared RNG stream. Every choice comes from `SeedMixer` hashes of
   (date, NPC ID, purpose), so the result does not depend on iteration order.
4. **Omniscience:** dialogue reads only the NPC's knowledge entries, which go stale (known →
   believed) and travel as rumours with falling confidence. Tests assert that an NPC who never
   learned a fact cannot state it.
5. **Migration:** v5 saves get identity, personality, skills, locations and schedules derived
   from the seed and NPC ID, plus present-state knowledge of their own settlement. No memories,
   relationships beyond household and workplace ties, or past events are invented. Running it
   again changes nothing.
6. **Time of day:** none is added (the graphics brief deferred it). Schedules are per weekday;
   an NPC spends the day at one location.
7. **Scope:** no LLM or external AI. Responses are composed from typed knowledge.
