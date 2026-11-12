# NPCs: the NPC Intelligence Engine (Phase 4)

Named people are simulated individuals with an identity, a personality, skills, needs, goals,
memories, knowledge, relationships, a home, a workplace and a weekly routine. Everything about
them lives in `NpcData` (and two world lists: `Locations`, `NpcRelations`): there is no second
NPC store, and their IDs never change (faction leaders are the same `NpcId`s as before).

## Who is simulated, and how closely (tiers)

| Tier | Who | How often | Stored as |
|---|---|---|---|
| 1 — notable | Faction leaders, elders, innkeepers (and anyone who succeeds a leader) | Every day | `NpcData` |
| 2 — resident | Every other named person | Once a week, on a weekday fixed by their ID | `NpcData` |
| 3 — population | Everyone else in the settlement | In aggregate, by the economy | `SettlementData.Population` minus living named people |

**No double counting.**
- **Named people count in the population.** They are members of it, never added to it, and the
  validator rejects more living named people than people.
- **When the population falls below them**, through starvation or emigration, `NpcSystem` lets
  the least notable named residents die, in a stable order by ID. Each death is a chronicle
  entry, and those close to the dead remember it.
- **A faction is never led by the dead:** the most notable living member succeeds.
- **The DM moving someone to another settlement** moves one member of the population with them.

## A person

| Part | Contents | Bounds |
|---|---|---|
| Identity | Name, sex, birth date (age), occupation (role), tier, home settlement, faction, met the player | Name 1–64 characters |
| Personality | Sociability, courage, ambition, compassion, discipline, curiosity, trustfulness, greed | 0–100, fixed unless the DM edits |
| Skills | Farming, crafting, trading, combat, leadership, lore, hospitality, labour | 0–100. The role's skill sets the wage. A finished learning goal adds 5. |
| Condition | Health, fed, energy, safety, morale; savings in coin | 0–100; coin ≥ 0 |
| Goals | Earn a living, secure food, recover, rise in standing, make friends, serve their faction, grow rich, learn a skill, find safety | Up to 4 ambitions; urgent needs are never crowded out |
| Whereabouts | Residence, workplace, current location, a 7-day routine | Locations in their settlement |
| Memories | Event link, date, category, subject, people, salience, confidence, source (experienced / witnessed / learned) | At most 24; fade 6 a week; forgotten below 8 |
| Knowledge | Topic, subject, value, status (known / believed / rumour), confidence, learned on, source person, tellings away | At most 40 |

## Locations and routines

Each settlement has persistent locations on its layout sites (`SettlementSites`):
- the square and the market (plaza), the tavern (hall) and the granary;
- a keep and a gatehouse (town), a chapel (village) or a watchtower (hamlet);
- a shop, a smithy and a shrine, depending on type;
- residences in the remaining houses.

When a settlement has more named people than houses, they share homes as households.

**Routine:** each person works at their workplace on six days a week and spends a rest day at
the tavern (if sociable), a place of worship, or home.

**Overrides:** an urgent need (priority 60 or more) sends them elsewhere.
- Ill: home.
- Hungry: the market.
- Unsafe: the keep or chapel.

**Exploration** places each person present today at the site of their current location: inside
the tavern, among the stalls, by the gate, or at a building's door. Someone moved to another
settlement is not shown here.

## The daily system (`realmkeeper.npcs`, order 425)

It runs inside the day's transaction, between population (400) and politics (450):
1. **Reconcile** named people with the population (above).
2. **Update people who are due** (tier 1 daily, tier 2 weekly):
   - **condition:** food follows their settlement's granary, health follows food, energy follows
     work and rest, safety follows stability, wages and living costs;
   - **goals:** opened by needs and personality, progress, achieved, with a 30-day cooldown;
   - **movement;**
   - **observation:** they refresh what they see of the settlement they are in.
3. **Conversations.**
   - **Where they happen:** people at the same location talk, a few pairs per place, and
     neighbours meet in the evening (one pair per six residents).
   - **Who agrees to talk:** willingness depends on sociability.
   - **What talking changes:**
     - a tie may form, with "conversation" as its cause;
     - affinity moves with how compatible their personalities are;
     - each learns where the other can be found;
     - each may pass on one piece of news as hearsay (see Knowledge).
