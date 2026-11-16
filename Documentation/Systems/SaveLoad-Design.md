# Save, load, Continue and New Game: release design (Phase 30)

**Date:** 2026-11-16. **Status:** approved by the owner 2026-11-16. **M1.1 implemented** the menu, slots, metadata, Continue, New Game, reliability and autosave at arrivals, story moments and on leaving (schema v28, ADR-0123). Differences from this design: the checksum is optional on read (a file without one is read unchecked, so hand-edited and older saves load); New Game uses a fixed stand-in world until M1.3; there is no quicksave key. **Not yet:** the timed autosave, saving off the main thread and compact JSON (M1.2).
**Current implementation:** `Systems/Persistence.md` (schema v28), `Infrastructure/Persistence/`, `Presentation/UI/TitleScreen.cs`.

## Answer first

**The current persistence architecture can support three manual slots and autosaves.** It already saves the whole world as one document, atomically, with versioned migrations from v1 and a refused-not-crashed load path.

What it lacks:
- slot metadata (character, level, location, playtime, thumbnail);
- autosave;
- backups and corrupted-save recovery;
- a save that does not stall the main thread (608 ms measured at 9 regions);
- a menu built around one game rather than many worlds.

## 1. Main menu

| Today (`TitleScreen`) | Release |
|---|---|
| Continue: the newest readable, compatible file by write time | **Continue**: the most recently played valid save (§4) |
| New world: name, seed, speed; a new slot per world | **New Game**: choose a slot (confirm if it is used), then create the character and start the canonical world |
| Load world: every world, with delete | **Load Game**: three manual slots, the autosaves and the quicksave, with metadata and delete |
| Campaigns: start a campaign in this world | Removed from the menu: the main campaign belongs to the canonical world (`Architecture/SingleWorld-Transition.md` §1) |
| Settings, Credits, Exit to desktop | **Settings**, Credits, **Exit Game** (kept) |

In a game, the pause menu already offers Resume. Add **Save Game** (choose a slot) and **Load Game** there.

## 2. Slots

| Slot | Written by | Kept |
|---|---|---|
| `slot-1`, `slot-2`, `slot-3` | The player (Save Game) | Until overwritten or deleted, with a confirmation to overwrite |
| `auto-1`, `auto-2`, `auto-3` | Autosave (§5), rotating | The three newest |
| `quick` | Quicksave key, if added | One |

- The existing slot rule (`SaveSlot`: `[A-Za-z0-9_-]`, 48 characters) already allows these names.
- Old developer saves (`autosave`, world-named slots) stay listed under "Other saves" in development builds only.

## 3. Metadata

A small header beside each save, written in the same atomic step, read without parsing the world:

| Field | Source today | Missing? |
|---|---|---|
| Character name, class and level | Player character sheet | Not in the header yet |
| Location (settlement, place or region) | Player location | Not in the header yet |
| In-game date and time | `CurrentSimulationDate` and minute | Date is in the header; the minute is not |
| Playtime | Nothing records it | **Missing:** a playtime counter in the save |
| Last saved (real time) | `savedAtUtc` | Present |
| Thumbnail | Nothing | **Missing:** a small JPEG captured at save (the Phase 29 capture code shows how) |
| Save and world-definition versions; compatibility | `schemaVersion`; `Compatible` | Present; add the world-definition version |
| Campaign chapter | Campaign instance | Not in the header yet |

Adding playtime and the header fields is a schema change (v28) with a structural migration. It must be designed in its own phase, not here.

## 4. Continue and New Game

