# Combat (Phase 8; spells and features from Phase 9)

Turn-based tactical combat on a 5-ft grid, following the SRD 5.2.1 rules. The engine is
`Runtime/Domain/Combat`, the commands and queries are in `Runtime/Application`, and the panel is
`Runtime/Exploration/Presentation`. The decisions behind it are ADR-0030 to ADR-0034 and ADR-0036.

## Layers

| Layer | Contents |
|---|---|
| Domain (`Domain/Combat`) | `CombatModel` (encounter, combatant, conditions, turn resources, log, grid), `CreatureCatalog`, `CombatStats` (derived statistics), `CombatActions` (actions, results, setup), `CombatEngine` (rules), `CombatAi` (proposals) |
| Domain (`Domain/World`) | `ItemCombat` (SRD weapon and armour profiles on items); encounter validation in `WorldValidator.Combat` |
| Application | `CombatCommands` (start, practice, act, AI turn, join, conditions, heal, end), `CombatQueries` (`EncounterSnapshot`, options with reasons, move check) |
| Infrastructure | Schema v10: `WorldSaveMapper.Combat`, `MigrationV9ToV10` |
| Presentation | `CombatWindow` (the panel), `ExplorationController.Combat` (commands, AI timer, ground markers, paused clock) |

## Encounters

- **Lifecycle.** An encounter is created and begun in one command:
  - the participants are validated;
  - initiative is rolled and the order sorted;
  - the first turn starts.
  It is **Active** until one side is left able to fight (**Victory**), nobody is (**Stalemate**),
  the player's side withdraws (**Withdrawn**), or a command ends it (**Ended**).
- **Storage.** At most one encounter is active. The 20 most recent finished encounters are kept.
- **Sides** are strings. The same side means allies; a different side means enemies. `"neutral"`
  is hostile to nobody. Any number of sides can take part.
- **Reinforcements** join with `JoinEncounterCommand`. They roll initiative and take their place in
  the order without changing whose turn it is.
- **The practice encounter** ("Practice: bandits at the edge of town") is the only way to start a
  fight in play; no encounter generator exists yet. It is started from the HUD, or with F, in a
  settlement.
  - **Participants:** the player, a bandit, a bandit archer, and the settlement's guard on the
    player's side if one lives there.
  - **Terrain:** two squares of difficult terrain.
  - **Lethality:** lethal by default (the command accepts non-lethal).
- **Time stands still** while a fight is active:
  - time advancement is refused;
  - leaving the settlement is refused;
  - the exploration clock pauses, and resumes when the panel closes.

## Combatants

| Kind | Identity | Statistics | Hit points and death saves |
|---|---|---|---|
| Player | the player's character | `CharacterRules` and the equipped loadout | on the character sheet |
| NPC (resident) | the NPC's ID; never duplicated | SRD profile by role: guard → Guard, others → Commoner | on the combatant; death saves |
| Creature | catalog ID | SRD statistics | on the combatant; dies at 0 |

**Creatures (SRD 5.2.1 statistics):**
- **Commoner:** AC 10, 4 HP, club +2 (1d4).
- **Bandit:** AC 12, 11 HP; scimitar +3 (1d6+1); light crossbow +3 (1d8+1, 80/320 ft).
- **Guard:** AC 16, 11 HP; spear +3 (1d6+1, reach 5 or thrown 20/60 ft).
- **Wolf:** AC 12, 11 HP, speed 40; bite +4 (1d6+2, knocks prone); Pack Tactics.

Descriptions are Realmkeeper's own.

## Initiative

- **Roll.** d20 + Dexterity modifier, plus any bonuses from features (`CharacterRules.Initiative`),
  with advantage or disadvantage from roll modes, being invisible or being incapacitated.
  Exhaustion subtracts 2 per level.
- **Ties** are broken in this order:
  - higher initiative;
  - then the higher Dexterity modifier;
  - then a seeded tie-break value;
  - then the combatant ID.
  This is deterministic (ADR-0032).

## Turns and the action economy

- **At the start of a turn:**
  - the action, bonus action, reaction, free interaction, movement, Disengage and Dodge reset;
  - conditions timed to expire then expire;
  - a dying combatant makes a death save, and its turn passes.
- **Actions:**
  - **Attack:** with Extra Attack, the action gives 2, 3 or 4 attacks.
  - **Dash:** adds the combatant's speed to its movement.
  - **Disengage:** no opportunity attacks this turn.
  - **Dodge:** attacks against it have disadvantage, and it has advantage on Dexterity saves.
  - **Stand up:** costs half speed.
  - **Escape a grapple:** Athletics or Acrobatics, whichever is better, against the escape DC.
  - **Stabilize:** Medicine DC 10, adjacent target.
  - **End turn.**
