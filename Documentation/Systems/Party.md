# Companions and the party (Phase 15)

Decisions: ADR-0073 (companions are the world's own people), ADR-0074 (loyalty from real events), ADR-0075 (the party in combat; the companion AI layer).

## Movement: exploration is free, combat is tactical

Exploration movement is the real-time `PlayerCharacter` controller. It has no grid, no movement budget, and no slowdown from weight or hunger: since the art addendum, `SpeedMultiplier` is always 1 in exploration. Squares, feet and Dash exist only inside `CombatEngine`. Companions follow the same rule: they walk, jog and run freely in a settlement, and in a fight they move square by square through the engine.

## Recruiting

| Step | What happens |
|---|---|
| Ask | In conversation, "Travel with me" (permanent) or "Come with me for a few days" (3 days). The same rules as `RecruitCompanionCommand`. |
| Refusal | `RecruitRules.Problem` returns the first cause in words, and the person says it; nothing changes. Causes, in order: dead or not alive; no character; in a fight; already with you; under 16; being escorted; posted elsewhere; not here; left over a grievance not yet mended (loyalty below 35); Familiarity below 15; Hostility 30+ or Affinity below 0; Trust below 20; knows an unsettled crime of yours; their faction's standing with you −20 or lower; and for a permanent arrangement only, a post (elder, merchant, innkeeper, smith, guard), Courage below 25, or an active request of their own still open. Last, the party is full. |
| Joining | A sheet if they have none (`NpcCharacterGenesis.Make`, their own archetype), the companion record (loyalty starts at 40 plus a third of their liking, up to +30), their gear turned into real items once, a place in the party (active if there is room, else reserve), a memory, and a chronicle line. |
| Parting | "It's time we parted ways", or Part ways in the Party section. They go home with everything they carry and resume their life; their record stays. |
| Leaving on their own | Loyalty below 15 (after the fight, never during it), a temporary term ended, or death. |

## The party

- **Active (3) and reserve (3).** Active members travel and fight. Reserve members live their ordinary lives at home until called up.
- **Follow and wait.** A waiting member stays in the settlement, or the place and room, where they were told. Follow again works only where they wait. Regroup gathers everyone waiting where the player stands and says where the others are.
- **Formation.** Order 0, 1, 2… with no gaps, and an optional leader (presentation only; the player always leads the party).

## Following in the world

- **Settlements.** Each active member with the player has one body (`CompanionFollower`): a `CharacterController` with gravity, keeping a slot behind the player that depends on their order. They walk, jog or run to keep up and step aside from each other and the player.
  - **Recovery** happens only when they are more than 35 m away, or have tried to move for 3 s without moving (held by a wall). It places them on clear ground behind the player and is counted and logged. Moving more slowly than the player is not being stuck.
  - **No double bodies.** A member's resident body is removed before their follower body appears, and comes back where they stood when they leave the party.
- **Places.** The place scene draws followers beside the player and a waiting member in the room where they were left.
- **Where a waiting member stands (Phase 16).** Told to wait in a settlement, a member keeps the spot their body stood on (`waitPose`, saved). They are drawn there when the player comes back, not beside the player as in Phase 15. Regroup clears it.
- **On the roads (Phase 16).** Active members with the player travel with them on journeys (see `Systems/Travel.md`): the atlas shows "You + n", their numbers lower the chance that outlaws bar the road, they join road fights, and they arrive and walk with the player in the next town. Reserves stay at home.
- **Fights.** The fight draws everyone; follower bodies are hidden meanwhile.

## Items

A companion owns `Inventory` and `Equipment` like the player. Commands:

- `TransferItemCommand(from, to, stack, quantity)`. An empty ID means the player. The instance moves with its state and provenance. Equipped and bound items cannot be handed over.
- `CompanionEquipCommand`, using the player's slot rules: rings, two-handed weapons free the off hand, and an off-hand item displaces a two-handed weapon.
- `CompanionUnequipCommand`. A curse refuses.

The rules engine reads their equipment for AC and attacks (`CharacterRules.LoadoutOf(companion)`). The validator checks one holder per instance across everyone.

## Fights

See ADR-0075. Active members with the player join every fight on the player's side. Their sheets are used live, so wounds, slots and conditions persist, and they gain experience by the existing keyed awards. Their decisions come from `CombatAi.ForCompanion`, with a reason in the log (`Reason` entries; the combat HUD shows them in green italics).

## Loyalty

See ADR-0074. Only real events move it, once per cause, with the reason recorded:

- crimes they saw, weighed by their own traits;
- their quest settled;
- being left on the ground, or saved by the player's own hands.

Changes of 5 or more are chronicled. Liking moves at half the rate.

## The personal quest

"Old Ground" (`companion_old_ground`) is offered once by a companion with loyalty 60 or more who has travelled with the player for a full day. It is bound to them and to the place near their home with the lowest ID. Objectives: go into the place with them, then talk it over. Done +25; failed −10; abandoned −15.

## Rest

The player's short or long rest applies the same rest to each member present, once each (keyed by rest and person), with the same once-a-day rule. Their nightly self-rest is skipped while they are active. On a short rest a companion spends hit dice only as far as needed.

## UI

- **HUD strip**, lower left: each member with an HP bar, state (or "waiting", "reserve") and loyalty word. Click it to open them in the Party section.
- **Hub → Party** (K, or "Party (K)" by the clock):
  - the line-up and reserve, each with Wait here or Follow me, To reserve or Call up, Move up, Lead, Talk and Part ways;
  - the selected member's sheet, loyalty and its recent reasons, personal quest, items (Equip, Take off, Take) and the player's pack (Give);
  - former companions and why they left.
- **Combat HUD:** companions are allies in the initiative strip; their reasons appear in the log.

## Not done (see the Phase 15 report)

- Direct player control of companions' turns.
- Further authored companion stories beyond the two personal quests ("Old Ground", and Phase 17's "Unfinished Business").
- Ambient banter (Phase 17 companions speak only when asked, about things that happened).
- Party travel on the strategic map shown as separate markers (the party travels with the player's marker).
- Charged items recharging for companions on a long rest (the player's items do).

## Phase 17: companions in the story (ADR-0087)

- **Reactions.** Companions travelling with the player (active and present; never reserves) react to campaign decisions by their own personality. Each choice names the traits it appeals to or offends; a companion's reaction is the sum of sign × (trait − 50) / 10, at most ±8, and below 2 is indifference. Loyalty moves once per decision, with the reason in words ("objected when you told the warden the count belongs to Ashford"), and they remember it.
- **What they say.** A companion keeps up to twelve remarks, one per cause:
  - a decision they felt strongly about;
  - falling out, or making up, with another companion;
  - arriving where their own people live;
  - a world event the player knows of at their home.

  In conversation, "Something on your mind?" has them say the oldest one, once. There is no random banter.
- **With each other.** Two companions on opposite sides of a decision grow apart (their Phase 4 relation: rivalry +12, affinity −8, cause Quarrel); on the same side, closer. From rivalry 20 the quarrel shows, and "You two need to settle this" (Persuasion, DC 12, harder with unsociable companions) eases it on success (rivalry −20, both +3 loyalty) or hardens it on failure.
- **A second story.** "Unfinished Business" is asked once by an active companion who:
  - trusts the player (loyalty 70+);
  - has their first story behind them;
  - has someone they parted badly with, living elsewhere (a rival, a friend or household, or rivalry 30+).

  Go with them to that person's town, be there when they meet, then talk it over. There are three endings, chosen there:
  - **mended:** an apology, rivalry −30 and affinity +20;
  - **paid:** 25 coin, rivalry −15;
  - **defied:** an Intimidation check, rivalry +15.

  Closing it moves loyalty through the same rule as the first story.
- **In the Party section:** each companion's second story and its state, how many things they have to say, what they last said, and how they get on with each of the others (liking, trust, rivalry).
