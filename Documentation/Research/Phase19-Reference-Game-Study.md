# Phase 19A: Reference game study

**Purpose.** To identify the interaction and visual principles that make these interfaces work, and how Realmkeeper could apply them in its own way.

**Method.**
- The study draws on published play experience and widely documented interface behaviour of each game. The Game UI Database (https://www.gameuidatabase.com) is a good place for the owner to look at the screens themselves.
- **No proprietary artwork, icon, layout, logo or texture is reproduced here, and none will be used.** For copyright reasons, no screenshots of these games are included.
- The claims are general and describe interface conventions, not exact pixel layouts.

## Summary: what Realmkeeper should take

| Principle | Learned from | Realmkeeper application |
|---|---|---|
| One quiet frame, many states | BG3, Witcher 3 | One panel style and one button style everywhere. Hover, selection and disabled states differ by value and edge, not by new art. |
| Information in layers: glance, then inspect, then deep detail | CK3, Pathfinder | Every row shows a summary; a tooltip explains; an inspector holds the full record. Nested tooltips for rules terms (advantage, cover, concentration). |
| A diegetic but legible world map | AC, Witcher 3, RDR2, Skyrim | The atlas on parchment, with drawn symbols for places, a clear legend, layer toggles with their own colours, and unknown land as drawn blank space rather than a grey box |
| Restraint in the exploration HUD | RDR2, KCD2, Skyrim | Show only what is relevant: a prompt when looking at something, condition when it changes, a compass with tracked quest markers |
| Clear action economy in combat | BG3, Pathfinder | Action / bonus / reaction / movement as always-visible pips. Unavailable actions greyed **with a reason**; hit chance and cover shown before committing. |
| Dialogue as a readable script | BG3, Witcher 3, DA:I | The speaker's name and role are prominent, choices are numbered, and a check shows its skill and DC. Consequential choices are marked with a glyph, never by colour alone. |
| Journal as a document | KCD2, Witcher 3, Pathfinder | A two-pane list and page, quests grouped by state, objectives as a checklist, and an entry's history in dated lines |
| Dense data, made scannable | CK3 | Icon-plus-number pairs, and small charts for trends (prosperity, food days), with consistent iconography across screens |
| Settings that tell the truth | All, at their best | Only working options, previewed live, with accessibility (text size, contrast, colour-blind-safe markers) |

## Per game

### Baldur's Gate 3
- **Effective:**
  - A dark, low-saturation frame with fine metallic edges.
  - Large, high-contrast serif headings.
  - A hotbar grouped by resource type: action, bonus action, reaction and spell slots each have their own shape and colour.
  - Rich tooltips that expand into rule explanations.
  - Dice checks with the skill named and the DC shown.
  - Dialogue as cinematic text with numbered options and tagged checks.
- **Lessons:**
  - Realmkeeper already uses SRD rules, so it should **name the rule in the tooltip** (advantage, cover, opportunity attack) and show the number behind every derived value.
  - Give each action resource a distinct, consistent marker shape, so colour is not the only cue.
  - Keep the battlefield clear: the bar sits at the bottom edge, and the combat log is collapsible.

### Kingdom Come: Deliverance 2
- **Effective:**
  - A grounded, historical material language: paper, ink, woodcut-style illustration.
  - The codex and journal read like period documents.
  - The world HUD is nearly empty until something matters.
  - The map is a hand-drawn period map.
- **Lessons:**
  - Realmkeeper's world is low-fantasy and mediaeval-agrarian, so **paper and ink for documents** (journal, map, letters) fit better than glowing magic UI.
  - Exploration should hide the HUD by default and surface condition and hunger when they change.

### Assassin's Creed (recent titles)
- **Effective:**
  - A clean, modern menu skeleton (tabs across the top, cards beneath) under a light thematic skin.
  - A fast, smooth world map with filterable icon categories.
  - A compass strip in the HUD.
- **Lessons:**
  - Navigation must be fast: one key per major screen, Q/E or tabs to cycle, Esc always backs out one level.
  - Atlas layers should work like **filter chips with counts**.

### Far Cry (recent titles)
- **Effective:**
  - A bold, minimal HUD.
  - Strong colour coding for threat versus friendly.
  - A large, readable map with category filters.
  - Short contextual prompts ("Hold E to …").
- **Lessons:**
  - Interaction prompts should name **the verb and the object** ("Talk · Cwen Stroud", "Open · Chest (owned by the miller)") and show ownership or crime risk before acting.

### The Witcher 3
- **Effective:**
  - The inventory paper-doll with a comparison tooltip that shows deltas in green and red, *with arrows*.
  - A quest journal that separates the main quest, side quests and contracts, with a tracked objective.
  - Readable, consistent panel framing.
  - The map's levelled quest markers.
- **Lessons:**
  - Item comparison should show a signed delta and an arrow glyph, not only a colour.
  - The journal tracks one quest and writes it into the HUD.

### Crusader Kings III
- **Effective:**
  - The best-in-class handling of very dense political data:
    - map modes (realm, faith, culture, terrain);
    - nested tooltips with "lock and expand";
    - character portraits with relation icons;
    - an outliner on the right edge for pinned items;
    - consistent icon-plus-number tokens.
  - Every number can be explained by hovering over it.
- **Lessons:**
  - This is closest to Realmkeeper's simulation layer. Take:
    - **map modes as layers** (factions, economy, threats, events, travel);
    - **every number explainable** (prosperity = …);
    - an **outliner** of pinned settlements, people and quests in place of the long scrolling sidebar.
  - Avoid CK3's density at first sight: Realmkeeper should progressively disclose.

### Pathfinder: Wrath of the Righteous
- **Effective:**
  - Thorough character sheets: ability scores with modifiers, a breakdown of every bonus, and a level-up flow that shows prerequisites.
  - The spellbook is organised by level, with slot pips.
- **Lessons:**
  - The Realmkeeper character sheet should show **where each derived value comes from** (AC 17 = armour 14 + Dex 2 + shield 1).
  - The spellbook should be grouped by spell level, with slot pips and prepared or known toggles.
  - Do not draw a "skill tree": SRD progression is level-based choices (ADR scope; Phase 20 owns expansion).

### Dragon Age: Inquisition
- **Effective:**
  - The war table: a stylised map with operation cards, where agents, time and result are shown before committing.
  - The codex is clearly separated from quests.
  - Approval reactions for companions.
- **Lessons:**
  - Settlement and faction decisions should be **cards with consequences previewed**, using data that exists ("this will cost X; stability −2").
  - Companion approval should appear as a small, quiet notification.

### Red Dead Redemption 2
- **Effective:**
  - Extreme HUD restraint, with cores and meters shown only on change.
  - A diegetic catalogue and journal.
  - A paper map with drawn terrain, and roads as ink lines.
  - Contextual multi-verb prompts on the target.
- **Lessons:**
  - Exploration prompts list all valid verbs for the target (Talk / Trade / Inspect), with keys.
  - The HUD fades out after a few seconds without change.

### The Elder Scrolls V: Skyrim
- **Effective:**
  - Very simple, keyboard-friendly list menus.
  - A compass HUD with discovered or undiscovered markers.
  - The constellation skill trees (a clear visual identity).
  - The quest log as a simple two-pane list.
- **Lessons:**
  - Lists must be fully keyboard-driven (arrows, Enter, Esc).
  - Undiscovered locations are shown as outlined or unfilled markers; discovered ones are filled.
  - Avoid Skyrim's weak item filtering: Realmkeeper's inventory needs search, sort and category chips.

## Anti-patterns to avoid
- Gold filigree on every edge, and glowing borders.
- Ornamental blackletter for body text.
- A dark rectangle on a dark rectangle with no hierarchy.
- Bright white form fields (Unity's default `TextField` and `DropdownField`) on a dark UI. The current Speed dropdown and the creator's text fields show this.
- Hiding the reason a control is disabled.
- Colour-only states (the current atlas's road colours pale, ochre and red need a pattern or icon too).