- **Bonus actions from features:** Second Wind (fighter: heals 1d10 + level and uses a charge), and
  Cunning Action Dash or Disengage (rogue). Other class features are deferred.
- **Reactions:** opportunity attacks, taken automatically (ADR-0033).
- **Reasons.** Every option the player cannot take says why. The reason comes from the engine's
  `Unavailable`, which the AI and the panel share.

## Movement

- **The grid:**
  - 5-ft squares;
  - distance is Chebyshev, so a diagonal step costs 5 ft;
  - the grid has height (Z) but no elevation rules yet.
- **Step costs:** 5 ft, plus 5 in difficult terrain and plus 5 while crawling (prone).
- **Blocking:** a conscious enemy blocks its square. A move cannot end on an occupied square.
- **Speed after conditions:**
  - 0 when grappled, restrained, paralyzed, petrified, unconscious or down;
  - −5 ft per exhaustion level;
  - −10 ft in heavy armour without its Strength requirement.
- **Opportunity attacks.** Leaving an enemy's reach provokes one, unless the mover has disengaged.
  - It is taken right before the step, with the enemy's best melee attack.
  - It needs a reaction and an enemy that can see.
  - If it drops the mover, the move ends there.
- **Grapples end** when the grappler is incapacitated or no longer adjacent.

## Attacks

1. **The roll.** d20 + attack bonus − 2 × exhaustion against the target's AC.
   - The attack bonus is the ability modifier, plus proficiency if proficient, plus Archery for
     ranged attacks.
   - Advantage and disadvantage come from features, conditions, Dodge, range, adjacent enemies (for
     ranged attacks) and Pack Tactics. Both together cancel out.
2. **Natural rolls.** A natural 1 always misses ("a natural 1 always misses"). A natural 20 always
   hits and is a critical hit.
3. **Automatic critical.** A hit within 5 ft on a paralyzed or unconscious target is a critical.
4. **Damage.**
   - Roll the weapon's dice, twice on a critical; Great Weapon Fighting raises low dice.
   - Add the ability modifier, plus Duelling.
   - Versatile weapons use the larger die when held two-handed.
5. **Grapple and Shove.** These are Unarmed Strike options. The target makes a Strength or
   Dexterity save, whichever is better, against 8 + Strength + proficiency. Grapple needs a free
   hand.
6. **What the result explains:** both dice and the reason for the roll mode, the bonus breakdown,
   the total against AC, hit, miss (and why) or critical, the damage dice and modifiers, and hit
   points before and after.

## Damage, healing and death (ADR-0034)

- **Defences.** Immunity, then resistance (halved, rounded down; petrified creatures resist all
  damage), then vulnerability (doubled). Temporary hit points absorb damage first.
- **At 0 hit points:**
  - **Lethal fight:**
    - **The player and residents:** they fall unconscious and prone, and are dying.
    - **Creatures:** they die.
    - **Massive damage:** if the remainder beyond 0 is at least the hit point maximum, they die
      outright.
  - **Non-lethal fight:** everyone is knocked out (unconscious and stable). No one dies.
- **Death saves:**
  - DC 10. Three successes means stable; three failures means death.
  - A natural 20 regains 1 hit point and wakes; a natural 1 is two failures.
  - Damage taken while down is a failure, two on a critical. Damage at least equal to the hit point
    maximum kills (lethal fights only).
- **Healing** works up to the maximum. Healing a combatant at 0 wakes it and clears its death saves.
  The dead cannot be healed.
- **When the fight ends:** a dying player is stabilized. Resurrection does not exist.
- **Residents' deaths** use the shared NPC death routine and add an `NpcDied` event with the cause
  "combat".

## Conditions

All 15 SRD conditions exist (`ConditionType`), with these effects:

| Condition | Effects |
|---|---|
| Blinded | Disadvantage on attacks; attacks against it have advantage; no opportunity attacks |
| Charmed | Cannot attack the charmer |
| Deafened | Recorded (no hearing checks yet) |
| Exhaustion (levels 1–6) | −2 × level to D20 tests; −5 × level ft speed; death at 6 |
| Frightened | Disadvantage on attacks and checks; cannot move closer to the source |
| Grappled | Speed 0; disadvantage on attacks against anyone but the grappler; escape DC |
| Incapacitated | No actions, bonus actions or reactions |
| Invisible | Advantage on attacks and initiative; attacks against it have disadvantage |
| Paralyzed | Incapacitated; speed 0; fails Strength and Dexterity saves; attacks against it have advantage; critical within 5 ft |
| Petrified | As paralyzed; resists all damage; immune to the poisoned condition |
| Poisoned | Disadvantage on attacks and ability checks |
| Prone | Disadvantage on its attacks; attacks against it have advantage within 5 ft and disadvantage beyond; crawling costs double; standing costs half speed |
| Restrained | Speed 0; disadvantage on attacks and Dexterity saves; attacks against it have advantage |
| Stunned | Incapacitated; fails Strength and Dexterity saves; attacks against it have advantage |
| Unconscious | Incapacitated; prone; fails Strength and Dexterity saves; attacks against it have advantage; critical within 5 ft |

