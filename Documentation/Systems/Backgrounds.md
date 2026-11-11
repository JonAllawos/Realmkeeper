# Backgrounds

There are twelve backgrounds (`RpgCatalog.Backgrounds`). Each gives:

- two skills;
- tool proficiencies;
- possibly a language;
- a little gear and coin (granted once, at creation);
- a **feature** (a typed effect);
- optional roleplaying prompts.

A background never decides a future occupation or faction.

| Background | Skills | Tools | Language | Kit and coin | Feature | Source |
|---|---|---|---|---|---|---|
| Soldier | Athletics, Intimidation | Dice set | — | Dice set, 2 rations, 14 coin | Military Rank (social) | SRD 5.2.1 proficiencies |
| Noble | History, Persuasion | Dice set | Elvish | Dice set, 25 coin | Position of Privilege (social) | Realmkeeper |
| Criminal | Sleight of Hand, Stealth | Thieves' tools | — | Thieves' tools, 16 coin | Criminal Contact (social) | SRD 5.2.1 |
| Acolyte | Insight, Religion | Calligrapher's supplies | Celestial | Holy symbol, calligrapher's supplies, 8 coin | Shelter of the Faithful (social) | SRD 5.2.1 |
| Artisan | Investigation, Persuasion | Smith's tools | — | Smith's tools, 15 coin | Guild Standing (social) | Realmkeeper |
| Merchant | Insight, Persuasion | Navigator's tools | Dwarvish | 2 rations, 30 coin | **Trade Connections: −5% buying, +5% selling in shops** | Realmkeeper |
| Outlander | Athletics, Survival | Herbalism kit | Sylvan | 5 rations, 8 coin | Wanderer (social) | Realmkeeper |
| Scholar | Arcana, History | Calligrapher's supplies | Draconic | Calligrapher's supplies, 10 coin | Researcher (social) | SRD 5.2.1 (Sage) |
| Sailor | Athletics, Perception | Navigator's tools | — | Navigator's tools, 10 coin | Ship's Passage (social) | Realmkeeper |
| Entertainer | Acrobatics, Performance | Lute, disguise kit | — | Lute, 15 coin | By Popular Demand (social) | Realmkeeper |
| Hermit | Medicine, Religion | Herbalism kit | Primordial | Herbalism kit, 5 coin | Discovery (story hook) | Realmkeeper |
| Folk Hero | Animal Handling, Survival | Carpenter's tools | — | Carpenter's tools, 10 coin | Rustic Hospitality (social) | Realmkeeper |

- **Merchant's feature** already changes play: it is a labelled price factor in every shop.
- **The social features** are typed `SocialFeature` effects with stable keys, waiting for the
  dialogue, quest and reputation systems to read them. They are shown on the sheet but have no
  mechanical effect yet.
- **Background kits** use the existing item catalog. Phase 6 added the tools as real items
  (category Tool): carriable, sellable, and shown in shops and on shelves.
