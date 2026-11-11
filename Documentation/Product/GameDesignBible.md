# Realmkeeper Game Design Bible

This file holds the **Game Design Bible as supplied by the project owner** with the Phase 6 brief
(2026-11-10). The Bible's own text follows the status section, unchanged. Only the status section
below is maintained by implementation phases (Bible §21, items 7, 8 and 10).

> **Relationship to `ProductFoundation.md`.** The binding product foundation describes "a persistent
> fantasy world simulation and exploration platform". The Bible extends this into a full RPG
> (combat, magic, companions, campaign). The two agree on persistence, determinism, local play and
> optional AI. Reconciling them, for example with a decision record that supersedes parts of the
> foundation, is an **owner decision** and has not been made. Each phase builds only what its own
> brief authorises.

## Implementation status (updated at the end of Phase 10, 2026-11-11)

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
| 9 | Spellcasting, class features and combat integration | **Implemented, awaiting review.** See below. |
| 10–16 | Onwards | Not started |

### §7 Character and RPG system

| Area | Status | Notes |
|---|---|---|
| Six abilities, modifiers | Implemented | Derived; increases from species, feats, Epic Boons and capstones, each with its cap |
| Standard array, point buy | Implemented | No manual entry or rolling |
| 18 skills, proficiency, expertise | Implemented | Expertise from levels too; Jack of All Trades; Reliable Talent shown |
| Six saving throws | Implemented | Save proficiencies and aura bonuses from levels; roll modes |
| 12 species | Implemented as rules; **Placeholder** art | Unchanged from Phase 6 |
| 12 backgrounds | Implemented | Social features are Foundation |
| 12 classes | **Implemented, levels 1–20** | SRD 5.2.1 features, scaling, resources, slots; distinct and validated |
| Subclasses | Implemented (one per class) | The SRD 5.2.1 subclass; further subclasses Not started |
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
| NPC and companion progression | **Implemented** for NPCs (Phase 10); companions Not started | Every named person has a record; fighters, healers and wardens hold full sheets by the player's rules; promotion, keyed experience and advancement (ADR-0043) |

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
- **20 (controlled phases):** Phases 7, 8 and 9 stopped for review; Phase 10 stops here for review.
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
