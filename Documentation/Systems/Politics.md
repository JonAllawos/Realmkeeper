# Factions and politics (Phase 3)

The political state lives in the domain: `WorldData.Factions`, `WorldData.Relations` and
`WorldData.Claims`, plus each settlement's `FactionId` (its governor) and
`AdministratorFactionId`. The UI only reads query snapshots (`FactionSummary`,
`SettlementPoliticsSummary` and others) and changes politics through typed commands.

- **Constants:** every constant is in `PoliticalRules` (Simulation).
- **Formulas:** they are in `PoliticalMath`, which is pure, integer-only and uses no randomness.
- **The simulation:** it is `PoliticalSystem`.

## Factions

Every faction type uses one shared model (`FactionData`) and differs only by data.

**Identity**
- `FactionId` is a GUID, never derived from a name or position.

**Fields**
- name, description, type, founding date, status, dissolution date;
- leader (an existing `NpcId`; empty means vacant);
- influence and its target (0..100);
- treasury (coin);
- presence per settlement (strength 1..100, aggregate members, Established or Waning);
- goals.

### Types and capabilities

| Type | Can govern | Can administer | Can claim territory |
|---|---|---|---|
| Kingdom | yes | – | yes |
| Noble house | yes | yes | yes |
| Guild | – | – | – |
| Religious organization | – | – | – |
| Military order | – | – | yes |
| Mercenary company | – | – | – |
| Criminal organization | – | – | – |

Influence weights, membership scale and dues differ by type (`PoliticalRules.Profile`). Each
type's weights add up to 100; `PoliticalRules.Validate` refuses anything else.

| Type | Members | Wealth | Holdings | Presence | Economy | Authority | Reputation | Economy follows | Full membership | Max local share | Dues / 1,000 members |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Kingdom | 15 | 10 | 30 | 10 | 5 | 20 | 10 | prosperity | 50% of world | 60% | 20 |
| Noble house | 10 | 20 | 25 | 15 | 10 | 10 | 10 | prosperity | 8% | 10% | 60 |
| Guild | 30 | 20 | 0 | 15 | 25 | 0 | 10 | trade | 10% | 12% | 80 |
| Religious organization | 40 | 5 | 0 | 30 | 5 | 0 | 20 | prosperity | 30% | 35% | 15 |
| Military order | 25 | 15 | 10 | 20 | 0 | 10 | 20 | prosperity | 3% | 4% | 50 |
| Mercenary company | 25 | 35 | 0 | 15 | 15 | 0 | 10 | trade | 2% | 3% | 100 |
| Criminal organization | 20 | 30 | 0 | 25 | 25 | 0 | 0 | trade | 3% | 4% | 100 |

### Status

| Status | Rule |
|---|---|
| Active | The default. A Declining faction returns to Active at influence ≥ 25. |
| Declining | Influence below 20. |
| Dormant | Influence ≤ 5 and it governs nothing. |
| Dissolved | After 26 consecutive dormant ticks (about six months). |

When a faction dissolves:
- its presence and goals end (goals are marked `FactionDissolved`);
- its open claims are relinquished;
- its agreements end;
- settlements it governed become independent, and settlements it administered lose their administrator;
- **the faction itself stays in `WorldData.Factions`**, so every chronicle entry that names it
  remains valid.

## Influence

The influence target is Σ weight × score / 100. Each score is 0..100 and is measured from a
different quantity, so nothing is counted twice:

| Score | Measured from |
|---|---|
| Membership | members ÷ world population, against the type's "full membership" share |
| Wealth | the faction's own treasury ÷ 1,500 |
| Holdings | population governed + half the population administered, ÷ world population |
| Presence | mean presence strength over all settlements |
| Economy | mean prosperity where present, or mean trade activity ÷ 600 for trade-following types |
| Authority | mean stability of the settlements it governs or administers |
| Reputation | mean of (opinion + 100) ÷ 2 over its living relationships (50 with none) |

- **Movement:** influence moves toward the target by at most 2 points a tick.
- **Explanation:** the faction inspector shows the terms, largest first (for example "Why:
  Holdings +30 · Authority +12 …"). They come from the same function the simulation uses, and a
  test checks that they add up to the target.

### Treasury: dues and upkeep

- **Dues:** each tick, a faction collects members × dues ÷ 1,000 coin from each settlement where
  it is present. Settlements pay only from coin above a reserve of 30 days of their own upkeep.
  The coin is moved (and recorded in the settlement's ledger as exported), never created.
- **Upkeep:** 3% of the treasury is spent each tick.
- **Economic link:** hard-up settlements fund no one; prosperous ones fund their factions.

## Membership and presence

Membership is aggregate: there is one count per faction per settlement, never an NPC per member.

**Presence target**

30 + influence / 2, plus:
- +25 where the faction governs, or +15 where it administers;
- −5 for each rival or hostile faction that is at least as strong there.

Strength moves 1 point a tick toward the target. It is Established at ≥ 25 and Waning below.

**Membership target**

population × max share × strength% × support%, where support is:
- the settlement's **stability** where the faction governs or administers (hardship erodes
  support for the authorities);
- the mean of prosperity and (50 + influence/2) for trade-following types;
- 50 + influence/2 otherwise.

**Limits**
- Members move toward the target by at most population ÷ 100 a tick.
- Every day (not only on ticks), each faction's members are kept ≤ the population, and the
  total over all factions is scaled down in proportion if the population shrank below it.
  `WorldValidator` rejects any world where either is exceeded.
- Named residents stay a separate, persistent subset of the population. Leaders are chosen
  from them; no resident is ever created for politics.

## Governance and claims

- **Governance:** a settlement's `FactionId` is its governing authority (empty means
  independent). Only kingdoms and noble houses may govern.
