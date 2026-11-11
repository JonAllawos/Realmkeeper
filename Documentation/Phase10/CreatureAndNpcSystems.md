# Creature and NPC systems (Phase 10)

Decisions: ADR-0043 (NPC records and sheets), ADR-0044 (bestiary and wildlife), ADR-0045 (reactions),
ADR-0046 (world encounters).

## 1. Named people

Every living named person has a `NpcData.Character` record:

| Part | What it holds |
|---|---|
| Archetype | `NpcArchetypeCatalog`. Sheet archetypes: town guard, guard captain (fighter, Champion), watch warden, woodsman, priest, priest elder (cleric, Life), smith. Civilian archetypes: town elder, merchant, innkeeper, farmer, labourer, townsfolk. |
| Occupation | Taken from the archetype and their workplace. |
| Gear | Body armour, shield, main and off hand, carried items and a loadout name, all from the item catalog. |
| Purse | Coin, in copper. |
| Sheet | A full `CharacterSheetData` for sheet archetypes, built through the player's pipeline (species, background, class choices, ability scores, skills, spells) at the archetype's level. |

Phase 10 also fills the posts the world's places call for:
- Bramhaven gains a guard captain, a guard and a priest at its shrine.
- Westerthorpe gains a watch warden.
- Named people go from 15 to 19; 9 of them hold sheets.

| Command | Effect |
|---|---|
| `PromoteNpcCommand(npc, class?)` | Gives a sheet to someone without one: the archetype's class, its promotion class, or the class given. Identity, home, schedule, relations, memories and gear are kept. Tier becomes Notable. Chronicle event `NpcPromoted`. Promotion happens once, and never mid-fight. |
| `AwardNpcExperienceCommand(npc, amount, key, source, reason)` | Experience once per key. |
| `AdvanceNpcCommand(npc)` | Every earned level, through the shared advancement rules. Chronicle event `NpcLevelAdvanced`. |
| `WorldSession.GetNpcCharacter(npc)` | The inspector's view: archetype, class and level, hit points, armour class, abilities, saves, skills, equipment, features, spells, resources, languages and state. |

- **In combat:** a person with a sheet joins as themself, and the fight reads and writes their sheet.
- **Resting:** they take a long rest each night.
- **Inspector:** the people inspector has a Character section, and its DM tools can give a sheet or take a level.

## 2. The bestiary

`CreatureCatalog` holds the Phase 8 and 9 creatures (bandit, guard, commoner, wolf, acolyte, cult fanatic, …)
and the 25 added in Phase 10.

Each definition carries:
- **Category:** wildlife, livestock, common threat, dangerous, undead.
- **Behaviour:** skittish, passive, pack, predatory, territorial, scavenging, mindless, intelligent, each with a
  summary.
- **Ecology:** habitats, diet and social form.
- **Traits, reactions and loot.**
- **Attack riders:** a save on hit (prone, grappled with an escape DC); a save for damage instead of an attack roll;
  recharge.
- **Source:** SRD 5.2.1, SRD 5.1 or Realmkeeper original.

