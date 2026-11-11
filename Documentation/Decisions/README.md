# Architectural decision records

| ADR | Decision | Status |
|---|---|---|
| [0001](ADR-0001-unity6-urp-uitoolkit.md) | Unity 6 + URP + UI Toolkit | Accepted (6000.0.47f1) |
| [0002](ADR-0002-layered-assemblies.md) | Seven layered assemblies, ports in Application | Accepted |
| [0003](ADR-0003-engine-independent-core.md) | Engine-independent core plus an out-of-Unity .NET harness | Accepted |
| [0004](ADR-0004-deterministic-randomness.md) | xoshiro256**, derived streams, integer simulation state | Accepted |
| [0005](ADR-0005-geography-from-seed.md) | Geography rebuilt from seed, not saved | Accepted |
| [0006](ADR-0006-fixed-calendar.md) | Fixed 12 × 30-day calendar | Accepted |
| [0007](ADR-0007-json-persistence.md) | Newtonsoft JSON with DTOs and migrations | Accepted |
| [0008](ADR-0008-copy-validate-commit.md) | Copy → simulate → validate → commit per day | Accepted |
| [0009](ADR-0009-dual-perspective-world.md) | Dual-perspective world: strategic map and exploration share one simulation | Accepted (binding direction; not implemented) |
| [0010](ADR-0010-third-party-art.md) | Third-party art: CC0 only, used subset committed, licences on file | Accepted (graphics pass) |
| [0011](ADR-0011-modular-kit-assembly.md) | Buildings assembled from engine-free kit plans, merged for the draw budget | Accepted (graphics pass) |
| [0012](ADR-0012-humanoid-characters.md) | Humanoid characters whose look derives from identity | Accepted (graphics pass) |
| [0013](ADR-0013-tiered-npc-simulation.md) | Tiered NPC simulation inside the day; named people counted in the population | Accepted (Phase 4) |
| [0014](ADR-0014-npc-knowledge-and-dialogue.md) | Knowledge separate from truth; dialogue only from knowledge | Accepted (Phase 4) |
| [0015](ADR-0015-player-identity.md) | The player is one persistent domain entity, extended rather than replaced | Accepted (Phase 5) |
| [0016](ADR-0016-item-ownership.md) | Item definitions are data; instances are stacks with exactly one owner | Accepted (Phase 5) |
| [0017](ADR-0017-equipment-presentation.md) | Equipment shown from world data with the outfit kit's own parts and procedural props | Accepted (Phase 5) |
| [0018](ADR-0018-shop-economy-integration.md) | Shops kept by residents and stocked from the settlement economy, without double counting | Accepted (Phase 5) |
| [0019](ADR-0019-time-cadence.md) | Minutes on top of daily simulation; hourly behaviour derived, not stored | Accepted (Phase 5) |
| [0020](ADR-0020-rpg-record-on-player.md) | The RPG character is a record on the existing player; derived values never stored | Accepted (Phase 6) |
| [0021](ADR-0021-typed-rule-effects.md) | Species, background and class features are typed effects | Accepted (Phase 6) |
| [0022](ADR-0022-rules-configuration-and-licensing.md) | Fifth-edition-compatible rules from the SRD under CC BY 4.0 | Accepted (Phase 6) |
| [0023](ADR-0023-legacy-migration-and-kit-once.md) | Older players become Legacy; the starting kit is granted exactly once | Accepted (Phase 6) |
| [0024](ADR-0024-placeholder-species-appearance.md) | Species appearance is a contract; non-human species use labelled placeholders | Accepted (Phase 6) |
| [0025](ADR-0025-progression-in-the-character-record.md) | Progression lives in the one character record; level values derived | Accepted (Phase 7) |
| [0026](ADR-0026-rules-configuration-v7.md) | Rules configuration v7: SRD 5.2.1 progression on the Phase 6 base | Accepted (Phase 7) |
| [0027](ADR-0027-shared-rules-engine.md) | One shared rules engine with conditions, stacking, roll modes and prerequisites | Accepted (Phase 7) |
| [0028](ADR-0028-experience-ledger-and-explicit-advancement.md) | Experience ledger with idempotent awards, explicit advancement, outstanding choices | Accepted (Phase 7) |
| [0029](ADR-0029-character-agnostic-progression.md) | Progression services work on any character sheet | Accepted (Phase 7) |
| [0030](ADR-0030-domain-combat-engine.md) | Combat is a domain engine driven by commands; time stands still during a fight | Accepted (Phase 8) |
| [0031](ADR-0031-combatants-reference-identities.md) | Combatants reference persistent identities; the sheet holds the player's vitals | Accepted (Phase 8) |
| [0032](ADR-0032-deterministic-dice-and-action-sequence.md) | Counter-based dice per encounter; an action sequence against stale actions | Accepted (Phase 8) |
| [0033](ADR-0033-combat-rules-configuration.md) | Combat rules configuration (SRD 5.2.1) with recorded Realmkeeper choices | Accepted (Phase 8) |
| [0034](ADR-0034-death-and-dying.md) | Death saves for characters; creatures die; nothing comes back | Accepted (Phase 8) |
| [0035](ADR-0035-settlement-structures.md) | Settlement structures built on existing places, honestly classified | Accepted (Phase 8) |
| [0036](ADR-0036-combat-chronicle.md) | Combat reaches the chronicle as a few moments, never a log of swings | Accepted (Phase 8) |
| [0037](ADR-0037-shared-spell-engine.md) | One spell engine, driven by data, for every caster | Accepted (Phase 9) |
| [0038](ADR-0038-spell-state-as-character-choices.md) | Spell picks are character choices; preparation is separate state | Accepted (Phase 9) |
| [0039](ADR-0039-effects-and-concentration.md) | Lasting effects live in the encounter; concentration per caster; all end with the fight | Accepted (Phase 9) |
| [0040](ADR-0040-rests-and-recovery.md) | Rests pass world time first, then recover through one idempotent command | Accepted (Phase 9) |
| [0041](ADR-0041-class-feature-activation.md) | Class features act through one activation model matched by feature ID | Accepted (Phase 9) |
| [0042](ADR-0042-npc-spellcasting.md) | Creatures and residents cast through the same engine; caster stat blocks from SRD 5.1 | Accepted (Phase 9) |
| [0043](ADR-0043-npc-character-sheets.md) | Named people carry character records; sheets by archetype, promotion on demand | Accepted (Phase 10) |
| [0044](ADR-0044-bestiary-and-wildlife.md) | The bestiary is data; creatures are drawn from aggregate populations into named instances | Accepted (Phase 10) |
| [0045](ADR-0045-reaction-decisions.md) | Reactions and decisions are asked, not taken: suspend, ask, replay | Accepted (Phase 10) |
| [0046](ADR-0046-world-encounters.md) | Encounters come from the world, are checked before they start, and settle exactly once | Accepted (Phase 10) |
| [0047](ADR-0047-settlement-layouts.md) | Settlements have a persisted identity and a terrain-first plan | Accepted (Phase 10) |
| [0048](ADR-0048-persisted-spatial-data.md) | Spatial plans are persisted, versioned and generated once; schema v12 | Accepted (Phase 10) |
| [0049](ADR-0049-settlement-navigation.md) | People walk the plan's own roads: a road graph, ground heights and one leaving point | Accepted (Phase 10) |

Template: Context · Decision · Consequences · Alternatives considered.