- **Administration:** `AdministratorFactionId` is a noble house administering it for that
  governor. It requires a governor, and is never the governor itself.
- **Presence:** this is separate from governance; any number of factions can be present.
- **Claims:** a claim never changes who governs.

| Claim status | Meaning |
|---|---|
| Unresolved | Filed and not yet answered. |
| Recognized | Accepted as legitimate (by good relations, administration, or the player). |
| Contested | Disputed: poor relations, a competing claim, or the player. |
| Relinquished | Withdrawn. Closed. |
| Fulfilled | The claimant now governs the settlement. Closed. |

**How the simulation answers a claim (only if it is still Unresolved)**
1. The claimant governs the settlement → Fulfilled.
2. Another open claim on the same settlement → Contested.
3. Ungoverned settlement and the claimant's presence ≥ 50 → Recognized.
4. The claimant is the administrator → Recognized.
5. Its relationship with the governor is Friendly or Allied → Recognized; Rival or Hostile → Contested.

A decision already made, by the simulation or by the player, stands until someone changes it.

## Relationships

There is one record per pair, stored with FactionA < FactionB. Its dimensions are:
- opinion (−100..100);
- trust (0..100);
- rivalry (0..100);
- agreement (none, trade agreement, or mutual support), with the date it started;
- the cause that last moved it.

**States** (checked in this order, so they never contradict each other):

| State | Rule |
|---|---|
| Hostile | opinion ≤ −50 |
| Allied | mutual support and opinion ≥ 40 |
| Rival | opinion ≤ −25, or rivalry ≥ 60 with opinion < 25 |
| Friendly | opinion ≥ 25 |
| Wary | opinion ≤ −10, or rivalry ≥ 30 |
| Neutral | otherwise |

**Rivalry target** (capped at 100)
- +20 per settlement shared by *competitors*: both can govern, or same type. A governor and its
  own administrator never count as competitors.
- +40 per contested claim between them.
- +15 per unresolved claim between them.

**Opinion target** — the sum of these terms, each recorded with its cause:

| Term | Value | Cause |
|---|---|---|
| Trade agreement | +20 | Agreement |
| Mutual support | +35 | Agreement |
| Non-competitors sharing prosperous settlements (prosperity ≥ 55) | +10 each, at most +20 | SharedTrade |
| Administration between them | +15 | PoliticalSupport |
| Each recognized claim between them | +10 | PoliticalSupport |
| Rivalry | −rivalry target / 2 | Competition |
| Any contested claim between them | −30 | TerritorialDispute |

**Movement**
- Opinion moves 2 a tick toward its target; rivalry moves 3.
- Trust grows 1 a tick under an agreement, and drifts toward 50 without one.
- Breaking an agreement (by command) costs 25 opinion and 30 trust.
- A change of state is chronicled once, with its cause.

**How relationships affect behaviour**
- Rival and hostile factions press each other's presence.
- Relations with the governor decide whether claims are recognized or contested.
- Relations feed every faction's reputation score.

## Goals

Goals are structured data:
- type, target settlement and/or faction, and target value;
- progress (0..100) and priority (1..5);
- status, creation date, closing date and reason;
- whether the player set it.

Progress comes only from world conditions:

| Goal | Progress | Completes |
|---|---|---|
| MaintainControl | share of governed settlements with stability ≥ 40 | never (continuous) |
| GainInfluence | influence ÷ target | at 100% |
| IncreaseMembership | members ÷ target | at 100% |
| EstablishPresence | strength in the settlement ÷ target | at 100% |
| ImproveRelationship | (opinion + 100) ÷ (target + 100) | at 100% |
| SecureClaim | 100 governs or recognized · 50 contested · 25 unresolved | at 100% |

**What goals make factions do** (on each tick):
- EstablishPresence opens a branch (strength 5) once influence ≥ 25, then adds +1 strength a tick.
- SecureClaim files one claim once local strength ≥ 30.
- ImproveRelationship adds +1 opinion a tick.

**Closing goals**
- A goal whose target faction dissolved is abandoned as TargetLost.
- So is MaintainControl when nothing is governed any more.
- A goal created already met completes on the next tick: completion is the simulation's call.