- **Stacking.** The same condition does not stack: the longer duration is kept. Exhaustion levels
  add up.
- **Durations:** until removed, until the start of someone's turn, or until the end of someone's
  turn.
- **Immunities** come from the creature profile.

## Armour and weapons (ADR-0033)

| Armour | Category | AC | Notes |
|---|---|---|---|
| Leather jerkin | Light | 11 + Dex | SRD leather |
| Forester jerkin | Light | 12 + Dex | SRD studded leather |
| Chain shirt | Medium | 13 + Dex (max 2) | |
| Chain mail (new item) | Heavy | 16 | Strength 13; Stealth disadvantage |
| Round shield, buckler | Shield | +2 | the SRD has one shield |

Without armour, AC is the better of 10 + Dex and the class's Unarmoured Defence.
- **Untrained armour:** disadvantage on Strength and Dexterity D20 tests; spellcasting is blocked
  (recorded for when spells exist).
- **Untrained shield:** no AC.

**Weapons map to SRD profiles:**

| Item | SRD weapon | Damage | Properties |
|---|---|---|---|
| Iron dagger | dagger | 1d4 piercing | finesse, light, thrown 20/60 |
| Arming sword | longsword | 1d8 slashing | versatile 1d10 |
| Hand axe | handaxe | 1d6 slashing | light, thrown 20/60 |
| Flanged mace | mace | 1d6 bludgeoning | |
| Ash spear | spear | 1d6 piercing | thrown 20/60, versatile 1d8 |
| Quarterstaff | quarterstaff | 1d6 bludgeoning | versatile 1d8 |
| Hunting bow | shortbow | 1d6 piercing | ammunition 80/320, two-handed |
| Greataxe | greataxe | 1d12 slashing | heavy, two-handed |
| Rapier | rapier | 1d8 piercing | finesse |
| Shortsword | shortsword | 1d6 piercing | finesse, light |
| Scimitar | scimitar | 1d6 slashing | finesse, light |
| Light crossbow | light crossbow | 1d8 piercing | ammunition, loading, two-handed, 80/320 |
| Javelin | javelin | 1d6 piercing | thrown 30/120 |

- **Proficiency** follows the class lists.
- **Unarmed Strike:** 1 + Strength bludgeoning.

## The AI (`CombatAi`)

- **It only proposes.** The engine validates every proposal. A refused proposal ends the turn, so
  the AI is never stuck.
- **Order of thought:**
  1. stand up if prone;
  2. when at or below 25% of its hit points with an enemy adjacent: Disengage and step away, or
     Dodge if it cannot move;
  3. attack the weakest enemy in reach;
  4. otherwise shoot, if no enemy is adjacent;
  5. otherwise close in on the nearest enemy (greedy path);
  6. otherwise Dash;
  7. otherwise end the turn.
- **It ignores the fallen** unless its profile says otherwise.
- **Deterministic.** Ties are broken by distance, then hit points, then ID.
- **Data-driven.** `CombatAiProfile` holds the retreat threshold and whether to finish the fallen,
  so personality or morale can tune it later.

## Persistence (schema v10)

- **What is saved.** Encounters are saved whole:
  - status, outcome, round, turn index and order;
  - combatants (vitals, position, conditions, turn resources, initiative);
  - difficult terrain;
  - the seed as a decimal string, the roll counter and the action sequence;
  - the log;
  - references (quest, world event).
- **What is not saved:** derived values such as armour class, attacks and speed.
- **The character sheet** adds `deathSaveSuccesses`, `deathSaveFailures`, `isStable` and `isDead`.
- **`MigrationV9ToV10`** adds an empty encounter list and the default dying state. Hit points,
  belongings and everything else are untouched.
- **Validation rejects:**
  - two active encounters;
  - duplicate IDs;
  - the same resident twice;
  - unknown creatures;
  - hit points out of range;
  - a broken initiative order;
  - exhaustion outside 1–6;
  - death saves outside 0–3;
  - stable or dead with hit points left;
  - a log over 500 entries;
  - more than 20 finished encounters.

