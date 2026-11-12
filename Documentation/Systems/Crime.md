# Crime, witnesses and local consequences (Phase 14)

Decision record: ADR-0069. Code:
- `Domain/World/Crime.cs`
- `Application/Places/CrimeRules.cs`
- `Application/Places/PlaceActions.cs` (stealing)
- `Exploration/Application/PlaceEntryHandlers.cs` (breaking in)
- `Application/Places/PlaceCommands.cs` (assault)
- `Simulation/Npcs/NpcSystem.cs` (discovery)
- `Application/Quests/DialogueEngine.cs` (settling)

## The record

A crime is a record in `WorldData.Crimes`. It holds:
- the **kind**: theft, shop theft, breaking in, trespass, assault, murder or property damage;
- the minute, the settlement, place and room, and the victim;
- what was taken, in items and coin;
- the **bounty**;
- the **witnesses**: each person, whether they recognised the player, the description they give, and how they saw (in the same room, through the open way, or woken by the noise).

Its **status** is one of:
- **Undiscovered:** nobody knows it happened.
- **Discovered:** found out, but not who did it.
- **Witnessed:** someone saw it.
- **Settled:** put right.

## Who knows

`CrimeRules.Witnesses` finds who perceived the act. A person sees it only if all of these hold:
- they are alive and on the same floor, within 60 ft (12 squares);
- they are in the same room, or a room in sight through open ways;
- `GridSight.CanSee` finds a line between them through the place's walls (closed doors block);
- there is light, either a lit room or a carried lantern (a loud act needs none);
- they are awake, unless the act is loud.

Stealing quietly lets the player's Sleight of Hand total beat each witness's passive Perception. That passive score is 10 plus their Wisdom modifier, plus watchfulness for a guard.

A witness **recognises** the player if they have met, or if the player's notoriety is 30 or more. Anyone else gives only a description: the player's kind and armour.

**Each witness:**
- remembers the deed;
- learns it as knowledge: a deed by the player if they recognised them, otherwise a recent event by "a stranger in mail".

That knowledge travels only through the Phase 4 knowledge exchange, between people who meet, so other towns do not know. An unseen theft, break-in or damage is found out at the next day's daily step (`NpcSystem.DiscoverCrimes`): it becomes Discovered with no culprit named, and the victim remembers being robbed or broken in on.

**Only if someone recognised the player:**
- a **bounty** is set (theft: the value taken, at least 10; assault 25; murder 100; trespass 5; otherwise 10);
- reputation is lost once with the settlement, the victim and the victim's faction;
- notoriety rises.

Each change is keyed by the crime, so nothing is applied twice.

## What follows

- A merchant who knows of an unsettled crime by the player in their own settlement, or against them, will not trade.
- Anyone who knows has a dialogue line about it and a **settle** response. Settling:
  - gives back the goods the player still has;
  - pays the rest in coin (the bounty, less the value returned, never below half);
  - clears the bounty and marks the crimes settled.

  Memories stay: people do not forget. If the player cannot pay, nothing changes.
- The journal's **Crimes** tab lists each crime the player knows of: what it was, its status, who saw it and what it means now.

## Doors at night, households and assault

- **Homes are locked from 22:00 to 06:00.** Taverns, temples and guard posts are not. The door offers to pick the lock (thieves' tools, Sleight of Hand) or force it (Athletics, loud), or to leave it. Picking it is recorded as breaking in; forcing it is recorded as damage to property.
- **A household** warns a stranger in a private room once, then shows them out. Coming back is trespass.
- **Attacking a person** records an assault before the first blow. A death makes it murder.