| Source | Creatures |
|---|---|
| SRD 5.1 | deer, boar, black bear, brown bear, dire wolf, giant rat, giant spider, goat, riding horse, draft horse, mastiff, goblin, highwayman (the SRD's Thug), bandit captain (Parry), cultist, veteran, skeleton, zombie, ogre, troll |
| Realmkeeper original | rabbit, fox, cattle, sheep, chicken (no SRD statistics exist; labelled in the catalog and the inspector) |

`CreatureCatalog.Validate()` requires:
- unique IDs and well-formed numbers;
- attacks that make sense;
- traits with their values;
- loot from catalog items.

### Traits in the engine (`CombatEngine.Creatures`)

| Trait | When | Effect |
|---|---|---|
| Charge | Attack after moving straight 20 ft or more | Extra damage, and a Strength save or the target is knocked prone. |
| Regeneration | Start of its turn | Heals its value unless fire or acid hit it since its last turn. At 0 hit points it collapses rather than dies. At the start of its turn it dies only if it cannot regenerate. |
| Undead Fortitude | Damage would drop it to 0 | Constitution save (DC 5 + damage) to stay at 1. Never against radiant damage or a critical hit. |
| Relentless | Damage would drop it to 0 | Once per fight, damage up to its limit leaves it at 1. |
| Nimble Escape | Its turn | Disengage or hide as a bonus action. |
| Recharge | Start of its turn | A d6 roll readies the attack again. |
| Parry (reaction) | It is hit by a melee attack | +2 armour class against that attack, if that makes it miss. |

### Behaviour (`CombatAi.Behaviors`)

- **Skittish and passive animals** flee: away from the nearest threat, toward the edge, where they escape. Flight
  ends their turn. Cornered, they bite.
- **Packs** close on the weakest and flank.
- **Predators** use web or reach first.
- **The mindless** press the nearest.
- **Territorial creatures** hold their ground and strike what comes close.
- **Intelligent foes** use the Phase 8 tactics.
- **Wounded creatures** fall back below their own flee threshold (`BehaviorProfile.FleeBelow`).

## 3. Wildlife and herds

`CreaturePopulationData` is an aggregate: a creature, a region, a centre and range (cells), a count, a capacity,
deaths and draws.

`FaunaGenesis.Ensure` adds what is missing:
- **Wildlife:** a population for each habitat niche in each 32-cell sector (deer and rabbits in open ground; boar,
  wolves and spiders in forest; bears in hills).
- **Road bands:** bandits with a highwayman, ranging along each road.
- **Vermin:** giant rats in each settlement.
- **Herds:** one per animal a settlement's farms keep (`HerdTargets`: cattle, sheep, goats, horses, chickens from
  agriculture and population).

`FaunaSystem` (`realmkeeper.fauna`, order 460) runs once a week:
- **Breeding:** logistic, toward capacity. Rabbits and rats breed fast; bears hardly at all.
- **Predation:** each two wolves take a deer a week in their own sector.
- **Hunting:** each settlement's hunters take game from the nearest deer, boar and rabbit populations.
- **Herds:** they move toward what the farms keep. A settlement in food shortage slaughters a tenth.

Encounters draw named `CreatureInstanceData` from a population. An instance records its fate (alive, dead, fled),
its hit points, when it was first seen and its last encounter. Each herd grazes a pasture in its settlement's plan
(`PastureKey`), and the walkable settlement shows it there.

## 4. Reactions and decisions

See ADR-0045. The rule points are:

| Point | Reactions and decisions |
|---|---|
| A hit lands | Shield, Parry, Cutting Words |
| Damage taken | Deflect Attacks, Uncanny Dodge |
| Being hurt | Hellish Rebuke |
| A spell is cast nearby | Counterspell (checked before the slot is paid) |
| A save is failed | Indomitable, Bardic Inspiration |
| An attack misses | (hook for future features) |
| Leaving reach | Opportunity attack |

- **Computer-controlled reactors** decide by policy (`AiReacts`):
  - take an opportunity attack unless skittish or passive;
  - Shield or Parry when the expected damage is a quarter of their hit points or more, or they are below half;
  - Counterspell a levelled spell;
  - Cutting Words when the margin is 4 or less;
  - Uncanny Dodge or Deflect Attacks against 4 or more damage.
- **The player** is asked through the encounter panel's prompt, and the action replays with the answer.
- **Spell status:** Shield and Hellish Rebuke are now Partial in the spell catalog (cast only as reactions);
  Counterspell is Functional.

## 5. World encounters

See ADR-0046.
- **Purposes:**
  - Outskirts: the settlement's surroundings within a few cells.
  - Road: the roads out, where the bands range.
  - Farms: the fields, where pests and predators come.
- **Types:** wildlife, predators, bandit ambush, patrol (with the road watch by day), hostile creatures, defend a
  farm, vermin.
- **Objectives:** defeat or drive off; protect a named person (a farmer at the farms); escape (when the foe is far
  beyond the party); hunt (game).
- **Budget:** groups within the moderate XP budget (75 per level); a leader only within the high budget (100 per
  level).
- **Starting one:** the HUD's "Go out: …" button cycles the purposes, or dispatch
  `StartWorldEncounterCommand(purpose)`.
- **Consequences** are applied once when the fight ends:
  - fates;
  - population deaths;
  - XP keyed `encounter:{id}`;
  - loot;
  - the protected person's memory;
  - an `EncounterResolved` chronicle event.