4. **Reactions to today's notable events** (shortages, deliveries, political changes):
   - **Who reacts:** people of the affected settlement or faction.
   - **What they get:** a memory linked to the event (experienced, witnessed or learned), first-hand
     or believed knowledge of it, and a change in morale.
5. **Weekly:**
   - memories fade and the faintest are forgotten;
   - first-hand knowledge older than 14 days becomes a belief;
   - rumours older than 45 days are forgotten;
   - ties not kept up drift toward neutral.

**Determinism:** every choice hashes (world seed, date, person, purpose) with `SeedMixer`. No
shared random stream is consumed, and the result does not depend on iteration order.

**Chronicle:** the engine records deaths, notable people's ambitions set and achieved, and ties
becoming friendship or rivalry. Routine needs and moves are not recorded. A test keeps NPC entries
under one a day.

## Knowledge: what people hold, not what is true

The world's truth is `WorldData`. A person holds:
- **Known:** seen first-hand, current.
- **Believed:** seen but possibly out of date, or heard from someone they trust.
- **Rumour:** heard second-hand.

Rules:
- **Observation always wins**, and refreshes knowledge.
- **Hearsay never overrides** what they currently know first-hand.
- **Facts weaken as they travel:** each telling costs 20 confidence (adjusted by trust and
  trustfulness), and a fact travels at most 3 tellings from its observation.
- **What gets passed on:** people pass on the most interesting thing the listener does not
  already hold. News of places, factions and events comes before who is where.
- **The inspector marks stale knowledge** with "✗ no longer true", next to the current truth, so
  the DM can see the difference.

## Talking to people

`AskNpcCommand(npc, topic)` is only possible where the person is today. The topics are about
themselves, this place, who governs, their faction's leader, news and rumours, plus a named
person.
- **Source of answers:** the person's own knowledge and memories (`NpcDialogue`). Hedges such as
  "last I knew" or "I've heard" reflect the status.
- **Unknown facts:** they say so.
- **Out-of-date beliefs:** they repeat them.
- **Memory:** the person remembers the conversation. The first meeting is chronicled.

## The DM's commands

All are atomic (validated, applied to a copy, validated whole), carry a source (`dm` or
`player`), and are chronicled. A rejected command changes nothing.

| Command | Effect |
|---|---|
| `EditNpcIdentityCommand` | Name, sex, birth date |
| `SetNpcOccupationCommand` | Role and workplace; the routine's work days follow |
| `SetNpcScheduleCommand` | Seven locations, one per weekday (in their settlement) |
| `SetNpcGoalCommand` / `AbandonNpcGoalCommand` | Give or reprioritise a goal; abandon one |
| `SetNpcRelationshipCommand` | Affinity, trust, respect, fear, rivalry (creates the tie with cause "manual") |
| `AddNpcMemoryCommand` | A memory, optionally linked to a chronicle event and people |
| `AddNpcKnowledgeCommand` | A piece of knowledge, with status and confidence |
| `MoveNpcCommand` | Where they are today; to another settlement, a relocation that conserves people |
| `ChangeNpcAffiliationCommand` | Their faction (a leader must be replaced first) |

## In the HUD

- **People** (left navigation): every named person, filtered by settlement, role, tier and name.
- **NPC inspector:** ten sections (identity, personality, skills, condition, goals, whereabouts,
  relationships, memories, knowledge, history), each with an explanation of where its values
  come from. **Manage** opens the DM's tools.
- **Settlement inspector → Residents:** the named people living there (click to inspect), and how
  many more the settlement holds in aggregate.
- **Exploration:** pressing E on a resident greets them and offers topics. Click one, or press
  1–6.

## Performance

Measured by `NpcPerformanceTests`: .NET 8 Release, i9-14900HX, 60 simulated days, all systems
running.

| World | ms per day (with / without the engine) | Save | Load | Save size |
|---|---|---|---|---|
| 3 settlements, 15 people | 0.58 / 0.23 | 49 ms | 54 ms | 206 KB |
| 3 settlements, ~100 people | 0.82 / 0.95 (noise) | 40 ms | 126 ms | 1.9 MB |
| 10 settlements, ~300 people | 10.4 / 1.7 | 133 ms | 460 ms | 6.3 MB |

## Hour by hour (Phase 5)

The daily engine is unchanged. Phase 5 adds an hourly layer on top, `NpcActivityPlan.At(world,
npc, date, minute)`. It is a pure function: nothing is stored, it is deterministic, and it is shared
by shops, dialogue, queries and exploration (ADR-0019).

