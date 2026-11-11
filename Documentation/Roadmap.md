# Roadmap and milestone status

| Milestone | Status | Where |
|---|---|---|
| Phase 0–1: foundation, world generation, strategic map | Done | `Documentation/Architecture`, ADR-0001…0007 |
| Phase 2: settlement economies, trade, chronicle | Done (`fa521f2`), completed by the Phase 2 completion pass below | `Documentation/Systems/Economy.md` |
| ADR-0009 prerequisites and exploration vertical slice | Accepted as a vertical slice (`0c7f60d`) | `Documentation/Architecture/DualPerspective.md` |
| Pre-Phase-2 stabilization: session rebinding, save slots | Done in the Phase 2 completion commit | `DualPerspective.md`, `Systems/Persistence.md` |
| Phase 2 completion | Done; see the test results for what was run | `Systems/Economy.md`, `Testing/TestResults.md` |
| Phase 3: factions and political engine | Done (`5c89f8a`, playtest fixes `d82a01b`) | `Documentation/Systems/Politics.md` |
| Graphics pass | Done (`b0545b5`): CC0 modular kit, humanoid residents, street-level lighting | `Reports/GraphicsPass-Report.md` |
| Phase 4: NPC Intelligence Engine | Done (this milestone); see the test results for what was run | `Systems/NPCs.md`, `Systems/Relationships.md`, `Reports/Phase4-NPC-Intelligence.md` |
| Phase 5: Living World, Player Hub & Commerce | Done (`1f37bb6`) | `Systems/WorldTime.md`, `Systems/Player.md`, `Systems/Inventory.md`, `Systems/Equipment.md`, `Systems/Commerce.md`, `Reports/Phase5-Living-World.md` |
| Phase 6: Character Creation & Core RPG Foundation | Done (`f2682a2`) | `Systems/Characters.md`, `Systems/Species.md`, `Systems/Backgrounds.md`, `Systems/Classes.md`, `Systems/SkillsAndAbilities.md`, `Systems/CharacterCreation.md`, `Reports/Phase6-Character-Creation.md` |
| Phase 7: Character Progression & Core Rules Engine | Done (`a3cd9f5`) | `Systems/CharacterProgression.md`, `Systems/RulesEngine.md`, `Systems/Classes.md`, `Phase7/Phase7-Report.md` |
| Phase 8: Combat Foundation, Encounter Engine & Settlement Structures | Done (`2fe83eb`) | `Systems/Combat.md`, `Systems/SettlementStructures.md`, `Phase8/Phase8-Report.md`, ADR-0030…0036 |
| Phase 9: Spellcasting, Class Features & Combat Integration | Done (`27a5261`, committed locally) | `Systems/Spellcasting.md`, `Systems/ClassFeatures.md`, `Systems/RestAndRecovery.md`, `Systems/SpellCatalogAudit.md`, `Phase9/Phase9-Report.md`, ADR-0037…0042 |
| Phase 10: Bestiary, NPC Character Systems, Encounter Intelligence & Living Settlement Generation | Done (`1707805`, pushed) | `Phase10/SettlementGeneration.md`, `Phase10/CreatureAndNpcSystems.md`, `Phase10/Testing.md`, `Phase10/Performance.md`, `Phase10/Limitations.md`, `Reports/Phase10-Bestiary-NPCs-Settlements.md`, ADR-0043…0049 |
| Phase 11: Inventory, Loot, Equipment, Crafting & Item Progression | Done (`7b2831b`, pushed with Phases 9–10 on the owner's instruction) | `Phase11/Items.md`, `Phase11/Testing.md`, `Phase11/Performance.md`, `Phase11/Limitations.md`, `Phase11/PreFlight.md`, `Systems/Inventory.md`, `Systems/Equipment.md`, `Reports/Phase11-Inventory-Items-Progression.md`, ADR-0050…0055 |
| Phase 12: Quest, Dialogue, Reputation & World Interaction Engine | Done (this milestone); awaiting review, committed locally and not pushed | `Systems/Quests.md`, `Systems/Dialogue.md`, `Systems/Reputation.md`, `Phase12/PreFlight.md`, `Phase12/Testing.md`, `Phase12/Limitations.md`, `Reports/Phase12-Quest-Dialogue-Reputation.md`, ADR-0056…0061 |
| Phase 13 | **Not started.** Phase 12 stops for review; recommended priorities are in the Phase 12 report (§T). | — |

## Phase 2 completion: what it added

- **Trade:** routes can be suspended and resumed through a typed command, which is chronicled
  and saved. Schema v4 adds this, with a migration from v3.
- **Inspector:**
  - a "Why" line under prosperity and stability, listing the terms behind each target;
  - the food reserves trend, and whether a shortage is easing or worsening;
  - "Generalist" for settlements without a specialization;
  - a Suspend/Resume toggle on each route, and a count of active routes.
- **Map:** markers above settlements for food shortage, declining stability, strong production
  and trade on the road.

## Known limitations carried forward

- One region per world; three settlements in the default world.
- There are no births, deaths, migration of individuals, construction, crafting or caravans.
  These were out of scope.
- Performance was measured only at the default world size: a 30-day run takes 7–9 ms. No
  claim is made for larger worlds.
- One save slot is active at a time. There is no save browser.
- The map markers use `Universal Render Pipeline/Unlit` found by name. A player build must
  include that shader (editor runs are unaffected). Not yet verified in a build.
## Phase 3: what it added

- **Factions:** first-class factions of seven types. They have status, leaders (existing
  residents), influence explained by its terms, a treasury funded by real dues, aggregate
  membership, presence, and goals.
- **Governance and claims:** governance and administration of settlements, and territorial
  claims that never move borders by themselves.
- **Relationships:** multi-dimensional, with readable states, causes and agreements.
- **Simulation:** a weekly political tick inside the daily transaction, tied to stability,
  prosperity and trade.
- **Player actions:** nine typed political commands, each chronicled as the player's.
- **UI:** the Factions list and inspector, a Politics section in the settlement inspector, and
  an optional political map overlay. The exploration HUD names the governing faction.
- **Saves:** schema v5, with a deterministic upgrade of older saves.

Still out of scope: warfare, rebellion, diplomacy trees, NPC political AI, religion or crime
detail, multiple regions.

## Phase 5: what it added

- **A clock:** a minute of the day on top of the date. Each midnight crossed runs one simulated day.
  - The realm map keeps whole-day pacing.
  - Settlements run in minutes, with pause, speed and wait.
- **Light that follows the clock:** sun, golden hour, moon, sky, haze, lit lamps and windows.
- **Residents who walk** to where the world says they are each hour. Their poses match what they
  are doing, and they go indoors at home or asleep.
- **A full player character:** identity, renderable appearance, purse, hunger, an inventory of
  stacks with one owner each, and eight equipment slots shown on the body.
- **The Player Hub:** eight sections and a live 3D preview.
- **Shops kept by existing residents:**
  - stocked from the settlement's stores through the existing ledger, paid for by the keeper;
  - atomic buying and selling, with explained prices and no arbitrage;
  - reputation that grows once a day;
  - trades in the ledger and the chronicle;
  - keepers remember notable trades.
- **Walk-in Outfitters and Smithy** whose shelves show every unit of stock.
- **Knowledge-bound talk** about work, shops, goods, prices and factions.
- **Save schema v7**, with a once-only migration that matches a fresh world.

Known limitations are listed in `Reports/Phase5-Living-World.md` §15. The main ones:

| Area | Limitation |
|---|---|
| Combat | None |
| Props | Procedural |
| Clothing | Two outfit families |
| Walking | Street routes rather than a navmesh |
| Shop stock | Counts, not item instances |
| Loose items | None in the world |
| Map advances | Leave the time of day and hunger unchanged |

## Phase 6: what it added

- **One RPG character on the existing player** (ADR-0020): species, background, class, six
  ability scores, eighteen skills, six saves, hit points, resources and level 1. Every derived
  value is computed, never stored.
- **Twelve species, twelve backgrounds, twelve classes** as validated data with typed effects
  (ADR-0021), SRD 5.2.1 / 5.1 mechanics under CC BY 4.0 and Realmkeeper's own text (ADR-0022).
- **A nine-step character creator** in the exploration scene with a live 3D preview wearing the
  chosen kit, standard array or point buy, skill, option and equipment choices, and a review.
- **One creation transaction** that grants the class and background kit exactly once and keeps
  identity, belongings and location (ADR-0023).
- **A full character sheet** in the Player Hub (sheet, skills, features, proficiencies, identity).
- **Effects already in play:** species walking speed, goliath carrying capacity, merchant prices,
  species height.
- **Save schema v8:** older players load as Legacy, with nothing lost and nothing invented.

Main limitations (full list in `Reports/Phase6-Character-Creation.md`):

| Area | Limitation |
|---|---|
| Species art | Only humans have finished models; the other eleven are labelled placeholders (ADR-0024) |
| Levels | Level 1 only; no experience awards, levelling or subclasses |
| Spells | Spellcasting stats only; no spells |
| Combat | Features stored as typed data, not used |
| Armour class | Provisional (no armour categories yet) |
| Social features | Shown on the sheet, not yet read by dialogue, quests or reputation |

## Phase 7: what it added

- **Experience** through a keyed ledger (the same quest or discovery never pays twice), using the SRD 5.2.1
  thresholds as configuration. Experience makes a character eligible; it never levels them up.
- **Advancement from level 1 to 20** in one validated transaction per command (one or several levels), with a
  chronicle entry and an advancement record per level.
- **All twelve classes progress to 20** with SRD 5.2.1 features, scaling columns, resources, spell slots and
  subclass levels. One SRD subclass per class; SRD feats (Ability Score Improvement, Grappler, origin feats,
  Fighting Style feats, Epic Boons); invocations; Metamagic.
- **A shared rules engine:** conditions, stacking, advantage and disadvantage, initiative, passive scores,
  senses and movement, prerequisites, feature replacement, a breakdown for every value, and integration notes
  for what waits for combat, magic or rests.
- **Resource and hit-point commands:** spend, restore, recover (the rest hook), damage, healing, temporary hit
  points, hit dice.
- **A level-up window** in the exploration scene, the HUD's "Level up (L)", and the Hub's experience bar,
  level-up banner and Progression panel.
- **Save schema v9:** Phase 5 and Phase 6 saves load. Druids and warlocks from Phase 6 make their new level-1
  choice at their next level-up.

Main limitations (full list in `Phase7/Phase7-Report.md`):

| Area | Limitation |
|---|---|
| Experience sources | No gameplay system awards experience yet (quests and combat come later); the command is ready |
| Subclasses | One per class (the SRD's); others deferred |
| Combat, spells, rests | Features are acquired and shown, with what they wait for; nothing resolves attacks, spells or rest time |
| Invocations | Three that need damage cantrips, and Lessons of the First Ones, deferred |
| Multiclassing | Not supported |
| NPCs and companions | The services take any sheet, but no NPC holds one yet |
| Species art | Unchanged placeholders (Phase 6) |

## Phase 8: what it added

- **A combat engine in the domain** (ADR-0030), driven by commands:
  - encounters with any number of sides and reinforcements;
  - deterministic initiative and dice (ADR-0032);
  - the action economy;
  - a 5-ft grid with difficult terrain and opportunity attacks;
  - SRD attacks with advantage and disadvantage, natural 1 and 20, critical hits;
  - grapple and shove;
  - damage with resistance, vulnerability and temporary hit points;
  - all 15 conditions;
  - death saves, stabilizing and death (ADR-0034).
- **SRD armour and weapons replace the provisional armour class** (ADR-0033):
  - armour categories, Dex caps, shields;
  - training penalties and Strength requirements;
  - the AC breakdown;
  - weapon profiles on the existing items, plus chain mail.
- **Participants.** Residents fight as themselves, never copied (ADR-0031). Their deaths go through
  the shared NPC death routine.
- **A deterministic AI** that proposes; the engine decides.
- **A practice encounter** from the settlement HUD (F): two bandits, with the local guard on the
  player's side. It uses the encounter panel (initiative, a tactical grid, actions with reasons,
  results, inspector, log) and placeholder ground markers. Time stands still during the fight.
- **The chronicle** gets the start, the end, people downed and deaths only (ADR-0036).
- **Settlement structures** (ADR-0035):
  - every existing place is a functional structure, housing its shop;
  - informational and decorative structures are added by size;
  - shown in the settlement inspector and as signs in the street.
- **Save schema v10:** Phase 5, 6 and 7 saves load. Older worlds gain their structures, and no
  encounter.
- **Shipped credits:** `CREDITS.md`, the Hub's Info section and the README (the SRD CC BY 4.0
  attribution and the CC0 art).

