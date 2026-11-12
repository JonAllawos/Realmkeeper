# Phase 14: known limitations

What Phase 14 does not do, or does only in part. Each item is stated as it is in the code at the Phase 14 commit.

## Sight and cover

- **Line of sight in places is per floor.** Nobody sees between floors (up a stairwell, down a shaft).
- **Creatures as cover.** They give half cover at most, as low obstacles do. A large creature does not give more.
- **Lighting is a yes/no.** Light decides whether a quiet act is seen (a lit room or a carried light). Dim light and darkvision do not change witnesses or cover outside fights.
- **The combat window** still shows the Phase 8 panel, now with walls, doors, low cover, cover text and faded figures out of sight. It is large and covers much of the screen. The combat overhaul (the next milestone) replaces it.

## Conditions, harm and rest

- **Sources.** Lasting conditions come from traps and hazards (poison needle, snare net, blinding dust, deep water's exhaustion). Spells and creatures do not yet leave conditions that outlast a fight, except through those already in a fight when it ends.
- **Lesser Restoration** is cast from the conditions panel at its base slot (level 2). The panel does not offer an upcast.
- **Interiors.** Rest inside any building interior is refused ("someone's house"). There is no inn room to rent yet; resting in a settlement uses the Phase 9 lodging rule outside.

## Crime

- **Bounties.** A bounty is a sum owed and a refusal to trade; nobody hunts or arrests the player. There are no guards who give chase, no jail and no fines collected by force.
- **Where crimes happen.** Witnesses are worked out inside places (building interiors and the new places). Acts in the open street of a settlement are not crimes yet.
- **Recognition** is all or nothing: the witness has met the player, or the player is notorious (30 or more). Disguise and masks do not exist.
- **Settling** goes through anyone who knows. It settles every crime that person knows of at once; it cannot settle them one at a time.
- **Night locks** apply to every building except taverns, temples and guard posts, including shops.

## Places

- **Fire** does not spread and does not tick: a burning room burns for four hours from when it was found, then stays burnt out. Torches burn only marked webbing.
- **Bridges** are created only by the generator. Nothing repairs a fallen bridge; nobody builds a new one.
- **Waking** is tied to features in the sleepers' own room. Noise in a neighbouring room does not wake them.
- **New places in old saves** are added on load for every world that lacks them (`PlaceGenesis.EnsurePhase14`). A world that already has three to five is never given more.

## People moving indoors

- **Interiors only.** Residents move room to room in building interiors. In dungeons the inhabitants keep to their rooms; there are no patrols.
- **Walking in the scene is shown after the fact.** When time passes in a large step, people appear where the schedule puts them, and the walker shows the last leg only.
- **Followers** (at most three) will not cross a damaged bridge, and wait at locked or barred doors. They do not fight in a room the player is not in.

## Tests

- **Content-dependent PlayMode flows.** Some flows depend on what the shipped scene's world contains: a household with someone at home beside something to take; a crypt room with inhabitants; which of the five new places this seed chose. They use `Assume` and are skipped, not failed, if the world lacks the content. On the shipped seed every flow ran.
- **Captures** are rendered at 1920×1080 only.
