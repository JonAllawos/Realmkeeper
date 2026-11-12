# Phase 15: Companions, Parties, Relationships & Party Progression — completion report

Date: 2026-11-12. Base: `4324084` (pushed). Phase 15, including the art and animation addendum (`Phase15-Art-Animation-Report.md`), is **committed locally and not pushed**. Phase 16 has not been started.

## 1. Summary

Named people from the generated world can now travel with the player:

- They are recruited in conversation, or refuse with a stated reason.
- They come for a few days or for good, and can be sent home and asked again.
- The party holds 3 active members and 3 in reserve, with follow, wait, regroup, formation order and a leader.
- In town they follow in one body each and keep up without teleporting. In places they appear beside the player.
- They have full sheets from the existing rules engine and their own item instances.
- They fight beside the player through the one combat engine, with a companion layer of the one AI that logs its reasons.
- Their loyalty moves only from real events, and they leave if it falls too low.
- One personal quest through the existing quest engine.
- The party rests together.
- Party UI: the HUD strip, Hub → Party, and the combat HUD.
- Saved as schema v17, with a migration from v16.

Verification:

| Suite | Result |
|---|---|
| .NET | 714/714 (23 new) |
| Unity EditMode | 714/714 |
| Unity PlayMode | 134: 107 passed, 0 failed, 27 skipped (captures; they need RK_CAPTURE_DIR) |
| Compile warnings | 0 (.NET and Unity) |

8 party captures plus a performance pass.

## 2. Repository state and preflight

- Branch `feature/phase-0-1-foundation` = `origin` at `4324084` before this work.
- The owner's `ProjectSettings/GraphicsSettings.asset` change is preserved and not staged.
- `Documentation/Screenshots/dev-*.png` and `Assets/InitTestScene*` are left alone.
- The Quaternius Bestiary stays git-ignored and unreferenced by committed assets.
- The preflight audit (`Phase15-Preflight-Audit.md`) found what to reuse and what to build:
  - **Reuse:** people, sheets, regard, relations, memories, items, combat, the AI, rest and quests.
  - **Build:** party model, recruitment rules, companion items, AI layer, party rest, loyalty, personal quest, following in settlements, UI, save v17.

## 3. Architecture and decisions

| ADR | Decision |
|---|---|
| ADR-0073 | Companions are the world's own `NpcData`. `PartyData` and `CompanionRecordData` record only membership and history. Gear becomes item instances once. Active members are taken out of their schedule (`PlacePresence.IsAway`). Capacities and thresholds are data. Commands only. |
| ADR-0074 | Loyalty moves only from real events, once per cause, with its reason. Departure below 15, return at 35. The personal quest is an ordinary bound quest. |
| ADR-0075 | The party joins fights as ordinary participants. A companion layer (`CombatAi.ForCompanion`) sits in front of `CombatAi.Next` and records reasons (`CombatLogKind.Reason`). Resting together uses the one `CharacterRest`. |

No second combat, NPC, inventory, quest or rest system was created.

New code:
- **Domain:** `Party.cs`, `WorldValidator.Party.cs`, `CombatAi.Companions.cs`, `QuestCatalog.Phase15.cs`.
- **Simulation:** `Party/CompanionLoyalty.cs` (with `PartySystem`, order 485).
- **Application:** `Party/PartyCommands.cs`, `PartyCombat.cs` (with `PartyAftermath`), `PartyRest.cs`, `PartyQueries.cs`.
- **Infrastructure:** `WorldSaveMapper.Party.cs`.
- **Presentation:** `CompanionFollower.cs`, `ExplorationController.Party.cs`, `PlayerHubWindow.Party.cs`.

## 4. Movement

