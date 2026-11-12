# World events and faction activity (Phase 16)

Code: `Runtime/Simulation/Atlas/WorldEventSystem.cs` (daily, order 477), `FactionActivitySystem.cs` (order 456), `WorldEvents.cs` (raise, move, tell, learn), `Runtime/Domain/World/Atlas.cs` (`WorldEventData`). Decision: ADR-0080.

## What an event is

| Field | Meaning |
|---|---|
| Type | One of the ten kinds below. |
| State | Active → Escalating → Resolved / Failed / Expired. |
| Severity | 1 to 5. |
| Cause | In words, read from the system that caused it. |
| Scope | A settlement, a road, a region, two factions, or a group. |
| Key | A stable key. One open event per cause, so the same cause never opens two. |
| Applied | Consequences applied, by key. Each consequence happens once, across saves. |
| History | Bounded. |
| What the player knows | Known (Unknown, Rumored, Confirmed), as of when, the state and severity as they heard it, and from whom. |

Closed events are kept, up to 60. The oldest closed ones go first.

## The kinds and their causes

| Kind | Raised when | Acts on the world | Ends |
|---|---|---|---|
| Food shortage | A settlement's own shortage (Phase 2). | After a week it worsens, and some people leave (refugees). | When food returns. |
| Local unrest | Low stability. | Severity grows below 15. | When stability recovers. |
| Bandit activity | A road band holds the road (strength ≥ 1). | Patrols are sent; travellers are threatened; caravans attacked. Severity 1 + strength ÷ 3. | When the band is broken (by a patrol, caravan guards or the player). |
| Patrol response | A faction answers outlaws on its road. | A patrol group rides out, paid from the treasury. | When its outlaws are gone, or after 12 days. |
| Bridge damaged | Old spans can crack (a seeded chance a month). A cracked span left unmended falls. | A fallen bridge closes the road to travellers and trade. The larger end-settlement mends it with its own timber and stone, a day's work at a time. | When mended. |
| Road blocked | A confrontation between hostile factions. | The crossing between their lands is barred, held by the stronger side. | When their relation mends. |
| Faction conflict | Rivals (tension) or hostile (confrontation) relations, where the two share a border crossing. | Barred crossings; faction patrols. | When the relationship mends. |
| Trade disruption | A caravan halted by a closed road. | Trade on that road waits. | When the road opens. |
| Caravan lost | Outlaws take a caravan (`realmkeeper.groups`). | The goods are recorded `lostOnRoad`; a chronicle line. | Resolved at once: the survivors return to the origin. |
| Refugee movement | A shortage gone on for a week. | People leave for the nearest settlement with food: counted out now, counted in on arrival. | On arrival. |

## Faction activity (order 456)

A faction that governs one end of a road held by outlaws sends a patrol. The rules:
- **Cost:** 40 coin from its treasury, if the treasury can pay.
- **Strength:** from the faction's presence.
- **Effect:** the patrol wears down the band's population. A broken band resolves its event.

## Knowledge

- **The world:** the people of the settlements an event touches learn of it as `RecentEvent` knowledge, with a deterministic ID. Their own conversations spread it further.
- **The player** learns of an event in three ways:
  - by seeing it: what touches the place they are in or the road they walk is **Confirmed**;
  - from the people of a settlement they arrive in;
  - by asking about *News* or *Rumours* in conversation.

  What they hear is **Rumored**, as of when they heard it.
- **The atlas** shows only events the player knows of, as they know them. News the player can see has changed is marked out of date (`~`).

## Quests from events

Events offer quests once, through the one quest engine (source key `event:{id}`):

| Quest | Offered when | Asks for | Holds while |
|---|---|---|---|
| `RoadOutlaws` | Outlaws at severity 2+ | Break the band (defeat three of its creatures), then report. | The event is open, or the player did it. It lapses if others break the band first. |
| `BridgeTimber` | The bridge's town cannot mend it for two days | Three timber planks. | The bridge is down. |

The Phase 12 food-shortage quest is linked to the shortage event, not duplicated.

## Chronicle

Every change of state of an event is a chronicle line (`WorldEventChanged`). The exception is a lost caravan, which writes its own `CaravanLost` line.