Main limitations (full list in `Phase8/Phase8-Report.md`):

| Area | Limitation |
|---|---|
| Encounters | The labelled practice fight is the only way to start one in play; no encounter generator |
| Class features | Second Wind and Cunning Action only (the rest came in Phase 9) |
| Spells, concentration | Done in Phase 9 |
| Presentation | Placeholder ground markers and stand-in figures; no creature models or animations; the grid lives in the panel |
| NPC statistics | Residents use SRD Guard or Commoner profiles until NPCs have character sheets |
| Rewards, recovery | No experience from combat; no resurrection or medical system |
| Structures | Informational ones simulate nothing yet; no construction or upkeep |

## Phase 9: what it added

- **One spell engine for every caster** (ADR-0037).
  - **Catalog:** 47 SRD 5.2.1 spells, cantrips to level 5, each with an honest status: 27
    functional, 10 partial, 4 data-only and 6 deferred.
  - **Targeting:** creatures, points, self, and sphere, cube, cone, line and cylinder areas on the
    grid.
  - **Resolution:** spell attacks and saves through the shared rolls; upcasting and cantrip scaling;
    one levelled slot per turn.
  - **Effects:** a stored effect model with concentration, repeat saves and turn-based expiry
    (ADR-0039).
- **Spell state** (ADR-0038).
  - **Picks:** known spells, cantrips and wizard spellbooks are ordinary character choices, offered
    at creation and level-up and fillable later from the Hub.
  - **Preparation:** after a long rest; paladins and rangers swap one.
  - **Pact Magic:** kept distinct from spell slots.
  - **Rules version 8.**
