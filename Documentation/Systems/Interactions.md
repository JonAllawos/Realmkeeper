# Interactions, traps and hazards in places

Phase 13. Decision: ADR-0064. Code: `PlaceActions`, `PlaceRules`, `PlaceCommandHandlers`, `FeatureCatalog`, `TrapCatalog`, `HazardCatalog`, `PlaceInteractable`.

## Targets and actions

| Target | Actions (when the state allows) |
|---|---|
| Room (the R key) | Search: one Investigation roll against everything hidden here, at disadvantage in the dark |
| Door, or a secret way once found | Open, close, unlock (key), lock (key), pick (thieves' tools, Sleight of Hand), force (Athletics vs DC + 2; breaks the door and alerts those near), listen (Perception DC 10) |
| Barred door | Lift the bar from its own side; force from the other |
| Guarded door | Refused while the guards hold it; the reason says to talk to them |
| Container | Open: it opens in the Player Hub's Storage, then Take or Take all. Refused when it belongs to a household or a standing garrison. |
| Document | Read: refused in the dark without a light. Reveals its text and records a discovery. |
| Clue, altar, furniture, workstation | Inspect: a check where the object has more to tell, a discovery where it matters |
| Trap, once found | Inspect; disarm (its skill and tool; failing by the trap's margin, or rolling a 1, sets it off; salvage on success) |
| Mechanism | Activate: opens what it is chained to, and wakes what sleeps on it |
| Captive | Free: Athletics DC 10, refused with enemies in the room. They then follow. |
| Inhabitant group | Attack: guards cost −15 faction standing and +5 notoriety |

- Every option carries the reason when it cannot be taken now. The interaction prompt shows it, and the choice list shows checks by skill.
- The command re-checks everything.
- **Retries.** Three tries per target, action and visit, each a fresh roll. Then another visit, another approach or a light changes things. Only the attempt record changes on a failure.

## Checks

- Skill checks and saves use `CharacterRules`: skill bonuses, save bonuses and darkvision.
- The world's dice are keyed by place, target, visit and attempt, so they are deterministic and replay identically.
- A natural 20 succeeds and a natural 1 fails.

## Darkness and light

A room with the darkness hazard is dark unless it is lit, the player carries an item tagged `light` (the hooded lantern) or the player has darkvision. In the dark:

- Investigation is at disadvantage;
- passive Perception is −5;
- documents cannot be read.

## Traps (`TrapCatalog`)

| Trap | Detect | Disarm | Effect | Reset |
|---|---|---|---|---|
| Dart plate | 13 | Sleight of Hand 13, tools | DEX 13, 2d4 piercing | Rearms (salvage: arrows) |
| Tripwire and bells | 11 | Sleight of Hand 10 | Alarm (inhabitants alerted) | Once |
| Loose roof | 12 | Athletics 12 | DEX 12, 2d6 bludgeoning | Once |
| Spiked pit | 12 | Athletics 11 | DEX 12, 2d6 piercing | Rearms |
| Poisoned needle | 14 | Sleight of Hand 14, tools | CON 13, 1d4 poison, Poisoned | Rearms |
| Warding glyph | 14 | Arcana 14 | DEX 13, 3d4 fire, alarm | Once |

- Stepping on an armed trap's cell springs it. A found trap is drawn and can be stepped around; a hidden one is not drawn.
- A spent trap does not spring again the same visit. A rearming one springs again on a later visit.
- **Fairness:** detection DCs are 11–14, dice never exceed 16 points, damage is halved on a successful save, and outside a fight it never takes the last hit point. The catalog's validator enforces the first two.

## Hazards (`HazardCatalog`)

Each is met once per visit to a room:

| Hazard | Check | Effect |
|---|---|---|
| Foul air | CON 11 | 1d6 poison, Poisoned |
| Unstable ground | DEX 10 | 1d6 bludgeoning |
| Deep water | Athletics 10 | 1d4 on a failure |
| Fire | DEX 12 | 1d10 fire |
| Darkness | — | See above |

Each has a reading (inspect with a skill) that tells how to get through.

**Limitation:** conditions such as Poisoned are reported, not kept on the sheet. There are no lasting conditions outside fights yet.

## Fights in places

`PlaceEncounters.Start` uses the one combat engine:

- **Participants:** the player; followers and anyone held in the room on the player's side (Protect); the group's members.
- **Members:** creatures are lasting individuals (`{place}:{group}#n`) that keep their wounds; people are themselves.
- **Walls:** every non-floor cell around the room, and every closed door, is a blocked square. Movement into one is refused. The AI routes around walls breadth-first, and a cornered creature that cannot get further away turns and fights.
- **Aftermath:**
  - groups won against are Defeated for good, and what they guarded opens;
  - otherwise they are Survived, still wounded;
  - the loot pile lies in the room (a physical sack: look through it to open it in Storage);
  - a protected person who lived emits `Protected`, and one who died makes the escort Lost.

Line of sight through walls is not modelled: walls stop movement, not missiles.