**The day's main place:**
- today: the location the day committed (`CurrentLocationId`): the routine, a pressing need, or a
  DM move;
- other days: the weekly routine.

**Rhythm.** Each person wakes between 06:00 and 07:00 and sleeps between 21:30 and 23:00. Both are
hashed from their ID. Innkeepers rise an hour later and stay up past 23:00.

**Order of precedence:**
1. Unwell and resting (a strong Recover need): at home all day.
2. Night: asleep at home.
3. Another pressing need: where that need takes them (e.g. to market to find food).
4. Breakfast at home for 45 minutes after waking.
5. Work days, in working hours:
   - at the workplace, with a 30-minute midday meal around noon (guards skip it);
   - before work, at home.

   | Role | Hours |
   |---|---|
   | Shopkeeper | The shop's own hours |
   | Guard | 07–19 |
   | Innkeeper | 10–22 |
   | Farmer | 07–17 |
   | Elder | 09–16 |
   | Others | 08–17 |

6. Rest days, or a DM move: at that place until evening.
7. Evenings: at the tavern if sociable (sociability ≥ 55) or the innkeeper, otherwise at home.

**Activities:**

| Activity | When |
|---|---|
| Sleeping | Night |
| Eating | Breakfast and the midday meal |
| Working | Workshops, granary, farm |
| Trading | Shop, market |
| Guarding | Guard post |
| Sitting | Administration |
| Worshipping | Shrine |
| Socialising | Tavern |
| Resting, Standing | Elsewhere |

Each comes with a reason ("Working hours.", "Night: asleep at home.") and the time it lasts until.

**Presence.** `GetPresenceIn(settlement)` and `GetNpcPresence(id)` give where everyone is this hour.
Exploration uses these to place people, and talking to someone requires them to be here and awake
this hour. Tests check:
- the layout puts people at the sites of their hourly locations;
- shopkeepers are at the counter in shop hours;
- everyone is asleep at home at night.

**Regard for the player (Phase 5).** Each person holds a `PlayerRegard`: familiarity, affinity, and
counts of trades and talks. It rises once per day per person (`Commerce.md`). The Hub lists who
knows the player and why.

**New conversation topics (Phase 5).** Answers are bound to knowledge, from fixed templates:

| Topic | Answer |
|---|---|
| Your work | Role, workplace, hours, whether they are at it now |
| Where to buy | The local shops and their hours (residents only; visitors "couldn't tell you") |
| What's for sale | A keeper lists their own stock; others say which shop deals in what, and that you'd have to ask |
| Prices | Only a keeper names numbers; others speak from what they know of food and fortunes |
| The factions | Who holds the place as they know it, their own allegiance, and the last political news they remember |

"Where is someone" remains the Person topic.

## Character records (Phase 10)

Every living named person now carries a character record (ADR-0043). People whose work is fighting, healing
or keeping order hold a full character sheet, built by the player's rules. The others hold a lightweight record
(archetype, occupation, gear, purse) until they are promoted. Phase 10 also fills the posts the world's places
call for (a captain, a guard and a priest in the town; a warden in the hamlet): named people go from 15 to 19.

- **Commands:** `PromoteNpcCommand`, `AwardNpcExperienceCommand` (keyed), `AdvanceNpcCommand`.
- **Query:** `WorldSession.GetNpcCharacter`.
- **Inspector:** a Character section, with DM tools to give a sheet or take an earned level.
- **Upkeep:** people with sheets take a long rest each night and fight as themselves.

Details: `Documentation/Phase10/CreatureAndNpcSystems.md`.

## Phase 18: natures in the day, overheard talk (ADR-0094)

- **`NpcActivityPlan.At`** now reads personality and the town's condition. The reason is shown in words.
  - Discipline moves working hours; shopkeepers and guards are excepted.
  - Curiosity goes to the market on a free day.
  - Compassion hands out bread in a hungry town.
  - The timid stay in during unrest.
  - Prosperity sets how readily people go to the tavern.
- **Overheard exchanges** (`NpcExchanges.Now`, `WorldSession.GetExchanges`).
  - Only between people who know each other, at the same place now, and free to talk.
  - They say what they really know, worded by certainty; rivals argue.
  - The scene shows lines above pairs within 14 m and turns them to each other. Nothing in the world changes.
- **Deferred:** sitting poses, because the streets and interiors have no seats.