## Chronicle (ADR-0036)

The chronicle gets only these events, each tagged `source: combat`:
- `EncounterStarted` and `EncounterEnded`;
- `CombatantDowned` (people);
- `CombatantKilled` (the player);
- `NpcDied` (residents).

The per-roll detail stays in the encounter's own log.

## The panel

The panel is docked along the bottom of the screen, so the fight stays visible above it.
- **Header:** title, round, lethality and whose turn it is. Continue (computer turns), Withdraw (the
  engine refuses it next to an enemy), Call off (practice only), and Close (once the fight is over).
- **Initiative:** one row per combatant (hit points, AC, state, death saves, conditions). Click a row
  to inspect.
- **Field:** a 15 × 11 square grid around the player.
  - Reachable squares are green; difficult terrain is shaded.
  - Each square's tooltip says why it is not reachable.
  - Click a square to move there.
- **Your turn:**
  - the action economy line;
  - every option, with the reason under each unavailable one;
  - End turn.
- **Result, Inspect, Combat log:** the last result explained, the selected combatant's full
  statistics, and the last 40 log lines.
- **In the street:** placeholder ground markers on 1.5 m squares. They are coloured discs, with
  capsules for people and boxes for beasts, and name labels. No creature models exist yet. The
  player's body moves to its square.
- **Timing.** Computer-controlled turns run one every 0.6 s, or at once with Continue.

## Phase 9 additions

- **Spells** cast through the engine with the shared rolls: `Spellcasting.md`.
- **Class features:** `ClassFeatures.md`.
- **Lasting effects and concentration:** in the encounter (ADR-0039).
- **Weapon Mastery:** Vex, Sap, Slow, Topple, Push and Graze.
- **Creature casters:** the SRD 5.1 Acolyte and Cult Fanatic.
- **Schema v11:** encounters also save effects, concentration, combatant resources and Wild Shape
  forms.
- **Panel:** Spells and Class features sections, the cast composer with grid aiming, and folding
  sections. Effects and concentration are on the initiative rows and in the inspector.

## Phase 10 additions

- **Reactions asked, not taken (ADR-0045).**
  - Opportunity attacks, Shield, Parry, Cutting Words, Deflect Attacks, Uncanny Dodge, Hellish Rebuke,
    Counterspell, Indomitable and Bardic Inspiration are asked at their rule point.
  - The player answers through the panel's prompt; computer reactors decide by policy.
  - The action is suspended, then replayed with the answer.
- **NPCs with sheets fight as themselves (ADR-0043).** Their vitals, effects and spells are read from and
  written to their sheet (`CombatStats.SheetOf`).
- **Bestiary traits and behaviours (ADR-0044).**
  - Traits: Charge, Regeneration, Undead Fortitude, Relentless, Nimble Escape, Recharge, Parry.
  - Attack riders (save on hit, grapple escape DC).
  - Behaviour-driven AI: flight for skittish animals, packs, predators, the mindless, the territorial.
  - A flee action ends the turn.
- **World encounters (ADR-0046).** Planned from the world's populations and checked before they start. Their
  instances have fates, and consequences are settled once when the fight ends.
- **Inspector.** A creature's bestiary entry: category, challenge and XP, behaviour, traits and source.
- **Persistence.** Schema v12: the waiting question, the encounter's origin, combatant anchors and creature
  instances.

## Deferred (not in Phase 9)

- **Reactions:** (Phase 10: done; see above.)
- **Weapon Mastery:** the chosen-weapons limit, Nick and Cleave.
- **Weapons:** off-hand attacks, and ammunition counts.
- **Sight and terrain:** cover, line of sight, lighting and elevation rules.
- **Companions:** companions and their control. (NPC character sheets: Phase 10.)
- **Content:** (encounter generation and a bestiary: Phase 10.)
- **Presentation:** creature models, animations and a 3D grid renderer.
- **Rewards and recovery:** experience awards from combat, resurrection, and recovery and medical
  systems.

## Line of sight, line of effect and cover (Phase 14, ADR-0067)

One function, `GridSight` (`Domain/Combat/GridSight.cs`), answers every sight question, in fights and in places alike. It works in whole numbers on a doubled grid, so it gives the same answer every time.

**Cover.** From a corner of the attacker's square, four lines are drawn to the four corners of the target's square. A line is blocked if it passes through a wall square, between two diagonal walls, or along a seam that walls close on both sides.

