# ADR-0116: Bestiary knowledge as player state, and notices derived from the world

**Status.** Accepted, 2026-11-14 (Phase 25). It adds save schema v27.

**Context.**
- **The bestiary.** The Phase 25 brief asks for a bestiary that "reflects actual discovered knowledge" and does not reveal unknown species. The creature catalogue (`CreatureCatalog`, Phase 10) holds every creature's rules, but nothing recorded which ones the player had met. The world keeps only the 20 most recent fights (`WorldData.MaxEndedEncounters`), so a bestiary built from them would forget.
- **Notifications.** The brief also asks for notifications of quests, discoveries, level-up eligibility, items, equipment, reputation, world events, encounters and travel, without flooding or repetition. These changes come from dozens of command handlers.

**Decision.**
1. **`PlayerCharacterData.KnownCreatures`** is a list of `KnownCreatureData` (creature id, first met, fights met, individuals defeated).
   - **Recorded by the combat engine itself.** `CombatEngine.Begin` counts each creature kind facing the player; `CombatEngine.Finish` counts each one that fell or fled.
   - **Only fights with the player in them count,** and only creatures on another side. The player's allies are not studied.
   - **Bounded** to 500 kinds, and validated: catalogued ids, no repeats, no negative counts.
2. **What the bestiary may show** (`WorldSession.GetBestiary`):
   - *Met:* name, kind, size, habitats and behaviour.
   - *Defeated at least once:* also armour, hit points, speed, challenge, attacks, defences, traits and senses, read from the catalogue.
   - Creatures never met are not listed. Fields not yet known are null, and the screen says so in words.
3. **Save schema v27.** `player.knownCreatures` is optional. A v26 or older save is seeded once on load from the fights it still holds (`CreatureKnowledge.SeedFromHistory`). An empty list stays empty, so nothing is counted twice. The migration rewrites nothing.
4. **Notices are derived, not hooked.**
   - **The snapshot.** `WorldSession.GetNoticeSnapshot` reduces the world as the player knows it to what notices compare. It uses the journal's own visibility rules, so it holds no undiscovered quest, hidden objective or unknown event.
   - **The rule.** `PlayerNotices.Compare` (pure, engine-free) turns the difference between two snapshots into notices in words.
   - **Bounds.** At most 8 notices per change; the rest are summed up in one.
   - **Not news:** a load or a new world (`WorldSession.ReplaceCount`).
   - **Who checks.** The exploration HUD checks twice a second; the map checks once a second.
   - **Command handlers are untouched.**

**Consequences.**
- One more optional save field. No gameplay rule changed: what a creature can do is the catalogue's as before; the record only decides what the player has seen.
- Notices can never claim something the game did not do, because they are read from the world after the fact.
- **The cost:** a notice can be up to half a second late, and changes that cancel out between two checks are not announced (taking an item off and putting it back on within half a second).
- **Measured:** a check takes about 0.03 ms in the played scene (Phase 25 capture log), and is held under 25 ms in a full .NET world by `Phase25InterfaceTests.NoticeSnapshot_IsCheap_ForAFullWorld`.