## Cadence and order

The political system runs inside the daily transaction at order 450, after the population
system (400) and before the chronicle milestones (500). A failure anywhere in the day discards
the whole day, politics included.

- **Every day:** members are clamped to the population.
- **Ticks:** they run on days whose day number is divisible by 7 (weekly), so the result does
  not depend on how days are batched. Paused playback runs no days and so no ticks.

Tick order:
1. Evaluate goals (abandon lost ones; complete achieved ones).
2. Collect dues and pay upkeep; recompute influence and step it.
3. Step presence strength.
4. Step membership; clamp; report large affiliation shifts.
5. Evaluate and step relationships; report state changes.
6. Goal-driven actions (open a presence, file a claim, improve a relationship).
7. Answer unresolved claims.
8. Update status (declining, dormant, dissolution).

**Measured** (.NET 8 Release, Windows):

| World | Measurement |
|---|---|
| Default (3 settlements, 5 factions, 10 relations) | one tick 22 µs; a full 30-day run of every system 6.3 ms; 84 KB allocated per day |
| Synthetic (30 settlements, 120 factions, 7,140 relations) | one tick 54.5 ms; 30 days of politics, with the daily copy and validation, 304 ms |

No claim is made beyond these sizes. Relation evaluation scans claims and settlements per pair,
so the cost grows with factions² × settlements.

## Commands (player interventions)

All are typed commands in the session's registry, built into every session. Each validates
every reference and fails without changing anything, and each successful one records a
chronicle event whose `source` fact is `player`. The HUD shows those events with "By your hand:".

| Command | Effect | In the HUD |
|---|---|---|
| `SetFactionRelationCommand(a, b, opinion?, agreement?)` | Set opinion and/or establish or break an agreement | ±10 opinion buttons in the faction inspector |
| `FileTerritorialClaimCommand(faction, settlement)` | File a claim (kingdoms, noble houses, military orders) | "Claim" on a present faction in the settlement inspector |
| `ResolveTerritorialClaimCommand(claim, Recognized / Contested / Relinquished)` | Answer or withdraw an open claim | "Recognize", "Contest" and "Withdraw" on each claim |
| `EstablishFactionPresenceCommand(faction, settlement, strength)` | Open a presence (members come with time) | "+ faction" buttons |
| `RemoveFactionPresenceCommand(faction, settlement)` | Withdraw (not the governor or administrator) | "Remove" |
| `SetSettlementGovernanceCommand(settlement, governor, administrator)` | Change who governs; adds their presence; fulfils the new governor's claim | "Make governor", "Set independent" |
| `CreateFactionGoalCommand` / `SetFactionGoalPriorityCommand` / `AbandonFactionGoalCommand` | Manage goals | "Abandon" per goal (creation and priority: command only) |

## Chronicle

Political events reference factions in `HistoricalEvent.FactionIds` (primary first) and
settlements in `SettlementIds`. Facts are stored in `Data`, including `source`
(`simulation` or `player`); `EventFormatter` writes the sentences.

Events are reported only on meaningful change:

| Event | Reported when |
|---|---|
| FactionInfluenceChanged | every 10 points of influence |
| AffiliationShifted | every 5 points of population share |
| RelationshipChanged | on a change of state |
| FactionStatusChanged | on a change of status |
| Claim, presence, goal, agreement and governance events | when they happen |

A simulated year of the default world produced 27 political entries (14 affiliation shifts, 4 goals completed, 4 influence milestones, 2 status changes, 1 relationship change, 1 presence established, 1 claim filed), not one per tick.

## Initial world (generation)

`PoliticalInitializer` builds on the two governing factions world generation has always made,
keeping their IDs and names. It adds new factions, leaders, relations and goals from the streams
`politics` and `politics-names`. For the default seed:

| Faction | Type | Governs | Present in | Leader |
|---|---|---|---|---|
| The Crown of <town> | Kingdom | town, village | all three | town elder |
| Free Folk of <hamlet> | Noble house | hamlet | hamlet | hamlet elder |
| House <name> | Noble house | administers the village | village, town | village elder |
| <town> Traders' Guild | Guild | – | town, village | town merchant |
| Order of <name> | Religious organization | – | all three | vacant |

**Starting agreements**
- Crown ↔ Guild: trade agreement.
- Crown ↔ House: mutual support (Allied).

**Starting goals**
- Each governor: MaintainControl.
- Crown: SecureClaim on the hamlet.
- Guild: grow membership, and EstablishPresence in the hamlet.
- Order: grow membership.
- House: GainInfluence, and ImproveRelationship with the crown.

Starting values are set at their own targets, so the first weeks are calm.

The initializer runs once: a world that already has a founded faction is left untouched, so
loading, re-entering settlements or initializing twice never duplicates anything.

## Exploration

Exploration keeps no political state. The exploration HUD shows the settlement's governing
faction from the same session query the map uses.