- **Continue** loads the save with the newest *played-at* stamp (written into the header, not the file's write time, which copying can change), among saves that are readable, compatible and whose body loads.
- If that load fails, Continue says so and offers the next newest. It never deletes or overwrites anything.
- **New Game** never touches existing saves until the player saves. Its first autosave goes to the autosave rotation, never into a manual slot.

## 5. Autosave

| When | Why |
|---|---|
| On arriving in a settlement, a place or a new region (after the loading transition) | Natural pause; the screen is already hidden |
| After a campaign milestone or a major decision is recorded | Protects story progress |
| Every 10–15 minutes of play, at the next safe moment | Guards long sessions |
| On Exit Game and Return to Title (with confirmation) | No lost progress on quit |

**Never during a fight, a conversation, a transition, or while a command is applying.** No autosave while the player is falling or a trap is resolving.

**Manual save restrictions:** not during a fight or a conversation. Allowed everywhere else, including in places, since their state is saved.

## 6. Reliability

| Requirement | Today | Release design |
|---|---|---|
| Atomic save | Yes: `.tmp`, then `File.Replace` or `File.Move` | Keep |
| Backup | None | Keep the previous file as `<slot>.bak` on each replace (`File.Replace` supports this) |
| Interrupted save | The `.tmp` is left; the old save survives | On start, delete a stale `.tmp` and report nothing lost |
| Corrupted save | The header regex marks it unreadable; a body that fails to parse is refused with errors | Also verify a checksum in the header; offer the `.bak` when the body fails |
| Version migration | v1→v27 chain, each step tested | Keep; refuse saves from a newer build in plain words (already done) |
| Compatibility across world-definition versions | Not applicable today | A world-definition version in the header, with explicit migrations |
| Performance | Serialize 608 ms, deserialize 1.6 s at 9 regions, main thread | Compact JSON from a snapshot on a worker thread; only the file swap on the main thread |
| Storage growth | 6.5–8.9 MB per 9-region save | Compact JSON (about half); bound the event history. Measure growth over a long session (not yet measured) |
| Size limit | 32 MB refused on load | Keep a limit, set from the target world size |

## 7. What the save preserves (audit)

| State | Saved? | Note |
|---|---|---|
| Character, progression, pending level-up | Yes (v6–v24) | |
| Inventory, equipment, containers | Yes | No "drop on the ground" exists, so nothing is lost there |
| Quests, decisions, journal | Yes | Old quests are pruned (`QuestWorld.Prune`) |
| Campaign progress | Yes (`Campaigns`) | |
| Named people: identity, home, work, relations, character sheets | Yes | Positions are derived from their schedule and the clock, by design |
| Unnamed townsfolk | No, derived | By design (ADR-0119): drawn from the saved population |
| Settlement population, economy, structures, plans | Yes | Plans carry a generator version (ADR-0121) |
| Factions, politics, conflicts | Yes | |
| Shops, ownership of goods, supply records | Yes | Property ownership (R3) does not exist yet |
| Discovered places, landmarks, regions | Yes | |
| World events and history | Yes | History not bounded: measure |
| Weather and ground conditions | Yes (v23) | |
| Places: rooms, doors, traps, puzzles, inhabitants, loot | Yes | Furnishing is derived scenery (ADR-0108) |
| Dead, captured, recruited people | Yes | |
| Crimes, bounties, witnesses, notoriety | Yes | |
| An active fight | Yes (`ActiveEncounter`) | |
| A conversation in progress | No | Closes on load; acceptable |
| Geography | **No, regenerated** | The main risk: `Architecture/SingleWorld-Transition.md` §2 |
| Overland region cache, streamed chunks | No, derived | By design (ADR-0122) |

**Systems that could fail after loading:** none found that depend on transient scene state. The exposure is regenerated **geography** (and with it overland passability and the player's overland pose) if generator output changes between builds.

## 8. Tests the design needs

- Save and load each slot; overwrite with confirmation; delete.
- Continue across all slot kinds, with the newest corrupt, incompatible or half-written.
- Kill the process mid-write: the old save and the `.bak` survive.
- Every migration from the oldest supported version to the newest, with real saved files.
- New Game with three full slots never alters them.
- Autosave never fires during a fight, conversation or transition.
- Long session: storage growth and load time after many in-game months.