- **Class features in play for all twelve classes** (ADR-0041): 38 combat features through one
  activation model, among them Rage, Sneak Attack, Action Surge, Channel Divinity, Wild Shape (one
  form), Martial Arts, Lay on Hands, Smite, Font of Magic, Metamagic and the Evoker features.
- **NPC casters** (ADR-0042): the SRD 5.1 Acolyte and Cult Fanatic cast through the same engine; a
  deterministic caster AI; a "Practice vs caster (G)" fight.
- **Rest and recovery** (ADR-0040):
  - short and long rests pass world time;
  - hit-die spending, and explicit recovery per resource;
  - Arcane Recovery;
  - one long rest per 24 hours;
  - idempotent.
- **UI:**
  - **Hub, Spells & rest:** the spellbook with search, filters, details, preparation, open picks,
    slots, rests and features.
  - **Encounter panel:** Spells and Class features sections; a cast composer with slot level,
    targets, metamagic and grid aiming with area preview; effects and concentration on the rows and
    in the inspector.
- **Save schema v11:** Phase 5–8 saves load.

Main limitations (full list in `Phase9/Phase9-Report.md`):

| Area | Limitation |
|---|---|
| Reactions | Automatic (ADR-0033); Shield, Hellish Rebuke, Counterspell and reaction features wait for a prompt |
| Durations | Effects end with the fight; out-of-combat durations (Mage Armour, Aid) are not tracked |
| Spells | Levels 6–9 have no catalog entries yet; summons, moving auras, light, stealth and terrain effects wait |
| Wild Shape | One form (wolf) |
| Metamagic | Four of ten options not simulated |
| NPC casters | Two SRD 5.1 stat blocks; slots refill per encounter |

