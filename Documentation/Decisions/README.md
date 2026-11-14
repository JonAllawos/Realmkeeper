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
| [0050](ADR-0050-item-model.md) | Items are definitions plus instances; every instance is made in one place | Accepted (Phase 11) |
| [0051](ADR-0051-holders-and-transfers.md) | Every instance has one holder; transfers are atomic commands; weight limits the pack | Accepted (Phase 11) |
| [0052](ADR-0052-equipment-and-magic-in-the-rules.md) | Equipment state and magic items act only through the rules engine | Accepted (Phase 11) |
| [0053](ADR-0053-loot.md) | Loot is deterministic, filled once into a container, and bounded by rarity | Accepted (Phase 11) |
| [0054](ADR-0054-crafting-gathering-repair.md) | Crafting, gathering, repair and attunement are work that holds its inputs and takes world time | Accepted (Phase 11) |
| [0055](ADR-0055-item-ui.md) | The item screens are query snapshots plus command requests | Accepted (Phase 11) |
| [0056](ADR-0056-quests-as-bound-templates.md) | Quests are templates bound to real entities; one engine for authored and emergent quests | Accepted (Phase 12) |
| [0057](ADR-0057-objectives-and-quest-events.md) | Objectives advance from typed quest events inside the command's transaction | Accepted (Phase 12) |
| [0058](ADR-0058-dialogue-graphs.md) | Dialogue is data — conditional lines and responses, rolled checks, typed consequences | Accepted (Phase 12) |
| [0059](ADR-0059-reputation-dimensions.md) | Reputation has separate dimensions; deeds travel as knowledge, and only those who hear react | Accepted (Phase 12) |
| [0060](ADR-0060-emergent-activities-and-discovery.md) | Activities arise from world conditions; the player discovers them; the journal shows only what is known | Accepted (Phase 12) |
| [0061](ADR-0061-content-tags.md) | Quest and dialogue text carries content tags with configurable presentation; no campaign canon is fixed | Accepted (Phase 12) |
| [0062](ADR-0062-places-as-persistent-world-entities.md) | Explorable places are persistent world entities in a hierarchy, with one architecture for every kind | Accepted (Phase 13) |
| [0063](ADR-0063-layout-generation-and-validation.md) | Layouts are generated on a slot grid from archetypes, validated, and saved — never regenerated | Accepted (Phase 13) |
| [0064](ADR-0064-interaction-framework-traps-hazards.md) | One interaction framework for places; traps and hazards through the rules; fights among real walls | Accepted (Phase 13) |
| [0065](ADR-0065-discovery-and-player-knowledge-of-places.md) | What the player knows of a place is a state; the map, journal and scene show only that | Accepted (Phase 13) |
| [0066](ADR-0066-people-in-places-escort-rescue-garrisons.md) | People in places are the same persistent identities — held, following, posted or at home | Accepted (Phase 13) |
| [0067](ADR-0067-line-of-sight-and-cover.md) | One grid function for line of sight, line of effect and cover | Accepted (Phase 14) |
| [0068](ADR-0068-lasting-conditions-shared-damage-rest-rules.md) | Lasting conditions on the sheet, one damage rule, and rest that depends on where you are | Accepted (Phase 14) |
| [0069](ADR-0069-crime-witnesses-local-consequences.md) | Crime is a record of what happened; who knows is a matter of who perceived it | Accepted (Phase 14) |
| [0070](ADR-0070-indoor-movement-and-followers.md) | People move through rooms along real passages; followers keep a room of their own | Accepted (Phase 14) |
| [0071](ADR-0071-archetype-placement-wake-bridges-fire.md) | New places reach every world once, chosen by context; creatures wake in sight; bridges and fire have state | Accepted (Phase 14) |
| [0072](ADR-0072-combat-presentation-and-move-planning.md) | The combat interface shows and asks; one planner proposes moves; the engine decides | Accepted (Combat overhaul) |
| [0073](ADR-0073-companions-are-the-worlds-own-people.md) | Companions are the world's own people; the party is a record of who travels together | Accepted (Phase 15) |
| [0074](ADR-0074-loyalty-from-real-events.md) | Loyalty moves only from real events, once per cause, with its reason | Accepted (Phase 15) |
| [0075](ADR-0075-party-in-combat-and-companion-ai-layer.md) | The party fights through the one engine; companions think in a layer of the one AI, and say why | Accepted (Phase 15) |
| [0076](ADR-0076-connected-world-one-generator-coherent-borders.md) | The connected world is made by the one region generator, region by region, with coherent borders | Accepted (Phase 16) |
| [0077](ADR-0077-persistent-route-graph-and-travel-scale.md) | Travel follows a persistent route graph, measured on one stated scale | Accepted (Phase 16) |
| [0078](ADR-0078-journeys-on-the-one-clock.md) | A journey is a world action on the one clock, hour by hour, and ends in the settlement | Accepted (Phase 16) |
| [0079](ADR-0079-travelling-groups-carry-the-economys-own-goods.md) | Travelling groups are the world's own people and carry the economy's own goods | Accepted (Phase 16) |
| [0080](ADR-0080-world-events-typed-once-and-known-through-people.md) | World events are typed records with a lifecycle; consequences apply once; the player knows them through people | Accepted (Phase 16) |
| [0081](ADR-0081-simulation-tiers-and-the-world-atlas.md) | Distant regions are simulated in tiers; the world atlas is a view of queries, not of the world | Accepted (Phase 16) |
| [0082](ADR-0082-campaigns-are-data-laid-over-the-one-world.md) | A campaign is data laid over the one world; its instance points at the world's own entities and quests | Accepted (Phase 17) |
| [0083](ADR-0083-decisions-are-records-with-typed-keyed-consequences.md) | Decisions are records with typed, keyed consequences — some now, some later — applied once | Accepted (Phase 17) |
| [0084](ADR-0084-narrative-resilience-reads-what-became-of-people.md) | Narrative resilience reads what became of people from the world, and offers other ways — never resurrection | Accepted (Phase 17) |
| [0085](ADR-0085-the-world-offers-its-own-stories-from-what-happened.md) | The world offers its own stories, built only from what happened and who knows it | Accepted (Phase 17) |
| [0086](ADR-0086-road-fights-are-fought-on-the-ground-at-the-roads-end.md) | A fight met on the road is fought on the ground at the road's nearer end, with the existing combat; the party stays on the road | Accepted (Phase 17) |
| [0087](ADR-0087-companions-react-remember-and-relate-through-existing-records.md) | Companions react by their nature, say what they think when asked, and relate to each other through the existing records | Accepted (Phase 17) |
| [0088](ADR-0088-discoveries-have-a-kind-a-certainty-and-people-who-care.md) | Discoveries have a kind, a certainty, and people who care about them | Accepted (Phase 18) |
| [0089](ADR-0089-places-in-every-region-and-three-complete-mechanism-patterns.md) | Places in every region, and three complete mechanism patterns | Accepted (Phase 18) |
| [0090](ADR-0090-faction-conflicts-have-stages-and-factions-act-without-the-player.md) | Faction conflicts move through stages, factions act without the player, and the player can take part | Accepted (Phase 18) |
| [0091](ADR-0091-four-quest-chains-and-a-deeper-waystone-count.md) | Four quest chains, and The Waystone Count deepened without changing what it is | Accepted (Phase 18) |
| [0092](ADR-0092-rewards-are-tools-with-uses-and-are-given-once.md) | Rewards are tools with uses, and every reward is given once | Accepted (Phase 18) |
| [0093](ADR-0093-every-world-event-has-follow-up-gameplay.md) | Every kind of world event has follow-up gameplay, and the story lifecycle is audited | Accepted (Phase 18) |
| [0094](ADR-0094-peoples-days-show-their-natures-and-they-talk-to-each-other.md) | People's days show their natures and their town's condition, and they talk to each other about what they know | Accepted (Phase 18) |
| [0095](ADR-0095-companions-react-to-more-and-have-more-stories.md) | Companions react to more of what happens, keep quiet in between, and have more stories | Accepted (Phase 18) |
| [0096](ADR-0096-save-schema-v20.md) | Save schema v20 | Accepted (Phase 18) |
| [0097](ADR-0097-third-party-creatures-and-scenery-through-one-pipeline.md) | Third-party creatures and scenery come in through one pipeline, licence first | Accepted (Phase 18) |
| [0098](ADR-0098-one-design-system-and-a-shared-ui-assembly.md) | One design system (tokens, shared components) and a shared UI assembly | Accepted (Phase 19) |
| [0099](ADR-0099-interface-assets-and-fonts.md) | Interface assets and fonts: CC BY icons, CC0 kits, OFL fonts | Accepted (Phase 19) |
| [0100](ADR-0100-species-lineages-rules-9.md) | Species lineages as choices (rules 9) | Accepted (Phase 20) |
| [0101](ADR-0101-save-schema-v21-identity-appearance.md) | Save schema v21: lineages, identity, height and build | Accepted (Phase 20) |
| [0102](ADR-0102-people-known-as-far-as-the-player-knows-them.md) | People are known as far as the player knows them | Accepted (Phase 20) |
| [0103](ADR-0103-lineage-mechanics-played.md) | Lineage benefits played through the existing systems | Accepted (Phase 20A) |
| [0104](ADR-0104-eye-colour-iris-mask-schema-v22.md) | Eye colour through an iris mask (schema v22) | Accepted (Phase 20A) |
| [0105](ADR-0105-seasons-and-regional-weather-in-the-simulation.md) | Seasons and regional weather live in the simulation; the street only shows them | Accepted (Phase 21) |
| [0106](ADR-0106-shop-kinds-supply-records-and-specialist-shops.md) | Shop kinds, supply records and specialist shops | Accepted (Phase 21) |
| [0107](ADR-0107-work-stations-in-shops-and-keepers-production.md) | Work stations in shops, and keepers' production | Accepted (Phase 21) |
| [0108](ADR-0108-furnished-interiors-and-settlement-errands.md) | Furnished interiors from a plan never saved; settlement errands | Accepted (Phase 21) |
| [0109](ADR-0109-level-one-baseline-and-player-driven-level-up.md) | A level 1 baseline at creation, and a player-driven level-up on the explicit advancement | Accepted (Phase 22) |
| [0110](ADR-0110-walkable-scale-overland-terrain-and-nine-regions.md) | A walkable scale of 40 m a cell, overland terrain rules, and nine regions in a new game | Accepted (Phase 23) |
| [0111](ADR-0111-realms-gateways-and-the-players-overland-location.md) | Realms, gateways, and the player's overland location | Accepted (Phase 23) |
| [0112](ADR-0112-modular-regions-loading-transitions-and-overland-streaming.md) | Modular regions, loading transitions, and streaming within a region | Accepted (Phase 23) |
| [0113](ADR-0113-one-travel-state-road-and-country-waypoints.md) | One travel state for the road and the country; the map position; waypoints | Accepted (Phase 24) |
| [0114](ADR-0114-landmarks-visible-travellers-overland-encounters.md) | Landmarks, visible travellers, and overland encounters | Accepted (Phase 24) |
| [0115](ADR-0115-ground-weather-from-the-existing-weather.md) | Ground weather derived from the existing weather | Accepted (Phase 24) |

Template: Context · Decision · Consequences · Alternatives considered.