| Lines blocked | Cover | Effect |
|---|---|---|
| None | None | — |
| 1–2 | Half | +2 AC and Dexterity saves |
| 3 | Three-quarters | +5 AC and Dexterity saves |
| 4 | Total | Cannot be targeted |

The attacker uses whichever of its corners gives the least cover. Low obstacles (a table, a sarcophagus) and creatures in the way give half cover at most.

**Where it applies:**
- **Attacks.** Cover is added to the target's AC, and the breakdown and the log name it ("half cover from the pillar").
- **Spells.** A target or point needs line of effect. An area spreads from its origin only to squares the centre line reaches, and only walls stop it. Dexterity saves get the cover bonus unless the spell ignores cover (Sacred Flame).
- **The AI.** It takes the same rules. Melee and ranged choices need line of effect, and a shooter without a line closes in along a real route (`PathAround`), never through walls.

**Doors and low cover in fights.** Fights in places carry the doors of the room and its neighbours (`EncounterData.Doors`: open or shut, locked) and the low obstacles (`EncounterData.LowCover`). A shut door is a wall until someone uses an action to open it (`DoorAction`). When the fight ends, the door states go back to the place.

**The combat window:**
- Walls, doors (brown) and low cover (tan) are drawn on the grid.
- Figures out of the player's line of sight are drawn faded.
- Each foe's square and order row say what cover it has from the player.

Performance: a cover check on a 40×40 grid with 15 % walls takes 20–24 µs; an area line, 0.5–1 µs.

## The combat interface (combat overhaul, ADR-0072)

**Moving.**
- Point at the ground. The route the rules allow is drawn: green while the movement left covers it, amber where Dash would be needed. A label beside the cursor gives the cost, what is left, difficult terrain, and any opportunity attack the route invites.
- Click to move. The body walks the route.
- A move that needs Dash asks first ("Dash and move" or Cancel); nothing is spent until the player agrees.
- A move too far even with Dash offers to go as far as possible.
- A square that cannot be reached says why: wall or shut door, someone standing there, no way round, speed 0 and the condition causing it, or outside the fight.
- Moving several times in a turn uses what is left each time.
- The route comes from `CombatPaths.Plan` (Domain) through `WorldSession.PlanPlayerMove`. The engine still makes and checks every move.

**The screen.**

| Piece | Position | Contents |
|---|---|---|
| Top strip | top | The fight's name and round, whose turn, Withdraw or Call off; initiative chips (name and HP, the rest in the tooltip) |
| Bar | bottom centre | The Action, Bonus and Reaction slots (dimmed when used), movement left, the groups (1 Attack, 2 Actions, 3 Spells, 4 Features, with how many can be used now), Dash, Grid, End turn |
| Pop-up | above the bar | The open group's choices; unavailable ones keep their reason. Spells open the Phase 9 composer here. |
| Card | lower left | The selected combatant: HP bar, AC, speed, conditions, cover from you, the last result; Details for the full stat block |
| Log | lower right | Collapsed to the last lines; More expands it, with every roll |
| Grid | optional (Tab) | The 5-ft grid with walls, doors, low cover and reach, for reading |

**Keys:**

| Key | Does |
|---|---|
| 1–4 | Open a group |
| Space | End turn |
| Tab | Show or hide the grid |
| Z | Turn the camera to whoever acts |
| Right-drag | Turn the camera |
| Enter | Confirm |
| Esc | Cancel, close the pop-up, or clear the preview |

**In the world:**
- Attackers lunge; ranged attacks and spells fly as projectiles.
- Damage (marked resisted or immune), healing, misses, criticals, saves, conditions, downs and deaths float above the body.
- Each is shown once, from the combat log; nothing here changes the fight.

## Phase 20A: species in combat (ADR-0103)

- **Goliath giant ancestries** go through the shared paths and spend the ancestry's uses (proficiency bonus per long rest):
  - **After a hit:** Fire's Burn and Frost's Chill (`FeatureActivation.AfterHit`), and Hill's Tumble. One boon per hit (`TurnResources.LastHitBoonUsed`); spell-attack hits count.
  - **Bonus action:** Cloud's Jaunt (`FeatureTargeting.Point`; the combat window's "Choose a square", then a grid click).
  - **Reactions:** Stone's Endurance (in `ReactToDamage`) and Storm's Thunder (in `ReactToBeingHurt`). `ReactionKind` gains 10 and 11.
- **Companions** use the after-hit boons. Computer-run goliaths decide their reactions through the shared policy.
- **Species features** sit in the combat window's **Features** group, which was "Class features" before.
- **Chill Touch** adds `CombatEffectKind.NoHealing` (22). `CombatEngine.Heal` restores nothing while it lasts.