## Phase 10: what it added

- **Named people with character records** (ADR-0043).
  - Fighters, healers and wardens hold full sheets, built by the player's rules.
  - Commands promote a person, award keyed experience and advance levels.
  - People with sheets fight as themselves.
  - The town and hamlet gain the posts their places call for (15 → 19 named people).
- **A bestiary and aggregate wildlife** (ADR-0044).
  - 20 SRD 5.1 creatures and 5 labelled originals, with traits and behaviours applied in combat.
  - Weekly populations, predation, hunting and herds.
  - Named individuals drawn into fights, whose fates persist.
- **Reactions asked, not taken** (ADR-0045). Suspend and replay. The encounter panel's prompt offers Accept or
  Decline, and the answer is atomic and saved while it waits.
- **Encounters from the world** (ADR-0046). Outings to the outskirts, the road or the farms, planned from the
  populations in range, checked before they start, and settled once.
- **Settlement identity and terrain-first plans** (ADR-0047, ADR-0048, ADR-0049).
  - Ground, water and bridges, roads, districts, buildings on buildable ground, farms and herds, landmarks and
    walls.
  - The plan is persisted and never regenerated.
  - The walkable settlement, the strategic map and the inspector all read it.
  - People walk its road graph and stand on its ground.
- **Schema v12** with migration; older saves gain the Phase 10 data once on load.

## Phase 11: what it added

- **An item model with instances** (ADR-0050). 114 items, 66 of them SRD 5.2.1. Each instance carries its own condition, make, charges, attunement, curse binding and provenance. One factory makes every instance.
- **One holder per instance, atomic transfers, a weight-limited pack** (ADR-0051).
  - The player's chest in every settlement, a cache filled once, loot piles.
  - Take, put, take all and coin.
  - SRD carrying capacity: over it Speed is 5 ft; past twice it nothing more can be taken.
- **Equipment and magic through the rules** (ADR-0052).
  - Condition that wears in play and changes attack and AC.
  - 11 magic items: +1 weapons, armour and shield, rings and cloaks of protection, gauntlets, goggles, boots, a wand with charges, and a cursed axe.
  - Attunement (three, an hour each).
  - Ammunition spent and half recovered.
  - Rings on either hand, and off-hand Light weapons.
- **Loot** (ADR-0053). 15 tables and carried gear, seeded and filled once into a container per fight, bounded by rarity. It opens from the combat panel.
- **Crafting, gathering, repair and attunement as work** (ADR-0054). 11 recipes; materials held; a tool check rolled when the work starts; world time passes. Gathering sites come from the settlement economy and recover weekly. An armourer mends for a price.
- **Merchants.** Offers count condition and make. Distinct items keep their identity as a shop's particular goods.
- **Screens** (ADR-0055).
  - Inventory search, filters, sorting, inspection and rules-engine comparison.
  - Eleven equipment slots.
  - Crafting & work, and Storage.
  - The chest and cache in the street.
  - The shop's particular goods and mending.
- **Schema v13** with migration; v12 saves gain the item world once on load.