- **Exploration:** free real-time movement. No grid, no movement budget, no weight or hunger slowdown (`SpeedMultiplier = 1`, art addendum).
- **Combat:** tactical squares only inside the engine.
- **Companions:** walk (1.9 m/s), jog (3.8 m/s) or run (6.4 m/s, or the player's speed + 0.4) to keep their slot, as `CharacterController` bodies with gravity.

## 5. Recruitment

Refusal causes, each said in words (full order in `Systems/Party.md`):
- dead, under 16, escorted, posted elsewhere, not here, or in a fight;
- left over an unmended grievance;
- low familiarity, trust or liking, or high hostility;
- knows an unsettled crime of the player's;
- their faction counts the player an enemy;
- for a permanent arrangement only: a post, low courage, or their own open request;
- a full party.

Joining:
- A sheet is made once, by `NpcCharacterGenesis` and their archetype.
- Their gear becomes item instances once.
- Loyalty starts at 40 plus a third of their liking.
- A memory and a chronicle line record it.

Other paths:
- **Temporary arrangements** (3 days) end at the first midnight after their term.
- **Dismissal** keeps their record and items.
- **Rejoining** never grants twice.

## 6. Party management and following

- **Party commands:** `SetFollowModeCommand` (waiting stores settlement, or place and room), `RegroupPartyCommand`, `SetPartyRoleCommand`, `MovePartyMemberCommand` (renumbered without gaps) and `SetPartyLeaderCommand`.
- **Settlements:**
  - One `CompanionFollower` per active member with the player. Their resident body is removed first, and restored where they stood when they leave.
  - Formation slots alternate sides, further back by order. Followers step aside from each other and the player.
  - Recovery happens only beyond 35 m, or after 3 s of trying to move without moving. It goes to clear ground behind the player and is counted and logged.
  - In the tests and captures: 0 recoveries.
- **Places:** followers are drawn beside the player; a waiting member stays in their room.
- **Talking:** a companion can be spoken to (E) at any time; present party members count as "beside" the player for conversation.

## 7. Sheets and items

- **Sheets:** the person inspector view (`NpcCharacterView`) now uses a companion's own items. Hub → Party shows:
  - species, class, subclass, level and XP;
  - HP, AC with its breakdown, speed, proficiency;
  - abilities, saves, skills, features, spells, resources.
- **Items:** `TransferItemCommand` moves the same instance between the player and a companion, or between companions. Equipped and bound items are refused.
- **Equipment:** `CompanionEquipCommand` and `CompanionUnequipCommand` follow the player's slot rules (rings, two-handed weapons, curses). The response notes armour they are untrained in, or that is too heavy for them.
- **Validation:** the validator checks one holder per stack across player, companions, containers and shops.

## 8. Combat and the companion AI

- **Joining fights:** active members with the player join every fight (practice, outings, place fights) on free squares beside the player, never twice.
- **State persists:** sheets are used live, so HP, slots and conditions carry over, and experience comes by the existing keyed awards.
- **The AI layer, in order:**
  1. heal a dying or badly hurt ally;
  2. first aid for a dying friend (going to them);
  3. Second Wind at half HP;
  4. Rage;
  5. defend the player;
  6. otherwise the baseline, explained.

  Deterministic: tested on the same state twice. The engine validates every proposal.
- **Reasons** are logged and shown in the combat log, for example "Maud Redmayne: heals Elric Brooke (1/10 HP)".
- **Aftermath:**
  - left on the ground on a withdrawal: −20;
  - stabilised by the player: +5;
  - killed: leaves the party.

## 9. Relationships, loyalty and the personal quest

- **Crimes they saw** are weighed by their own traits: discipline and compassion make them heavier, greed lighter, and greed with low discipline can even approve.
- **Every change** is keyed once, with its reason. Changes of 5+ are chronicled. Liking moves at half the rate through the existing reputation ledger.
- **Departure** below 15 happens after the fight, not during it. Return needs 35.
- **"Old Ground"** is an original template, offered once at loyalty 60+ after a full day together. It is bound to the companion and a place near their home. Done: +25.
- **Dialogue:** "Travel with me", "Come with me for a few days" and "It's time we parted ways" are in everyone's general conversation, through two new consequences and one new condition.

## 10. Rest

- The player's rest applies the same rest to each member present, once each (keyed per person), with the once-a-day rule.
- A companion spends hit dice only as far as needed.
- The nightly NPC self-rest skips active members. Before this fix it rested them on its own, which then blocked the party rest: it was found and fixed during capture.

## 11. UI

- **HUD strip**, lower left: HP bar, state and loyalty word. Click it to open the member. Hidden while a window is open.
- **"Party (K)"** button by the clock. K was chosen because P is pause.
- **Hub → Party:**
  - cards with Wait here or Follow me, To reserve or Call up, Move up, Lead, Talk and Part ways;
  - the selected member's sheet, loyalty with its last five reasons, personal quest, items and the player's pack;
  - former companions.
- **Combat HUD:** allies in the initiative strip; reasons in the log, in green italics.

## 12. Save, migration, validation

- **Schema v17:** the `party`, `companions`, and a person's `ownsItems`, `inventory` and `equipment`.
- **Migration:** `MigrationV16ToV17` adds an empty party and enables the party system after quests. Older saves (v1–v16) still migrate through the chain. The test helpers gained `StripToV16`, and every older strip now passes through it.
- **Validator:**
  - unique, living, known members, never escorted;
  - capacity, and the leader is a member;
  - waiting members wait somewhere;
  - records match membership, with approvals counted once;
  - items held once, in slots they fit.

## 13. Testing

- **.NET:** 714 passed, 0 failed (691 before Phase 15; 22 party tests + 1 healer-reason test). Two existing tests were updated for the new daily system: the documented system order, and a v4 save that strips through v16.
- **Unity EditMode:** 714/714 passed (the shared test sources), 0 warnings.
- **Unity PlayMode:** 134 tests: 107 passed, 0 failed, 27 skipped (captures, run separately with RK_CAPTURE_DIR: both Phase 15 party captures passed). Includes 3 new party flows:
  - recruited in dialogue, one body each, keeping up walking and sprinting, spaced apart, 0 recoveries;
  - the Party section's sheet and items, and Wait, Follow and Part ways buttons as commands, with the resident body back after parting;
  - a fight beside the player, the exploration body hidden, the reasons shown in the HUD log.

The brief's scenarios, as tests (`Tests/EditMode/Party/Phase15PartyTests.cs` unless noted):

| # | Scenario | Test |
|---|---|---|
| 1 | Recruit once, sheet, own items, no duplicates | `Recruiting_AWillingPerson_…` |
| 2 | Refusals with causes, no change | `Refusals_NameTheirCause_…`, `SomeoneWithAPost_…` |
| 3 | Temporary term ends | `ATemporaryArrangement_…` |
| 4 | Dismiss and rejoin, nothing granted twice | `Dismissed_TheyKeepWhatTheyCarry_…` |
| 5 | Capacity, reserve, schedule | `Capacity_ThreeTravel_…` |
| 6 | Follow, wait, regroup | `Waiting_HoldsTheirPlace_…` |
| 7 | Formation and leader | `Formation_OrderAndLeader_…` |
| 8 | Fights beside the player | `Companions_FightBesideThePlayer_…` |
| 9 | Explained, deterministic decisions | `CompanionDecisions_AreExplained_…`, `ADyingFriend_IsTendedFirst`, `AHealer_HealsTheHurt_…` |
| 10 | Fight state persists | `WhatHappensInAFight_StaysWithThem` |
| 11 | Abandonment costs loyalty once | `WithdrawingAndLeavingThemDying_…` |
| 12 | Witnessed crime by traits, once | `ACrimeTheyWitness_…` |
| 13 | Departure and return | `BelowTheirLimit_TheyLeave_…` |
| 14 | Personal quest | `ATrustingCompanion_AsksTheirOwnQuest_…` |
| 15 | Party rest once | `RestingTogether_…` |
| 16 | Items, one owner, equip rules | `Items_MoveBetweenOwners_…` |
| 17 | Exact save/load | `SaveAndLoad_KeepTheWholeParty_Exactly` |
| 18 | v16 migration | `AVersion16Save_Loads_…` |
| 19 | Validator | `TheValidator_CatchesBrokenParties` |
| 20 | Dialogue recruit, refusal and dismiss | `InConversation_…`; PlayMode `Phase15PartyPlayModeTests` (3 UI flows) |

## 14. Performance and screenshots

Performance, in town, scripted walking, 240 frames each (`Capture_Phase15PartyPerformance`):
- 5.34 ms/frame alone and 2.86 ms/frame with three followers (p95 5.95 / 3.30).
- The second measurement ran after warm-up, so the honest reading is **no measurable cost** from three followers, not a speed-up.
- `GetParty` takes about 0.56 ms; it runs on change and once a second.
- 0 recoveries.

Screenshots in `Documentation/Screenshots/Phase15/` (captions in `p15_party_capture_log.txt`), all automated captures. No manual play-through was done.

| File | Shows |
|---|---|
| `p15_party_01_dialogue_recruit.jpg` | "Travel with me" and "Maud Redmayne joins you." |
| `p15_party_02_exploration.jpg` | Three companions in formation behind the player (2.6 / 3.8 / 4.9 m), the HUD strip |
| `p15_party_03_party_ui.jpg` | Hub → Party: line-up, commands, the priest's sheet |
| `p15_party_04_companion_sheet.jpg` | The cleric's sheet: abilities, saves, skills, features, spells |
| `p15_party_05_inventory.jpg` | Her own items (chain shirt, mace, shield worn; holy symbol) and the player's pack with Give |
| `p15_party_06_combat.jpg` | A practice fight with the party (allies and foes) |
| `p15_party_07_companion_spell.jpg` | She casts Healing Word on Elric (1/10 HP); logged reason "heals Elric Brooke (1/10 HP)"; Cast motion |
| `p15_party_08_party_rest.jpg` | A long rest together: her spell slot back, the others rested |

## 15. Known limitations, documentation and Git

**Limitations (deferred, not hidden):**
- No direct control of companions' turns.
- One personal quest template.
- No companion banter, and no companion-to-companion relationships beyond the existing person-to-person relations.
- Followers in places stand with the player rather than walking room routes (escorts still use Phase 14 routes).
- Charged items do not recharge for companions on a long rest.
- Party members are not drawn on the strategic map.
- The leader is presentation only.
- A waiting member's body in town keeps its spot only while the town is loaded. On re-entry, members waiting in this settlement appear behind the player's arrival point.

**Documentation:**
- ADR-0073, ADR-0074, ADR-0075 and the ADR index;
- `Systems/Party.md`;
- `Systems/Persistence.md` (v17);
- `Systems/Economy.md` (daily order);
- `Architecture/DataOwnership.md`;
- the Game Design Bible's implementation status (Phases 12–15, companions);
- the Product Foundation: the conflict recorded as superseded for this feature only;
- the roadmap: Phases 14 and the combat overhaul corrected to pushed; the art assets; Phase 15; Phase 16 needs authorisation.
- Credits: unchanged. Phase 15 adds no third-party assets; the KayKit packs were credited when installed.

**Art addendum:** see `Phase15-Art-Animation-Report.md`. It is committed together with Phase 15, because the brief folds it in.

**Git:** one local commit of Phase 15 work only. `ProjectSettings/GraphicsSettings.asset`, `Assets/InitTestScene*` and the ignored Bestiary are excluded. **Not pushed.**
