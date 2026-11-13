# Realmkeeper Game Design Bible

This file holds the **Game Design Bible as supplied by the project owner** with the Phase 6 brief
(2026-11-10). The Bible's own text follows the status section, unchanged.

Two parts are maintained:
- **The status section** below, by implementation phases (Bible §21, items 7, 8 and 10).
- **The "Owner design amendments" section**, which records later owner requirements that extend
  the supplied text. Where an amendment and the supplied text differ, the amendment is the newer
  owner decision.

The phase-by-phase plan from Phase 21 onward is in [`FutureRoadmap.md`](FutureRoadmap.md).

> **Relationship to `ProductFoundation.md`.** The binding product foundation describes "a persistent
> fantasy world simulation and exploration platform". The Bible extends this into a full RPG
> (combat, magic, companions, campaign). The two agree on persistence, determinism, local play and
> optional AI. Reconciling them, for example with a decision record that supersedes parts of the
> foundation, is an **owner decision** and has not been made. Each phase builds only what its own
> brief authorises.

## Implementation status (updated at the end of Phase 22, 2026-11-13)

Status words: **Implemented** = built, tested and in the shipped scene; **Foundation** = the data
and rules exist and are tested, but the system that uses them does not yet; **Placeholder** =
present but visibly temporary; **Not started**.

### Roadmap (§18)

| Phase | System | Status |
|---|---|---|
| 0–1 | Foundation | Implemented |
| 2 | Civilization | Implemented |
| 3 | Factions | Implemented |
| 4 | NPC Intelligence | Implemented |
| 5 | Living World & Player Hub | Implemented (`1f37bb6`) |
| 6 | Character Creation | Implemented (`f2682a2`) |
| 7 | RPG Progression | Implemented (`a3cd9f5`) |
| 8 | Combat (foundation) and settlement structures | Implemented (`2fe83eb`) |
| 9 | Spellcasting, class features and combat integration | Implemented (`27a5261`) |
| 10 | Bestiary, NPC character systems, encounters, settlement generation | Implemented (`1707805`) |
| 11 | Inventory, loot, equipment, crafting and item progression | Implemented (`7b2831b`) |
| 12 | Quests, dialogue, reputation and world interaction | Implemented (`1e593ae`) |
| 13 | Persistent dungeons, ruins, interiors and exploration | Implemented (`cda9da0`) |
| 14 | Exploration integrity, living locations, world reactivity (and the combat overhaul) | Implemented (`242fde2`, `08c222e`) |
| 15 | Companions, parties, relationships and party progression | Implemented (`9a48d27`) |
| 16 | World events, regional travel, dynamic simulation and persistent world expansion | Implemented (`7ab5163`) |
| 17 | Campaign engine and narrative systems | Implemented (`8f36025`) |
| 18 | World depth, exploration and living content | Implemented (`713b9a0`) |
| 19 | UI/UX and visual design overhaul (first pass) | Implemented, partly (`6f225d8`; see its report) |
| 20 | Advanced character creation, character systems and character UI | Implemented (`6dce1a2`, pushed) |
| 20A | Character systems completion: lineage mechanics in play, eye colour, benefit status, familiarity and progression audits; free asset integration | **Implemented** (`77240e7`, pushed). See the Phase 20A report. |
| 21 | Environment and settlement depth: weather, seasons, functional shops, crafting stations, keepers' production, furnished interiors, settlement errands | **Implemented** (committed locally, not pushed; see `Reports/Phase21-Completion-Report.md`). Not hand-played: the owner checklist is open. This row previously read "Mature world content and immersion"; that scope is now planned as Phase 33. |
| 22–34 | Level 1 integrity, the open world (frame, streaming, geography, traversal, navigation, living overland), optional fast travel, advancement depth, visual identity, interface overhaul, mature content, release readiness | **Planned, not started.** Proposed order and detail in [`FutureRoadmap.md`](FutureRoadmap.md); each phase needs its own brief |

### §7 Character and RPG system

