# ADR-0123: Release save slots, the save summary, a checksum, and schema v28

**Status:** Accepted for M1.1. The new tests pass; a full-suite run of the finished code is still owed (see the M1.1 completion report). Owner hand-play outstanding. (2026-11-16)

## Context

- Through Phase 30 the front end offered many named, seeded worlds. Saves were one file per slot, written atomically (temporary file, then `File.Replace`), with no backup, no checksum and no autosave. `autosave` was only a slot name.
- Continue picked the newest file by its write time.
- The Phase 30 audit (`Systems/SaveLoad-Design.md`, risk T4) asked for:
  - three manual slots and rotating autosaves;
  - slot details a player recognises;
  - backups and recovery from a bad or interrupted save.
- The owner approved the M1–M9 roadmap and M1.1 on 2026-11-16 (`Briefs/M1.1-Release-Save-System-Brief.md`).

## Decision

### 1. Slots

| Slots | Written by | Kept |
|---|---|---|
| `slot-1`, `slot-2`, `slot-3` | The player: Save Game, or New Game's first save | Until overwritten or deleted (each confirmed) |
| `auto-1`, `auto-2`, `auto-3` | Autosave | Rotating: an empty slot first, then the one played longest ago |
| Any other name | Developer tools and saves from before M1.1 | Listed under "Other saves" in development builds only; they load and migrate as before |

- `SaveSlots` (Application) holds the names and the rotation rule.
- An autosave never becomes the active slot. If the game was loaded from an autosave, Save opens the slot chooser instead of writing into the rotation.
- **Accepted limitation:** the rotation is shared, so a second playthrough's autosaves can replace the first's. Manual slots are never touched by autosave.

### 2. Schema v28: the envelope

The file's envelope gains two blocks before the world. The writer composes the envelope itself (`SaveEnvelope`), so the world's text follows a fixed `"world": ` line byte for byte.

```
{
  "format": "realmkeeper.world",
  "schemaVersion": 28,
  "savedAtUtc": "…",
  "summary": {"worldName":…,"characterName":…,"className":…,"level":…,"locationText":…,"minuteOfDay":…,"date":…,"campaignChapter":…,"playtimeSeconds":…,"playedAtUtc":…,"slotKind":…},
  "integrity": {"algorithm":"sha256","world":"<64 hex>"},
  "world": { … }
}
```

- **The summary** is one line, so the Load Game screen reads it from the first 8 KB without parsing the world.
  - `locationText` is the place, the settlement, "The country of …", "On the road", or "Near …" on the map.
  - `slotKind` is `manual`, `auto` or `other`.
  - `campaignChapter` is the id of the open campaign's active chapter, or null.
- **The world** gains `playtimeSeconds`: seconds of active play.
  - The presentation counts it only while no menu is open and the pause menu is closed.
  - It is recorded by `RecordPlaytimeCommand` just before each save, so the world does not change every frame.

### 3. The checksum

- `integrity.world` is the SHA-256 of the world's text exactly as written.
- `JsonWorldStore` checks it before the world is parsed: on load, and when listing slots. A mismatch makes the slot **Damaged**.
- Only files declaring schema v28 or later are checked. A v28 file without an integrity block is accepted unchecked, which covers files rewritten by hand or by test tools.
- The checksum finds accidental damage, such as a cut-off write or a changed byte. It is not a lock against deliberate editing.
- `JsonWorldSerializer.Deserialize` (string in, world out) does not check it, so serializer-level code and tests are unaffected.

### 4. Reliability

| Case | What happens |
|---|---|
| Overwriting a slot | `File.Replace` keeps the replaced file as `<slot>.rkworld.json.bak`. A new slot has no backup until its second save. |
| Writing | The temporary file is read back and compared with what was meant to be written before it replaces anything. A short or failed write fails the save with "Your previous save is unchanged". The slot keeps both its save and its backup. |
| Disk full, access denied, file in use | The save fails with a plain message. The temporary file is removed. Nothing else is touched. |
| Interrupted save | At start-up `CleanStaleTemps` removes `<slot>.rkworld.json.tmp` files and logs them. Saves and backups are untouched. |
| Damaged slot with a good backup | Load Game offers "Load backup". The backup is loaded first. Only if that succeeds is it copied into the slot; the damaged file is kept beside it as `.damaged`. |
| Delete (confirmed) | Removes the save, its backup and its thumbnail. |

- File operations go through `ISaveFileSystem`, the seam the tests use to inject failures.
- **Slot states:** Ready; Will be updated (an older schema); Too new for this build; Damaged (backup available); Damaged.

### 5. Continue

- Continue loads the save with the newest `playedAtUtc` among Ready and Will-be-updated saves in the manual and autosave slots, plus older saves in development builds.
- A save without a summary falls back to its header's `savedAtUtc`, then to the file's write time.
- If the load fails, Continue says why and offers the next newest save. It never changes or deletes a file.

### 6. Autosave

`SaveService` (Presentation) writes to the rotation:
- right after a loading transition completes, still behind the loading screen;
- after a campaign decision or chapter change is recorded;
- on Exit Game and Return to Title, after their confirmation.

`AutosavePolicy` (Application) refuses an autosave:
- during a fight;
- while a conversation is open;
- during a transition that has not completed;
- while a command is applying;
- within 60 s of any save;
- when the Autosave setting (default on) is off.

A story trigger that is refused for a passing reason waits, and is written when allowed. Each autosave's time is logged. The 10–15 minute timed autosave is deferred to M1.2: a save takes about 0.6 s on the main thread at 9 regions.

### 7. Thumbnails

- `<slot>.jpg` (320 × 180) is written after each successful save, from the camera that shows the world. Menus never appear in it.
- A missing or broken thumbnail never makes a save invalid.

## Migration v27 → v28

- **Structural.** No world field is rewritten.
- `playtimeSeconds` is absent in v27 and reads as 0. The slot shows "—" until its next save writes the summary.
- **Test:** a real v27 file (`Tests/EditMode/Persistence/Fixtures/v27-world.rkworld.json.txt`, written by the pre-M1.1 build) loads, and its world re-serialises identically apart from `playtimeSeconds`. Every earlier migration is unchanged.

## Consequences

- The release menu offers Continue, New Game, Load Game, Settings, Credits and Exit Game. In a game it also offers Save Game.
- New Game always founds `NewGameDefinition` ("Realmkeeper", seed 123456789) until M1.3.
- Named, seeded worlds and Campaigns stay under "Developer" in the editor and development builds.
- Saving is refused in a fight or a conversation, with the reason. The rule is `WorldSession.GetSaveBlocker`.
- Files on disk: a slot can now have `.bak`, `.jpg` and, after a restore, `.damaged` beside it.
- **Not addressed here:**
  - saving off the main thread and compact JSON (M1.2);
  - the world definition (M1.3);
  - unbounded event history (risk T11, measured in M1.2).