| Area | Status | Notes |
|---|---|---|
| Six abilities, modifiers | Implemented | Derived; increases from species, feats, Epic Boons and capstones, each with its cap |
| Standard array, point buy | Implemented | No manual entry or rolling |
| 18 skills, proficiency, expertise | Implemented | Expertise from levels too; Jack of All Trades; Reliable Talent shown |
| Six saving throws | Implemented | Save proficiencies and aura bonuses from levels; roll modes |
| 12 species | Implemented as rules, with SRD 5.2.1 lineages for tiefling, goliath and gnome (Phase 20, rules 9). **Phase 20A:** the goliath ancestries and the tiefling legacy cantrips are played in combat; the gnome utility magic, the tiefling level 3 and 5 spells and the breath weapon are not, and say so. **Placeholder** art | Non-human species show the human model at their height, with their own skin tones and the person's height and build (Phase 20). Ears, horns, tails and scales: Not started (no fitting licensed asset). |
| 12 backgrounds | Implemented | Social features are Foundation |
| 12 classes | **Implemented, levels 1–20** | SRD 5.2.1 features, scaling, resources, slots; distinct and validated |
| Subclasses | Implemented (one per class) | The SRD 5.2.1 subclass; the SRDs hold no others. Further subclasses would be Realmkeeper originals: Not started. |
| Character creator, sheet, person inspection (Phase 20) | Implemented | Three-region creator with live build; sheet tiles, explanations and Origins; "About them" by acquaintance (ADR-0102); pronouns and concept; height and build |
| Feats | Implemented (SRD set) | General, Origin, Fighting Style, Epic Boon; prerequisites and repeat rules |
| XP and levelling | **Implemented** | Keyed ledger; explicit level-up window; multi-level transactions |
| Hit points, hit dice | Implemented | Fixed averages; current HP rises with the maximum; damage, healing, temporary HP |
| Armour class | **Implemented** (SRD armour, Phase 8) | Categories, Dex caps, shields, training penalties, Strength requirements, breakdown (ADR-0033) |
| Class resources | **Implemented** | Spend, restore and recover; short and long rests pass world time and recover explicitly per resource (Phase 9, `Systems/RestAndRecovery.md`) |
| Spellcasting | **Implemented** (Phase 9) | 47 SRD 5.2.1 spells (28 functional, 12 partial, 4 data-only, 3 deferred after Phase 10's reaction prompt), with known, prepared and spellbook state and picks at creation and level-up. Slots and Pact Magic, upcasting, areas, concentration. One engine for player and NPCs. Spellbook in the Hub, casting from the encounter panel (`Systems/Spellcasting.md`, `SpellCatalogAudit.md`) |
| Combat features | **Implemented**, partly simulated | 38 combat features across the twelve classes through one activation model, each Functional or Partial with its gap named; reaction features and spells are asked through the reaction prompt (Phase 10, ADR-0045) (`Systems/ClassFeatures.md`) |
| Turn-based combat | **Implemented** (Phase 8) | Encounters, initiative, action economy, 5-ft grid, opportunity attacks, SRD attacks and damage, 15 conditions, death saves; deterministic AI; practice fights and, since Phase 10, outings that draw encounters from the world's creature populations (ADR-0046) (`Systems/Combat.md`) |
| Death and dying | **Implemented** (Phase 8) | Death saves for the player and residents; creatures die; non-lethal knock-outs; no resurrection (ADR-0034) |
| Bestiary | **Implemented** (Phase 10) | The Phase 8–9 creatures plus 20 from SRD 5.1 and 5 labelled Realmkeeper originals: categories, behaviours, traits (Charge, Regeneration, Undead Fortitude, Relentless, Nimble Escape, Parry), riders; stand-in figures, no models (ADR-0044) |
| Wildlife and herds | **Implemented** (Phase 10), aggregate | Regional populations breed, are hunted and preyed upon weekly; settlements keep herds grazing their pastures; encounters draw named individuals whose fates persist (ADR-0044) |
| Settlement layouts | **Implemented** (Phase 10) | A persisted identity and terrain-first plan per settlement: ground, water, bridges, roads, districts, buildings on buildable ground, farms, landmarks, walls; one plan for the walkable settlement, the strategic map and the inspector (ADR-0047, ADR-0048) |
| Settlement structures | **Implemented** (Phase 8), simulation partly | Functional ones are the existing places and shops; informational and decorative ones are listed and signed, with no production (`Systems/SettlementStructures.md`) |
| Character creator | Implemented | Now includes the rules-v7 level-1 choices |
| Character sheet in the Player Hub | Implemented | Adds Progression, breakdowns, roll modes, senses, movement |
| NPC and companion progression | **Implemented** for NPCs (Phase 10) and companions (Phase 15) | Every named person has a record; fighters, healers and wardens hold full sheets by the player's rules; promotion, keyed experience and advancement (ADR-0043). A recruit without a sheet is promoted once on joining; companions gain experience from the fights they are in, by the same keyed awards (ADR-0073, ADR-0075) |
| §11.2 Companions and party | **Implemented** (Phase 15) | Recruit named people (with stated refusals), temporary or permanent; dismiss and rejoin; 3 active and 3 in reserve; follow, wait, regroup, formation order and leader; their own items and equipment; following bodies in settlements and places; fights beside the player with a companion AI layer that explains itself; loyalty from real events only, departure and return; one personal quest; resting together; save v17 (ADR-0073–0075, `Systems/Party.md`). Companion backgrounds as authored stories: Not started (the personal quest is one template) |
| §10.1 Equipment | **Implemented** (Phase 11) | 114 items (66 SRD 5.2.1); eleven slots with two rings, off-hand Light weapons, ammunition; per-instance condition that changes the rules and wears in play; the hub's numbers are the rules engine's (ADR-0050, ADR-0052) |
| Magic items, attunement | **Implemented** (Phase 11), 11 items | Bonuses and effects as typed rule effects; attunement (three, a short rest); a cursed item that binds; a wand casting through the spell engine with charges and recharge |
| §10.2 Loot | **Implemented** (Phase 11) | 15 tables, carried gear, seeded and filled once into a container per fight; caches per settlement; rarity bounded (ADR-0053) |
| Inventory and storage | **Implemented** (Phase 11) | Instances with state; one holder each; weight-limited pack (SRD carrying capacity); the player's chest per settlement; search, filters, sorting, inspection and rules-engine comparison (ADR-0051, ADR-0055) |
| Crafting, gathering, repair | **Implemented** (Phase 11), 11 recipes | Work that holds its materials, rolls a tool check and takes world time; finite gathering sites from the settlement economy, recovering weekly; mending by an armourer or by the player (ADR-0054). Armorsmithing and enchanting recipes Not started |
| Quests | **Implemented** (Phase 12), 6 quests | Data-driven templates bound to real people and places; validated states; stages, alternatives, optional, hidden, timed objectives fed by events from commands; atomic, idempotent completion and rewards; 3 authored (a settlement problem with three endings, a hunt through the encounter system, a sealed-letter delivery) and 2 emergent (food shortage, empty shelves) (ADR-0056, ADR-0057). Escort, rescue and protect used by "Lost Below" (Phase 13) |
| Dialogue | **Implemented** (Phase 12) | Conditional lines and responses, rolled skill checks (Persuasion, Intimidation, Deception, Insight), typed consequences through one command; the Phase 4 topics inside it; numbered keyboard choices (ADR-0058) |
| Reputation and relationships | **Implemented** (Phase 12) | Seven regard dimensions per person, faction standing, settlement reputation, notoriety; keyed changes; deeds spread by Phase 4 sharing and only listeners react; standing moves prices and can close a shop (ADR-0059). Theft and assault as sources: Data-only |
| Journal and discovery | **Implemented** (Phase 12) | Quests by state, detail with hidden objectives hidden, tracking (3), rumours, discoveries, decisions, player-known Chronicle; notice board and ledger as world objects (ADR-0060) |
| Content settings | **Foundation** (Phase 12) | Tags and show/soften/hide rules on quest and dialogue text; no settings screen yet (ADR-0061) |
| Explorable places (dungeons, ruins, interiors) | **Implemented** (Phase 13), 5 wilderness places + interiors | Persistent places in a hierarchy (region, settlement, building, floor, room); 10 archetypes generated on a validated slot grid (2,500 seeds, 0 failures) and an authored fort; building interiors planned from function and footprint on first entry; state kept across visits and saves (ADR-0062, ADR-0063) |
| Exploration interactions, traps and hazards | **Implemented** (Phase 13) | One framework: inspect, search, open, close, lock, unlock, pick, force, disarm, read, listen, activate, free, attack — each a validated command with rules checks; 6 trap kinds and 5 hazards, fair by design; lasting conditions after a trap: Not implemented (ADR-0064) |
| Location-based encounters | **Implemented** (Phase 13) | A place's inhabitants are lasting individuals (or persistent people) fought with the one engine among the room's walls (blocked squares; AI routes around them); defeated stays defeated; loot lies in the room (ADR-0064) |
| Escort, rescue, protect | **Implemented** (Phase 13), one quest | The same persistent person held, freed, following through every transition and brought home; protect when they fight beside the player; garrisons posted at their fort (ADR-0066) |
| Map markers and place discovery | **Implemented** (Phase 13) | Unknown → Rumored (name and direction) → Discovered (marker, ways in) → Visited → Explored → Cleared; the in-place map shows only rooms seen (ADR-0065) |
| A connected world of regions | **Implemented** (Phase 16), 6 regions | Each region made by the one generator from a derived seed; coherent borders (ranges continue, crossings follow the terrain); identity, neighbours and landmarks from its own terrain; discovery (home visited, neighbours known by name, far lands uncharted); old worlds can discover the lands beyond once (ADR-0076) |
| Regional travel | **Implemented** (Phase 16) | A persistent route graph with roads, trails, passes and bridges; up to three costed alternatives; journeys hour by hour on the one clock (night at half pace); halt, resume, turn, give up; closed roads stop the way, and the way back stays open; arrival enters the town with the party (ADR-0077, ADR-0078) |
| Caravans and groups on the roads | **Implemented** (Phase 16) | Every shipment travels with a caravan led by one of the world's people; outlaws, patrols, faction patrols and refugees; attacks and losses through the economy's own accounting (ADR-0079) |
| World events | **Implemented** (Phase 16), 10 kinds | Shortages, unrest, outlaws, patrols, bridges, blockades, conflicts, disruptions, lost caravans, refugees: from causes, with a lifecycle, consequences applied once, chronicled; known through people as news and rumours; two event quests (ADR-0080) |
| Simulation tiers and the world atlas | **Implemented** (Phase 16) | Distant regions keep living at a lower cost; the atlas (A) with eight layers, inspectors, travel planning and the travel strip, showing only what the player knows (ADR-0081) |
| Campaign engine (§4.1, authored campaign) | **Implemented** (Phase 17), 1 provisional demonstration | Campaigns as validated data laid over the one world: roles cast from the world's own people, places, factions and roads; chapters and arcs of linked quests, decisions and world-state objectives (sequential, parallel, optional, alternative, timed, refusable); set aside, take up, give up; endings; the world never waits (ADR-0082) |
| Lasting decisions | **Implemented** (Phase 17) | Recorded once, typed effects applied once through each system's own path, delayed consequences that come in their time, disabled choices shown with their reason (ADR-0083) |
| Narrative resilience | **Implemented** (Phase 17) | What became of people read from the world (dead, captured, hostile, away …); other people speak, other ways open, or the story ends another way; nobody is protected or raised (ADR-0084) |
| Emergent stories (§4.1, emergent campaign) | **Implemented** (Phases 17–18), 7 kinds | A lost caravan, a hungry town beside a full one, refugees arriving, local unrest, a cut trade road, a recent death with a friend elsewhere, and a faction conflict become stories from what happened and who knows it; one per cause, one new a day, five open at most, offers lapse after 10 days; every kind of world event has follow-up gameplay; goods taken on the road come back when the band is broken (ADR-0085, ADR-0093) |
| Companions in the story | **Implemented** (Phases 17–18) | Reactions to decisions, quest outcomes, major discoveries, hardship at home and each other (once-only and cooldown rules), remarks when asked, companion-to-companion bonds and quarrels, four personal stories; followers refuse undrained deep water (ADR-0087, ADR-0095) |
| Discoveries | **Implemented** (Phase 18) | Ten kinds; confirmed or unverified, with proof that confirms a guess; every clue records; major findings give experience once; telling a finding to someone who cares rewards once per settlement; journal grouped by kind (ADR-0088) |
| Place puzzles and deeper places | **Implemented** (Phase 18), 3 patterns, 4 places per region | Bells rung in a painted verse's order, a pump drained in stages (a choked intake cracks it), raking light on a scored lintel; state saved with the place, failures cost something, never reset; Greywater Drift, the Ringers' Vault, Hollin Tower and Cairn Watch in every region (ADR-0089) |
| Authored quest chains | **Implemented** (Phase 18), 4 chains of 2 quests | Investigation, faction, wilderness/dungeon and settlement/economy; each with a decision, two or more endings and lasting consequences; *The Waystone Count* deepened (precedent from the places, a fourth way to settle the count) and still provisional (ADR-0091) |
| Faction conflict stages | **Implemented** (Phase 18) | Peace, tension, dispute, skirmishes, open conflict read from the relationship; weekly strain on border towns; factions send relief and garrison without the player; the player can back a side, mediate or stay out (ADR-0090) |
| People's natures in their day; overheard talk | **Implemented** (Phase 18) | Discipline, curiosity, compassion, courage and the town's prosperity change where people are; people who know each other talk about what they really know, worded by certainty; sitting poses deferred (ADR-0094) |
| Creature and scenery models | **Implemented** (Phase 18) | 19 creatures drawn with 15 CC0 or CC BY animal and monster models, animated or tipping over when they fall; settlement trees, wells and place props from CC0 kits; licences and credits recorded (ADR-0097) |
| Road fights in the combat scene | **Implemented** (Phase 17) | A fight met on the road is played with the existing tactical combat on the ground at the road's end; the party stays on the road (ADR-0086) |
| Main campaign, setting canon | **Not started** | The kingdoms, antagonist and world history are not approved canon; the one campaign is provisional demonstration content |
| Seasons, regional climate and weather (Phase 21) | **Implemented** | Seasons from the calendar; climate from region kind, latitude and mountains; fronts crossing the atlas; eight periods a day per region, saved (v23). Effects: seasonal farming and fishing, slower travel in bad weather, people shelter in storms. Shown in the street: rain, snow, clouds, fog, lightning, stars and moon, wet and snowy ground, seasonal daylight (ADR-0105). |
| Kinds of shop, supply records, work stations, keepers' production (Phase 21) | **Implemented** | Twelve kinds; specialists in towns' empty shop buildings; supply records with origins; imports only by open road; shortfalls explained; seven stations; 15 new recipes (26 in all); keepers make goods at their stations from their own stock (ADR-0106, ADR-0107). Keepers' purses run low without NPC customers buying: the wider shop economy is not modelled |
| Furnished interiors and errands (Phase 21) | **Implemented** | Rooms furnished by kind from a validated, deterministic plan (scenery, not saved); shop fit-outs per trade; residents run errands to their town's shops on free days (ADR-0108) |
| Open-world overland travel (amendment A1) | **Not started** | Travel is abstract today: journeys hour by hour on the atlas strip over the route graph. Settlement scenes end at their edge (110–200 m half-extent) with no surrounding landscape. Positions are region-local. Planned as Phases 23–29. |
| Navigation HUD, overland discovery by sight (amendment A1) | **Not started** | Place discovery states exist (ADR-0065); compass, minimap and landmark sighting do not. Planned as Phase 27. |
| Level 1 start (amendment A2) | **Implemented (Phase 22)** | Every new character passes `LevelOneBaseline` before creation commits: level 1, 0 experience, proficiency +2, HP from the hit die, level 1 features, slots and spells only, resources full, the kit once. Proven over every class, species, lineage and background. No experience is earned before creation (owner decision D4); pending play (walking, trading, talking) remains, and coin and belongings carried before creation are kept. Old saves keep what they hold (ADR-0109) |
| Player-driven level-up (Phase 22) | **Implemented** | A one-time, recorded level-up notice; a HUD button that stays bronze with the open choices; the window separates automatic benefits from the player's choices and shows what stays locked; picks are kept on closing and saving and resumed; confirming applies each level once; nothing is chosen for the player (ADR-0109) |
| Realmkeeper ruleset document (amendment A3) | **Baseline written (Phase 22)** | `Ruleset-Baseline.md` classifies each rule area as implemented, from SRD 5.2.1 (as-is or adapted), Realmkeeper-specific, incomplete, planned or unsupported. The full ruleset document and its name (decision D3) are Phase 30 |

### §19 Design principles: how Phases 6–7 respect them

- **2, 3 (custom protagonist, not a chosen one):** creation and every level choice are the player's.
  Older characters keep their choices, and new choices are asked for, never guessed.
- **7 (D&D-inspired, adapted):** SRD 5.2.1 / 5.1 under CC BY 4.0. The hybrid configuration is
  explicit (ADR-0022, ADR-0026).
- **8 (real mechanical effects):** level features change speed, initiative, saves, skills, senses,
  hit points, AC and resources now. The rest declare what they wait for.
- **14, 15 (data-driven; extend, not duplicate):** one record (ADR-0025); typed effects and one rules
  engine (ADR-0027).
- **16 (tests):** .NET, EditMode, PlayMode (six classes through the real level-up window), and
  mutation checks.
- **17, 18 (honest placeholders, licensing):** no new art; SRD content attributed; deferred content
  listed rather than invented.
- **20 (controlled phases):** Phases 7 to 11 stopped for review; Phase 12 stops here for review.
- **Phase 8, principles 8, 16 and 17:** every combat number comes from the SRD 5.2.1 rules engine and is explained on screen; the rules are mutation-checked; ground markers and stand-in figures are labelled placeholders; the creature descriptions are Realmkeeper's own.
- **Phase 9, principles 7, 8, 14 and 17:**
  - Spells and features are data resolved by the shared rules engine, and each one says honestly
    how complete it is.
  - Spell names are the SRD's; every description is Realmkeeper's own.
  - No non-SRD subclass, spell or creature was added.

### §20 Open questions

None were locked. Used as replaceable configuration, not canon:
- the SRD 5.2.1 experience table;
- the SRD's one subclass per class.

Still open: the final XP curve, the subclass catalogue, the spell list, the combat design details,
and the world's names and history.

The `ProductFoundation.md` and Bible scope question remains an owner decision.

New open decisions from the 2026-11-12 amendments are listed in `FutureRoadmap.md` §4 (D1–D5):
- the walkable world scale;
- what fast travel is;
- the ruleset's name and scope;
- pending play before creation;
- committing store-licensed assets.

---

## Owner design amendments (2026-11-12)

Supplied by the owner on 2026-11-12, while Phase 21 was in progress. Status words follow the
status section above. **Everything in this section is a requirement or a plan unless the status
table marks it implemented.**

### A1. Genuine open-world overland travel

**The problem today.** These facts were inspected in the code; see `FutureRoadmap.md` §1.
- **Settlement scenes are islands.** A settlement is a persisted layout 220–400 m across. Beyond
  its edge there is no continuing geography: nothing behind the player, no road leading away, and
  no distant mountains or towns.
- **The atlas is the only way between places.** Travel is the hour-by-hour journey on the strategic
  map (ADR-0077, ADR-0078).
- **Positions are region-local**, by design in ADR-0076.

**Requirement.** The player physically traverses one connected, persistent world. They can:
- leave any settlement, dungeon or place on foot;
- walk or run into the countryside;
- follow roads, trails, bridges, passes and natural routes;
- see meaningful terrain and recognisable landmarks in every direction;
- reach and explore places without selecting them on the map first;
- find alternate routes and hidden places;
- spend substantial in-game time on long journeys.

There is no blank zone around a settlement and no invisible wall that makes a place feel like a
level.

**Design references.** Skyrim and Kingdom Come: Deliverance 2 are studied for their patterns, such
as:
- landmarks visible from afar that pull the player toward them;
- roads that are themselves content;
- signposts and diegetic navigation;
- travel time that matters.

None of their content or proprietary systems is copied.

**Continuous world architecture.** This extends §3.2 and §15.1.
- **One authority.** The strategic map, local settlement environments, wilderness and places are
  views of one authoritative world. No duplicated world exists, and no scene invents its own
  geography.
- **Matching positions.** A settlement's atlas position is its real position in the walkable world.
  Roads, rivers and ranges connect logically across region borders, as ADR-0076 already requires
  for the map.
- **Streaming, not one scene.** The world is not one enormous Unity scene. It is a world coordinate
  frame with deterministic terrain tiles streamed around the player and floating-origin precision.
  The existing settlement and place builders are placed at their world anchors.
- **What the architecture must cover:** distance and travel time from one scale; persistence across
  region transitions; continuity of NPCs, creatures, caravans, patrols and events; save and load
  anywhere in the open; deterministic generation; stated performance and memory budgets.
- **The scale (decision D1)** must be settled first. At today's travel scale, about 154 km a region,
  a literal walk would take a day and a half per region.

**Terrain and geography.** Expected content:
- mountains and ranges, hills, valleys, cliffs and rock formations;
- forests, woods and clearings; plains, meadows, grassland and farmland;
- rivers, streams, lakes, waterfalls and coastlines;
- roads, trails, bridges, crossings and passes;
- farms, mills, watchtowers, ruins, forts, camps and roadside structures;
- natural landmarks;
- weather, atmosphere and seasons (Phase 21).

Terrain is not decoration. It decides:
- how the world is crossed;
- where settlements stand;
- how trade routes run;
- where creatures live;
- where encounters happen.

**Travel as gameplay.** Planned elements:
- walking and running; navigation and choice of route; roads versus wilderness; terrain movement
  costs;
- day and night; weather;
- wildlife and creatures; bandits, patrols and caravans; travellers, merchants and refugees;
- roadside discoveries and hidden places; gathering where appropriate;
- camps and rest; party and companion behaviour;
- travel quests and events; injuries and hazards;
- the world simulating on through long journeys.

Not a stream of repetitive random encounters: authored places, emergent situations with causes,
landmarks and discoveries, at a stated density.

**Navigation HUD.** Planned elements:
- a compass with heading;
- a minimap or local map;
- discovered roads and trails;
- nearby points of interest;
- destination and quest markers, with distances;
- optional route guidance;
- zoom and orientation.

The world itself also guides the player, through signposts and landmarks. Guidance is a player
setting (guided, markers only, or explorer), so exploration is never a permanent GPS line. The
strategic map remains for planning.

**Discovery.** A location can **exist** (in world data), be **known** (directions, rumours, maps,
documents, quests, travellers, merchants, faction or settlement knowledge, or seen from afar), and
be **discovered** (physically reached). This extends the place states of ADR-0065 and the region
states of ADR-0076 to the whole overworld.

**Fast travel** is optional, never foundational. It:
- may come late (proposed Phase 29);
- never replaces walking, and is never the only practical way to reach a settlement;
- respects geography and the road network;
- is justified in the world (known roads, real services);
- passes real time, so events and closures still apply.

**Status:** Not started. Planned as Phases 23–29 in `FutureRoadmap.md`.

### A2. Every new character starts at level 1

This extends §7.2 and §7.7.

**Creation.** Every new player character is a legitimate level 1 character:
- species and lineage;
- background and origin;
- class;
- ability scores;
- skills and proficiencies;
- level 1 features only;
- starting equipment, spells and resources;
- appearance and identity.

Creation never grants features, resources, equipment, spells or benefits of later levels, and never
quietly produces an advanced character.

**Level 1 validation.** A formal check covers:
- hit points, AC, proficiency, skills and saves;
- class features limited to level 1;
- appropriate species and lineage features;
- starting spells and slots;
- starting kit;
- resources and charges;
- no subclass features early;
- no higher-level abilities active;
- a sheet that matches the rules engine.

The check is class-specific: classes legitimately differ in what and how much they get at level 1.

**Progression.** Levelling is a clear, deliberate event:
- experience (or another documented model) makes a character eligible;
- a notification says so;
- a level-up workflow presents, for each level:
  1. what is gained automatically;
  2. what new options exist;
  3. which choices are required;
  4. which choices are optional;
  5. what will come at future levels.

The workflow also covers prerequisites, class-specific advancement, ability improvements and feats,
subclass at its level, spell selection, proficiencies, HP and resources, and an advancement history
that is saved.

Important choices are never made silently for the player. The sheet distinguishes:
- current abilities;
- newly gained abilities;
- unselected choices;
- future locked features;
- historical decisions.

Players and NPCs share one underlying progression model. NPCs may have a simpler presentation.

**Status:**
- The creation command already builds level 1 (Implemented, with gaps; see the status table).
- The level-up window and keyed ledger exist (ADR-0028).
- Phase 22 delivered the level 1 audit and baseline, and a player-driven level-up workflow (ADR-0109).
  Deeper advancement (more subclasses and options, class depth) is Phase 30.

### A3. Realmkeeper's own ruleset, not a 5e reproduction

This extends §7.1 and §19 principle 7.

Realmkeeper draws on D&D, including SRD 5.2.1 under CC BY 4.0, without being bound to reproduce 5e
exactly. Every rule area is to be classed as one of:
1. **Implemented by Realmkeeper today**, with its source.
2. **Inherited from SRD 5.2.1**: as-is, or adapted, with the adaptation stated.
3. **Realmkeeper-specific**, labelled as an original.
4. **Planned.**
5. **Deferred or deliberately unsupported**, for example multiclassing (not supported today).

Areas covered: species and lineages, backgrounds and origins, classes and subclasses, abilities and
derived statistics, skills and proficiencies, feats, spells and progression, equipment and
properties, conditions and combat, advancement, NPC construction.

The classification will live in a ruleset document (planned, Phase 30), with the baseline in
Phase 22.

**The Tavern Table** is the owner's separate D&D application. A standalone build exists on the
owner's machine; it has not yet been studied for this roadmap. It is a **design reference only**:
its species, class, item, spell and progression ideas may be studied and selectively adapted, but
its code, assets and data are not merged into Realmkeeper. Working Realmkeeper systems are not
rewritten to resemble it.

### A4. Workstreams reaffirmed

These requirements were established before and are restated so the roadmap keeps them:
- **Complete UI/UX and gameplay interface overhaul.**
  - The full interface, not a restyle: menus, world and campaign selection, world creation, loading
    screens, navigation, HUD and context menus.
  - Character creation and sheets, inventory and equipment, spellbook, journal and quests, map, NPC
    interaction.
  - Settlement, kingdom and faction management.
  - Settings, accessibility, key bindings, graphics, audio and gameplay.
  - A cohesive design language with feedback, transitions and responsive layouts.
  - The first pass was Phase 19. The rest is planned as Phase 32.
- **Advanced character creation and visual identity.**
  - More species and subspecies, species-specific anatomy and proportions.
  - Faces, hair, facial hair, skin, eyes, scars and tattoos.
  - Fitted clothing and armour, starting equipment, a high-quality 3D preview.
  - NPCs created and inspected by the same rules.
  - **Known gap:** non-human species are shown on the human model (ADR-0024, Phase 20).
  - Planned as Phase 31.
- **Mature world content and immersion**, intentional and contextual, never gratuitous:
  - contextual non-sexual nudity, profanity, blood, injuries and gore, violence and its consequences,
    darker themes, adult relationships and romance;
  - believable taverns, bathhouses, homes and medical settings;
  - player content settings applied consistently to dialogue, appearance, NPC behaviour and
    environments (hook: ADR-0061).
  - Planned as Phase 33. This was previously listed as Phase 21.
- **Asset research, integration and visual quality.**
  - Exact licence verification, attribution, confirmation of commercial use, technical suitability
    and performance.
  - Free does not mean commercially usable, and a marketplace listing does not mean a suitable
    licence.
  - Runs as a gate inside each phase (`FutureRoadmap.md` §6), building on `Research/Realmkeeper-Asset-*.md`.

### A5. Principles added to §19

These extend the list in §19; the original twenty stand.

21. **The player physically travels one connected world.** Physical travel is the primary way to
    move between places; fast travel is optional and in-world.
22. **Locations are known before they are reached.** Existence, knowledge and physical discovery
    are distinct.
23. **Every new character starts at level 1.** Every advancement is an explicit, player-facing
    event, and its choices are the player's.
24. **The ruleset is Realmkeeper's own.** It is D&D-inspired and SRD-informed, with every rule's
    origin and status stated.
25. **Planned is never shown as implemented**, in documents or in the game.

### A6. Known gaps and dependencies

| Gap | Depends on |
|---|---|
| No world coordinate frame; positions are region-local | Owner decision D1 (scale), then Phase 23 |
| No terrain beyond settlement edges; no physical roads between settlements | Phases 24–25 |
| Road fights staged on the near settlement's ground (Phase 17 limitation) | Phase 26 |
| No compass, minimap, or landmark sighting | Phases 25–27 |
| ~~Creation optional; pre-creation experience and coin carry over; no level 1 validator~~ | Resolved in Phase 22: D4 decided (no experience before creation); level 1 baseline |
| No full ruleset document (a baseline exists since Phase 22) | Decision D3; Phase 30 |
| Non-human anatomy placeholders | Asset gate L-Characters; Phase 31 |
| No content settings screen (tags exist, ADR-0061) | Phase 32, then Phase 33 |

### Cross-references

- **The plan:** `FutureRoadmap.md`; `../Roadmap.md` (history).
- **World and travel:**
  - `../Architecture/DualPerspective.md`, ADR-0009 (one world, two views);
  - ADR-0076 (connected regions), ADR-0077 (route graph and scale), ADR-0078 (journeys on the one
    clock), ADR-0081 (simulation tiers and the atlas), ADR-0086 (road fights);
  - `../Systems/Travel.md`, `../Systems/WorldGeneration.md`, `../Systems/Exploration.md`.
- **Discovery:** ADR-0065, ADR-0088.
- **Characters:**
  - ADR-0020, ADR-0022, ADR-0023, ADR-0025, ADR-0026, ADR-0028, ADR-0029, ADR-0043, ADR-0100,
    ADR-0103;
  - `../Systems/CharacterCreation.md`, `../Systems/CharacterProgression.md`,
    `../Systems/RulesEngine.md`.
- **Phase 21:** `../Briefs/Phase21-Brief.md`, `../Reports/Phase21-Preflight-Audit.md`.
- **Interface:** `../Design/Phase19-UI-Design-System.md`, ADR-0098, ADR-0099.
- **Assets:** `../Research/Realmkeeper-Asset-Research-Master.md`,
  `../Research/Realmkeeper-Asset-License-Registry.md`, `../Art/AssetInventory.md`.

---

## The Bible (as supplied)

```text
REALMKEEPER

GAME DESIGN BIBLE & RPG ARCHITECTURE

Document: Core Game Vision, Narrative Direction, Gameplay Pillars & Technical Design Authority
Project: Realmkeeper
Engine: Unity 6 / URP
Status: Foundational design specification
Applies to: All future development phases

⸻

1. THE VISION

1.1 What is Realmkeeper?

Realmkeeper is a persistent, living-world fantasy RPG and world simulation inspired by the depth, freedom, and character-driven storytelling of tabletop Dungeons & Dragons.

It combines:

* An explorable fantasy world.
* A persistent civilization and faction simulation.
* Deep character creation and progression.
* First-person and third-person exploration.
* Tactical combat.
* Meaningful quests and decisions.
* NPCs with identities, schedules, relationships, memories, and motivations.
* Shops, merchants, equipment, loot, and an evolving economy.
* A player-driven campaign that can follow an authored main story or emerge from the simulated world.

The central promise:

You are not simply playing through a fantasy story. You are living inside a fantasy world that continues to exist, change, and react to your decisions.

A kingdom can weaken while the player is exploring somewhere else. A merchant can become wealthy. A faction can gain influence. A settlement can experience shortages. NPCs can remember the player’s actions. Political conflict can change the opportunities and dangers available to the player.

The world should feel like a place, not a collection of disconnected game levels.

1.2 Genre

Primary:

* Fantasy role-playing game.
* Open-world exploration.
* Character-driven adventure.
* Persistent world simulation.

Secondary:

* Tactical combat.
* Civilization and faction simulation.
* Sandbox world interaction.
* Campaign management.

1.3 Core inspirations

Realmkeeper should draw inspiration from the design strengths of:

* Dungeons & Dragons: character identity, classes, races/species, abilities, skills, equipment, spells, quests, dice-inspired checks, and meaningful progression.
* Baldur’s Gate 3: character-driven storytelling, companions, dialogue choices, tactical encounters, and consequential decisions.
* Skyrim: exploration, environmental discovery, equipment, world immersion, and freedom of approach.
* Kingdom Come: Deliverance: grounded interactions, believable settlements, and physical presence in the world.
* Mount & Blade: faction politics, territorial conflict, economies, and evolving world conditions.
* The Witcher: authored questlines, morally complicated decisions, memorable characters, and consequences.
* Dwarf Fortress and similar simulations: systemic world behavior and emergent histories.

These are design references, not instructions to copy their copyrighted characters, worlds, art, or proprietary systems.

Realmkeeper must establish its own identity, lore, visual language, and mechanics.

⸻

2. THE PLAYER’S ROLE

2.1 The protagonist

The protagonist is fully custom-created by the player.

The player chooses:

* Name.
* Species.
* Appearance.
* Background.
* Class.
* Ability scores.
* Skills.
* Equipment.
* Personality through decisions and roleplaying.
* Allegiances and relationships.
* Long-term direction.

The protagonist is not a predetermined character with a fixed personality.

The game should not force the player into being a noble, soldier, assassin, chosen one, or savior.

The player begins as an individual whose importance develops through actions.

2.2 The protagonist’s identity

The protagonist should initially be someone with a reason to travel, explore, work, investigate, fight, or seek opportunity.

Possible starting identities include:

* A mercenary looking for work.
* An adventurer seeking fortune.
* A former soldier.
* A scholar pursuing forbidden knowledge.
* A wanderer escaping their past.
* A person seeking someone or something.
* A traveler caught in a conflict larger than themselves.

The character’s background should influence starting opportunities and dialogue, not lock the player into a predetermined narrative.

Design rule: The player earns their reputation. The world does not automatically treat them as important.

2.3 Player motivations

The game should support several motivations simultaneously:

* Wealth and material success.
* Fame and reputation.
* Power and influence.
* Knowledge and discovery.
* Loyalty and service.
* Revenge.
* Justice.
* Exploration.
* Personal survival.
* Building relationships.
* Protecting people or places.
* Pursuing the main campaign.

The player should be able to pursue personal ambitions while the larger world continues to develop.

⸻

3. THE WORLD AND ITS PREMISE

3.1 Core setting premise

Proposed foundational premise — subject to final narrative approval:

Realmkeeper takes place in an original fantasy realm experiencing a period of political instability, economic pressure, competing ambitions, and increasingly dangerous events beyond the control of ordinary people.

The realm contains kingdoms, settlements, noble houses, guilds, religious institutions, military organizations, mercenary companies, criminal networks, and independent communities.

These groups have competing interests.

Some seek stability. Some seek wealth. Some seek territory. Some pursue religious or ideological goals. Others seek power regardless of the consequences.

The realm is not waiting for the protagonist to arrive before anything happens.

Its history, economy, politics, population, and conflicts already exist.

The protagonist enters this world and gradually becomes involved in its affairs.

3.2 World structure

The world should be divided into:

* Realms and kingdoms.
* Regions.
* Territories.
* Settlements.
* Cities.
* Towns.
* Villages.
* Hamlets.
* Wilderness.
* Dungeons and ruins.
* Roads and trade routes.
* Important landmarks.
* Underground and interior locations.

Each location should belong to the same persistent world.

The overhead map and local exploration must represent the same underlying geography and simulation.

3.3 Visual presentation

Realmkeeper uses a hybrid visual approach.

Strategic world view:

* Attractive, readable, stylized-realistic world map.
* Clear terrain, roads, rivers, borders, settlements, and landmarks.
* Faction and economic overlays.
* Visual information about world conditions.

Local exploration:

* More detailed, realistic environments.
* Walkable settlements and interiors.
* First-person and third-person perspectives.
* Detailed character models, clothing, weapons, architecture, and lighting.
* Persistent NPCs and environmental interactions.

The strategic map and exploration environments must never become separate versions of the world.

⸻

4. THE CAMPAIGN

4.1 Two complementary campaign experiences

Realmkeeper supports both:

Authored main campaign

* A structured narrative.
* Major characters.
* Important locations.
* A central conflict.
* Major decisions.
* A meaningful conclusion.
* Optional branches and alternative outcomes.

Emergent sandbox campaign

* Quests generated from world events and faction needs.
* Personal goals.
* Exploration.
* Contracts and bounties.
* Trade and economic opportunities.
* Political involvement.
* Faction reputation and advancement.
* Player-created objectives.

These modes must coexist.

The player can pursue the main story, ignore it for extended periods, or spend time developing their own reputation and ambitions.

The world should continue to function regardless.

4.2 Proposed main-story structure

The following is a narrative framework, not locked canon.

Act I — The Arrival

The protagonist enters the realm with their own background, motivations, and circumstances.

They encounter:

* A settlement dealing with immediate problems.
* Local merchants and residents.
* Conflicting interests.
* A first opportunity for work.
* A local event that reveals something larger is happening.

The player learns how the world works through actual gameplay rather than lengthy exposition.

Act II — The Web of Power

The protagonist becomes involved with the people and organizations shaping the region.

They discover:

* Conflicting faction objectives.
* Political tensions.
* Economic interests.
* Personal rivalries.
* Hidden information.
* Opportunities for advancement.

The player can work with, against, or independently of major organizations.

Act III — The Greater Conflict

The regional situation develops into a larger conflict.

The protagonist’s accumulated choices determine:

* Which factions trust them.
* Which settlements support them.
* Which enemies pursue them.
* Which allies remain available.
* Which information they possess.
* Which opportunities are open.

Act IV — The Realm’s Future

The player faces major decisions affecting the future of the realm.

The conclusion should reflect the player’s choices, relationships, alliances, and actions.

Possible outcomes include political change, faction victory, negotiated peace, regional independence, personal power, or other outcomes supported by the eventual story.

Do not implement these acts as fixed quests until the narrative design is approved.

4.3 Quest categories

The quest architecture should support:

1. Main story quests.
2. Side quests.
3. Faction quests.
4. Settlement quests.
5. Personal background quests.
6. Companion quests.
7. Contracts and mercenary work.
8. Bounties.
9. Investigation quests.
10. Exploration and discovery quests.
11. Dynamic world-event quests.
12. Repeatable work and economic activities.

Quests should have structured objectives, prerequisites, state, outcomes, rewards, and consequences.

Avoid quests that exist only as dialogue text without functional objectives.

⸻

5. FACTIONS, ALLIES, AND ENEMIES

5.1 Faction structure

Realmkeeper already has a developing political and faction simulation.

Preserve and extend it.

Supported faction categories should include:

* Kingdoms.
* Noble houses.
* Guilds.
* Religious organizations.
* Military orders.
* Mercenary companies.
* Criminal organizations.
* Independent communities.

Each faction should have:

* Leadership.
* Membership.
* Goals.
* Resources.
* Influence.
* Territory or presence.
* Relationships.
* Reputation.
* Political interests.
* Historical events.

5.2 Who does the protagonist work for?

The protagonist should not be permanently employed by one organization.

They can:

* Work independently.
* Accept contracts.
* Become a mercenary.
* Serve a noble house.
* Join a military order.
* Work for a guild.
* Become an agent of a kingdom.
* Assist religious organizations.
* Work with criminal networks.
* Build their own influence.

Employment and allegiance should be distinct concepts.

Accepting a contract does not automatically make the player a permanent member of the organization.

5.3 Allies and enemies

Enemies and allies should emerge from the world and the player’s decisions.

Examples:

* A merchant becomes an ally after repeated successful dealings.
* A noble house becomes hostile after interference in its affairs.
* A faction rewards the player for completing contracts.
* A criminal organization targets the player after exposing its operations.
* A military commander respects a capable fighter.
* A settlement remembers someone who helped it survive a crisis.

Relationships should be persistent and grounded in events.

Do not make every NPC’s attitude change through arbitrary dialogue flags.

⸻

6. CORE GAMEPLAY LOOP

The primary gameplay loop:

1. Explore the world.
2. Discover people, places, opportunities, and threats.
3. Interact with NPCs and organizations.
4. Accept or create objectives.
5. Prepare through equipment, supplies, skills, and companions.
6. Travel and investigate.
7. Resolve challenges through dialogue, checks, exploration, stealth, or combat.
8. Receive rewards, experience, reputation, and information.
9. Develop the character.
10. Observe how the world responds.
11. Choose the next direction.

The player should be able to spend meaningful time outside combat.

Exploration, social interactions, trade, crafting if introduced later, investigation, and political decisions must be legitimate gameplay.

⸻

7. CHARACTER AND RPG SYSTEM

7.1 Rules foundation

Realmkeeper uses a hybrid D&D-inspired rules foundation.

It should preserve familiar tabletop concepts while adapting them to a real-time digital world and tactical combat.

Use reusable rules definitions rather than hardcoded mechanics.

Where SRD material is used, document its source and licensing obligations.

Do not assume all published D&D content is freely reusable.

7.2 Character creation

The character creator should support:

* Species.
* Appearance.
* Background.
* Class.
* Ability scores.
* Skills.
* Saving throws.
* Proficiencies.
* Languages.
* Starting equipment.
* Character description.

Character creation should feel like a real RPG system, not a collection of disconnected dropdowns.

7.3 Species

The initial foundational species catalog:

* Human.
* High Elf.
* Wood Elf.
* Dwarf.
* Halfling.
* Gnome.
* Orc.
* Half-Orc.
* Tiefling.
* Dragonborn.
* Aasimar.
* Goliath.

Species should affect mechanical and visual identity without dictating personality or alignment.

Additional original Realmkeeper species may be added later.

7.4 Classes

The twelve foundational classes:

* Barbarian.
* Bard.
* Cleric.
* Druid.
* Fighter.
* Monk.
* Paladin.
* Ranger.
* Rogue.
* Sorcerer.
* Warlock.
* Wizard.

Each class should have a distinct mechanical identity.

Classes should eventually support:

* Level progression.
* Features.
* Subclasses.
* Resources.
* Proficiencies.
* Equipment.
* Spellcasting where applicable.
* Class-specific choices.

7.5 Attributes

The six foundational abilities:

* Strength.
* Dexterity.
* Constitution.
* Intelligence.
* Wisdom.
* Charisma.

These should influence relevant checks, saving throws, combat calculations, dialogue options, and other appropriate systems.

7.6 Skills

The foundational skills:

* Acrobatics.
* Animal Handling.
* Arcana.
* Athletics.
* Deception.
* History.
* Insight.
* Intimidation.
* Investigation.
* Medicine.
* Nature.
* Perception.
* Performance.
* Persuasion.
* Religion.
* Sleight of Hand.
* Stealth.
* Survival.

Skills should be used in actual gameplay.

A high Persuasion score should create meaningful opportunities, not simply display a larger number on a character sheet.

7.7 Level progression

The long-term character progression target is levels 1–20, subject to final balance.

Progression should include:

* Experience.
* Level advancement.
* Hit point increases.
* Class features.
* Subclass selection.
* Ability improvements or feats.
* Proficiency progression.
* Spell progression.
* Resource progression.
* Equipment and loot opportunities.

Experience should be awarded for more than killing enemies.

Potential XP sources:

* Completing quests.
* Solving problems.
* Discovering locations.
* Defeating meaningful threats.
* Achieving objectives.
* Completing faction work.
* Significant roleplaying and investigation outcomes.

Avoid rewarding repetitive low-value actions with unlimited experience.

7.8 Character development

The character should develop through both mechanical and narrative progression.

Mechanical development includes levels, skills, equipment, abilities, and class features.

Narrative development includes relationships, reputation, history, titles, faction membership, and personal consequences.

The game should never make these two progression paths feel disconnected.

⸻

8. COMBAT DESIGN

8.1 Confirmed direction: Hybrid combat

Realmkeeper uses two complementary combat experiences.

Exploration and real-time encounters

* Environmental threats.
* Small encounters.
* Opportunistic attacks.
* Player movement and positioning.
* Immediate reactions.

Tactical encounters

* Turn-based or initiative-driven resolution.
* Action economy.
* Movement.
* Attacks.
* Abilities.
* Spells.
* Conditions.
* Cover and terrain.
* Party positioning.
* Enemy behavior.

The transition between these approaches must be coherent and predictable.

Do not create two completely unrelated combat systems.

8.2 D&D-inspired mechanics

The eventual combat system should support:

* Initiative.
* Actions.
* Bonus actions or equivalent.
* Reactions.
* Movement.
* Attack rolls.
* Saving throws.
* Damage types.
* Armor and defense.
* Advantage and disadvantage or an equivalent mechanic.
* Conditions.
* Death and recovery.
* Class abilities.
* Spells.
* Equipment effects.

8.3 Weapons

Support distinct weapon categories:

* Simple melee.
* Martial melee.
* Simple ranged.
* Martial ranged.
* Finesse weapons.
* Heavy weapons.
* Two-handed weapons.
* Versatile weapons.
* Improvised weapons.

Weapon definitions must contain actual gameplay data.

A sword should not simply be a visual model with a damage number attached in a UI.

⸻

9. MAGIC AND SPELLCASTING

Magic is a major pillar of Realmkeeper.

The architecture must eventually support:

* Spell lists.
* Spell levels.
* Casting time.
* Range.
* Duration.
* Components where relevant.
* Spell slots or class-specific resources.
* Concentration.
* Saving throws.
* Attack rolls.
* Damage and healing.
* Status effects.
* Summoning.
* Area effects.
* Environmental effects.
* Spell progression.

Different magical classes should not all feel identical.

Clerics, druids, wizards, sorcerers, bards, paladins, rangers, and warlocks should retain distinct identities.

Magic must interact with the world.

Examples:

* A spell can illuminate a dark room.
* Fire can interact with appropriate environmental objects.
* Healing can restore a wounded character.
* Magical abilities can influence exploration and problem solving.
* NPCs can react to visible magic.

Do not implement a spell catalog that only works inside combat.

⸻

10. EQUIPMENT, INVENTORY, LOOT, AND ECONOMY

The existing Phase 5 item and commerce systems are foundational.

Extend them rather than replacing them.

10.1 Equipment

Equipment should include:

* Weapons.
* Shields.
* Armor.
* Clothing.
* Helmets.
* Gloves.
* Boots.
* Accessories.
* Rings.
* Amulets.
* Cloaks.
* Magical equipment.

Items should have meaningful definitions, slots, properties, and effects.

10.2 Loot

The long-term loot system should support:

* Common.
* Uncommon.
* Rare.
* Very rare.
* Legendary.
* Unique or artifact items.

Rarity should be meaningful, not just a color.

Items should be obtainable through:

* Exploration.
* Quests.
* Shops.
* Crafting if introduced.
* Treasure.
* Enemy encounters.
* Faction rewards.
* Special events.

10.3 Economy

The economy should remain connected to the existing settlement simulation.

Prices should respond to:

* Supply.
* Demand.
* Shortages.
* Prosperity.
* Unrest.
* Merchant behavior.
* Location.
* Faction conditions.

Merchants should have persistent identities and actual inventory.

⸻

11. NPCs AND COMPANIONS

NPCs are not disposable dialogue containers.

Important NPCs should have:

* Persistent identity.
* Species.
* Background.
* Occupation.
* Personality.
* Skills.
* Goals.
* Relationships.
* Memories.
* Knowledge.
* Faction affiliation.
* Schedule.
* Location.
* Inventory where appropriate.
* Health and condition where appropriate.

11.1 NPC simulation

NPCs should have different simulation detail depending on importance and proximity.

Not every resident needs expensive individual simulation every second.

Use appropriate simulation tiers:

* Aggregate population.
* Low-frequency offscreen NPC simulation.
* Persistent important NPC simulation.
* Detailed nearby NPC simulation.

The same NPC must remain the same person when the player encounters them in different locations.

11.2 Companions

The player should eventually be able to:

* Recruit companions.
* Dismiss companions.
* Develop relationships.
* Learn companion backgrounds.
* Complete companion quests.
* Equip companions.
* Assign combat roles.
* Manage party composition.
* Experience companion approval or disapproval.

11.3 Party direction

Both solo and party play are supported.

The player can travel alone or recruit companions.

The world and quest systems should not require a full party to function.

Companions should add strategic and narrative depth rather than become mandatory.

⸻

12. WORLD SIMULATION

This is one of Realmkeeper’s defining features.

The world should operate independently of the player.

12.1 Civilization

Settlements should simulate:

* Population.
* Resources.
* Production.
* Consumption.
* Prosperity.
* Stability.
* Trade.
* Shortages.
* Growth or decline.

12.2 Politics

Factions should simulate:

* Influence.
* Relationships.
* Territory.
* Claims.
* Governance.
* Goals.
* Alliances.
* Rivalries.
* Conflict.

12.3 History

The world should record significant events.

Examples:

* A settlement experiences famine.
* A merchant becomes wealthy.
* A faction expands its influence.
* A noble is removed from power.
* A road becomes unsafe.
* A military conflict begins.
* A character dies.
* A political alliance forms.

Events should have identifiable causes and consequences.

12.4 Persistence

The world must retain continuity across:

* Strategic map.
* Local exploration.
* Player Hub.
* NPC interactions.
* Shops.
* Quests.
* Combat.
* Save/load.

No major system should maintain a disconnected duplicate of world state.

⸻

13. QUEST AND NARRATIVE ARCHITECTURE

Quests must be represented as structured gameplay systems.

A quest should support:

* Stable ID.
* Title.
* Description.
* Source.
* Issuing NPC or faction.
* Objectives.
* Prerequisites.
* Active state.
* Completion conditions.
* Failure conditions.
* Rewards.
* Consequences.
* Related events.
* Journal history.

Quest sources can include:

* Authored campaign content.
* NPC requests.
* Faction objectives.
* Settlement conditions.
* World events.
* Player discovery.

Quest outcomes should update the actual world.

For example, completing a supply quest should affect the settlement’s supply situation if the quest was designed to do so.

Do not allow quest completion to be merely a UI checkbox.

⸻

14. PLAYER FREEDOM AND CONSEQUENCES

Realmkeeper should avoid unnecessary invisible restrictions.

The player should be able to:

* Ignore the main quest.
* Refuse faction work.
* Change allegiances.
* Explore independently.
* Become wealthy through trade.
* Develop combat skills.
* Pursue social influence.
* Build relationships.
* Make enemies.
* Fail quests.
* Miss opportunities.
* Experience consequences.

However, freedom must be supported by coherent simulation.

Do not create superficial dialogue choices that have no effect.

Do not promise that every possible action can be supported.

Prioritize meaningful systemic freedom over a huge number of shallow options.

⸻

15. TECHNICAL ARCHITECTURE

15.1 Authoritative world state

Realmkeeper has one authoritative world state.

Strategic map, local environments, NPCs, shops, quests, and player systems read from or mutate that state through controlled application interfaces.

Do not create independent world copies for separate game modes unless the existing transactional architecture explicitly requires temporary working copies.

15.2 Architecture principles

Preserve:

* Plain C# domain models.
* Simulation independent of UnityEngine.
* Application commands and queries.
* Infrastructure persistence.
* Presentation separated from rules.
* Stable GUID-based identities.
* Deterministic seeded simulation.
* Explicit transactions.
* Typed command handlers.
* Versioned saves and migrations.
* Testable simulation logic.

15.3 Simulation

The simulation should be deterministic wherever practical.

Given identical initial state, seed, and commands, it should produce identical results.

World time should be explicit.

Systems should have documented execution order.

Do not use uncontrolled random behavior in core simulation.

15.4 Content architecture

Prefer data-driven content definitions for:

* Species.
* Classes.
* Backgrounds.
* Skills.
* Spells.
* Items.
* Weapons.
* Armor.
* NPC templates.
* Factions.
* Quests.
* Enemies.
* Locations.

Avoid massive hardcoded conditional blocks.

Use typed effects and validated content references.

15.5 AI

AI is a future optional narrative layer.

It may eventually propose:

* Dialogue.
* Quest descriptions.
* Character reactions.
* Narrative summaries.
* Rumors.
* World-event interpretations.

AI must not become the authoritative simulation.

Any proposed world mutation must pass through the same validation and command architecture as ordinary player actions.

Realmkeeper must remain playable without an AI service or internet connection.

15.6 Performance

Prioritize:

* Deterministic simulation.
* Efficient world updates.
* Region-based loading.
* Simulation detail tiers.
* Nearby character detail.
* Efficient character animation.
* Reusable environment assets.
* Profiling before optimization.

Do not introduce ECS/DOTS, multiplayer infrastructure, or massive streaming systems prematurely.

⸻

16. VISUAL AND ART DIRECTION

Realmkeeper should aim for a grounded, detailed fantasy aesthetic.

The desired direction is:

* Believable anatomy.
* Detailed faces.
* Distinct species silhouettes.
* High-quality hair.
* Layered clothing.
* Functional armor.
* Convincing weapons.
* Natural lighting.
* Atmospheric environments.
* Detailed interiors.
* Readable strategic maps.

The game should not look like a collection of unrelated free asset packs.

16.1 Character art

Long-term character standards:

* Distinct species models.
* Male and female body configurations where appropriate.
* Multiple body types.
* Detailed faces.
* Hair and facial hair.
* Skin variation.
* Layered clothing.
* Armor that fits the body.
* Weapon attachment points.
* Consistent skeletons and rigs.
* Animation compatibility.

16.2 Environment art

Long-term environment standards:

* Modular architecture.
* Distinct regional architecture.
* Detailed settlements.
* Interiors with functional layouts.
* Natural terrain.
* Vegetation.
* Lighting.
* Weather.
* Environmental storytelling.

16.3 Asset acquisition

Claude Code should be allowed to research publicly accessible resources and recommend reusable assets.

However:

* Verify the actual license.
* Check commercial-use permissions.
* Check attribution obligations.
* Preserve source links and asset provenance.
* Do not use ripped game assets.
* Do not assume publicly viewable means freely reusable.
* Do not purchase assets without explicit user approval.

Use asset packs strategically rather than building every model from scratch.

The long-term objective is a cohesive art pipeline, not simply accumulating assets.

⸻

17. USER INTERFACE

The interface should support the feeling of inhabiting a world.

Major interfaces:

* World Overview.
* Character Creation.
* Player Hub.
* Character Sheet.
* Inventory.
* Equipment.
* Skills.
* Journal.
* Quest Log.
* Map.
* Faction information.
* NPC information.
* Shops.
* Dialogue.
* Party Management.
* Combat.
* Settings.

The Player Hub should become the central personal management interface.

It should eventually contain:

* Character.
* Appearance.
* Attributes.
* Skills.
* Inventory.
* Equipment.
* Weapons.
* Clothing and armor.
* Currency.
* Journal.
* Quests.
* Party.
* Information.

Do not cram everything into one screen.

Use clear navigation and context-sensitive panels.

⸻

18. DEVELOPMENT ROADMAP

This roadmap describes the intended order of development, not permission to implement all phases at once.

Phase	System	Purpose
0–1	Foundation	World architecture, simulation, map, persistence
2	Civilization	Economy, resources, production, trade
3	Factions	Politics, influence, relationships, territory
4	NPC Intelligence	Persistent people, relationships, schedules, memories
5	Living World & Player Hub	Time, movement, player inventory, equipment, shops
6	Character Creation	Species, classes, backgrounds, abilities, skills
7	RPG Progression	XP, levels, subclasses, class features
8	Combat	Initiative, tactical encounters, attacks, damage
9	Magic	Spellcasting, resources, spell effects
10	Loot & Equipment	Magical items, rarity, rewards, item progression
11	Companions & Party	Recruitment, party management, companion behavior
12	Quest Architecture	Objectives, journal, quest state, rewards
13	Main Campaign	Authored story, major characters, branching outcomes
14	World Content	Dungeons, enemies, regions, locations, encounters
15	Visual Production	Character models, animations, environment and asset integration
16	Polish & Expansion	Performance, UX, balance, content, release preparation

The roadmap may be adjusted when implementation reveals a better dependency order.

Do not skip foundational dependencies just to reach visible features.

⸻

19. DESIGN PRINCIPLES THAT MUST NOT BE VIOLATED

1. The world is persistent and authoritative.
2. The protagonist is custom-created.
3. The protagonist is not automatically a chosen one.
4. The player can follow the main campaign or pursue independent goals.
5. Solo and party play are both supported.
6. Combat is hybrid, combining real-time and tactical encounters.
7. Rules are D&D-inspired but adapted to Realmkeeper.
8. Character progression must have real mechanical effects.
9. NPCs are persistent people, not disposable dialogue generators.
10. Factions have independent goals and relationships.
11. The economy must connect to the world simulation.
12. Quests must change actual world state when appropriate.
13. AI must never be required for the game to function.
14. Data-driven systems are preferred over hardcoded content.
15. Existing systems must be extended rather than duplicated.
16. Every significant feature must have tests.
17. Visual placeholders must be labeled honestly.
18. Assets must have verified licensing.
19. Do not introduce major systems without architectural justification.
20. Implement in controlled phases and stop for review after each one.

⸻

20. CANONICAL DECISIONS VS. OPEN DESIGN QUESTIONS

The following decisions are confirmed:

Topic	Decision
Combat	Hybrid
Campaign	Authored main campaign and emergent sandbox
Protagonist	Fully custom
Rules	Hybrid D&D-inspired
Party	Solo and companions
Exploration	First-person and third-person
World	Persistent, simulated, interconnected
Visual direction	Stylized-realistic strategic map, detailed local environments

The following are not yet locked:

* Final world name and geography.
* Final history and creation mythology.
* Major kingdoms and their names.
* Main antagonist and central conflict.
* Exact main-story inciting incident.
* Final protagonist starting circumstances.
* Definitive opening settlement.
* Main campaign’s final ending.
* Exact companion roster.
* Final spell list and balance.
* Exact XP curve and level progression.
* Final art asset selection.

Claude must not silently make these decisions canonical.

It may propose options and prototypes, but must identify them as proposals pending approval.

⸻

21. INSTRUCTIONS TO CLAUDE CODE

This document is the design authority for Realmkeeper.

For every future phase:

1. Read this Bible before implementation.
2. Read the current roadmap and relevant architecture documents.
3. Respect all confirmed decisions.
4. Identify conflicts between the Bible and existing code.
5. Do not silently rewrite established systems.
6. Propose changes when a genuine conflict exists.
7. Distinguish implemented functionality from planned functionality.
8. Distinguish placeholders from production-ready content.
9. Preserve existing tests and save compatibility.
10. Update this Bible when an approved design decision changes.
11. Keep the roadmap accurate.
12. Work only on the requested phase.
13. Stop when the requested phase is complete.
14. Do not push Git changes unless explicitly instructed.

The Bible defines what Realmkeeper is intended to become.

The current phase prompt defines what Claude is authorized to build now.

Do not confuse the long-term vision with permission to implement every system immediately.
```
