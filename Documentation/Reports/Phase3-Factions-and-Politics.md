# Phase 3 report: factions and the political engine

**Project:** Realmkeeper · **Branch:** `feature/phase-0-1-foundation` · **Date:** 2026-11-10 · **Commits:** `5c89f8a` (Phase 3) and `d82a01b` (playtest fixes), both pushed. Local and origin
are at `d82a01b`.

## 1. Summary

Phase 3 turns Realmkeeper's two placeholder factions into a working political world:
- **Factions:** organized groups of seven types, each with an identity, leaders, a following,
  influence, a treasury and goals.
- **Governance and claims:** factions govern or administer settlements, establish a presence
  in them, and claim territory.
- **Relationships:** factions relate to each other through opinion, trust, rivalry and formal
  agreements.

All of this is simulated weekly inside the existing daily transaction, and is driven by the
Phase 2 economy (stability, prosperity, trade). The player can inspect every part of it in the
HUD and on the map, and can intervene through nine typed commands. Each intervention is recorded
in the chronicle as the player's.

**Verified:**
- the .NET harness, and Unity EditMode and PlayMode, all pass;
- a playtest by you confirmed that entering and walking a city work.

**Not finished:** the visuals are still placeholder art. They're covered by the separate
graphics-pass brief (section 14).

## 2. Pre-flight findings

| Check | Expected (brief) | Found |
|---|---|---|
| Branch | `feature/phase-0-1-foundation` | Same |
| HEAD | `f988a05` | `f988a05`, working tree clean |
| Origin | One commit behind | **Level with `f988a05`.** You had authorized that push the turn before. |
| Baseline tests | 174 / 174 / 37 | .NET harness 174/174 before any change; Unity matched the earlier report |
| Existing factions | — | A two-field model (ID and name); each settlement referenced its governing faction; residents carried an affiliation; generation made "The Crown of …" and "Free Folk of …". **I extended this model rather than building a parallel one.** |

## 3. What was built

### Factions
- **Types:** Kingdom, Noble house, Guild, Religious organization, Military order, Mercenary
  company, Criminal organization. They share one model and differ only by a data profile:
  influence weights, membership scale, dues, and whether they can govern, administer or claim.
- **Status:** Active → Declining (influence < 20; back to Active at 25) → Dormant (≤ 5 and
  governing nothing) → Dissolved (after 26 dormant weeks). **A dissolved faction is never
  deleted:** it gives up what it holds but stays in the world, so history keeps its references.
- **Leaders:** existing named residents, never new people. The religious order starts with its
  leadership deliberately vacant.
- **Membership:** aggregate counts per settlement, never one NPC per member. It's kept within
  the population every day, both per faction and in total.
- **Treasury:** dues are real coin moved from settlements, only from coin above a 30-day
  reserve; 3% upkeep a week.

### Governance, presence and claims
- **Governance:** each settlement has a governing authority (or is independent), and optionally
  a noble house administering it for that authority.
- **Presence:** any number of factions can be present (strength, members, Established or Waning).
- **Claims:** they go through Unresolved → Recognized / Contested → Relinquished or Fulfilled.
  **A claim never moves governance by itself.**

### Relationships
- **Dimensions:** opinion, trust, rivalry and an agreement (trade agreement or mutual support).
- **States:** six readable states derived from them (Hostile, Rival, Wary, Neutral, Friendly,
  Allied), checked in a fixed order so they never contradict.
- **Causes:** every change records what moved it most (agreement, shared prosperity,
  competition, territorial dispute, political support, broken agreement, your intervention).

### Goals
- **Structure:** structured data with a type, target, progress, priority, status and a reason
  when closed.
- **Progress:** comes only from world conditions, never from chance.
- **Effect:** goals make factions act, for example opening a branch or filing a claim.

### Player actions (typed commands through the existing registry)
1. Set opinion, and establish or break an agreement.
2. File a territorial claim.
3. Recognize, contest or withdraw a claim.
4. Establish a presence.
5. Remove a presence.
6. Change governance.
7. Create a goal.
8. Change a goal's priority.
9. Abandon a goal.

Each one validates every reference, fails without changing anything, and is chronicled with
`source = player`. The HUD shows those entries as "By your hand: …".

### Interface
- **Factions list** (left navigation): colour swatch, name, type and influence.
- **Faction inspector:**
  - description, status, founding date and leader;
  - following and treasury;
  - influence with its **"Why" breakdown**;
  - goals with progress;
  - presence, holdings and claims;
  - relationships, with ±10 opinion buttons;
  - recent history.
- **Settlement inspector, Politics section** (below the Phase 2 economy, which is unchanged):
  - governor, administrator and affiliated population;
  - each present faction's share;
  - open claims;
  - action buttons behind **Manage**.
- **Political map overlay** (top-bar "Politics" toggle, off by default): a thin ring on the
  ground in the governor's colour, and dashed rings for claims.
- **Exploration HUD:** names who governs the settlement, from the same data the map uses.

## 4. Architecture decisions

| Decision | Reason |
|---|---|
| Extend `FactionData`; keep `SettlementData.FactionId` as the governor | No duplicate faction system; existing saves and code keep working |
| One `PoliticalSystem` at execution order 450 | It runs after the economy and population, before chronicle milestones, inside the daily transaction, so a failed day discards politics too |
| Daily membership clamp; full evaluation weekly on day numbers divisible by 7 | Cheap every day; results don't depend on how days are batched; pausing runs no days |
| Rules as data (`PoliticalRules`), formulas as pure functions (`PoliticalMath`) | Explanations shown in the UI are computed by the same functions the simulation uses |
| Events carry `FactionIds` and a `source` fact | Stable faction references survive dissolution; player and simulation changes are told apart |
| A deterministic, once-only `PoliticalInitializer` | New worlds and upgraded old saves get identical political structures; nothing is ever duplicated |

## 5. Initial world (default seed)

| Faction | Type | Governs | Present in | Leader |
|---|---|---|---|---|
| The Crown of Bramhaven | Kingdom | Bramhaven (town), Mossholm (village) | all three | the town elder |
| Free Folk of Westerthorpe | Noble house (existing faction, typed by rule) | Westerthorpe (hamlet) | Westerthorpe | the hamlet elder |
| House Aldane | Noble house | administers Mossholm for the crown | Mossholm, Bramhaven | the village elder |
| Bramhaven Traders' Guild | Guild | — | Bramhaven, Mossholm | the town merchant |
| Order of the Open Hand | Religious organization | — | all three | vacant |

- **Agreements:** Crown ↔ Guild trade agreement; Crown ↔ House Aldane mutual support (Allied).
- **Goals:**
  - the crown wants to keep control and to secure a claim on Westerthorpe;
  - the guild and the order want more members;
  - the guild wants a presence in Westerthorpe;
  - House Aldane wants more influence and better relations with the crown.
- **The world size is unchanged:** one region, three settlements.

**After a simulated year** (same seed):
- The Crown reached influence 82.
- The order reached 50, with 348 members.
- The crown's claim on Westerthorpe was filed and is still unresolved.
- No border moved.

## 6. Formulas, in short

- **Influence target:** Σ weight × score ÷ 100, from seven scores (0–100), each measured from a
  different quantity:
  - membership;
  - own treasury;
  - population governed or administered;
  - presence strength;
  - local economy (prosperity, or trade for trade-following types);
  - stability where it holds authority;
  - opinion of others.

  It moves at most 2 points a week.
- **Membership target:** population × the type's maximum share × strength × support, where
  support is the settlement's stability where the faction governs (hardship erodes support).
  It moves at most 1% of the population a week.
- **Relationship targets:**
  - rivalry comes from shared settlements between competitors and from claims;
  - opinion is the sum of agreement, shared prosperity, political support, −rivalry/2 and
    territorial disputes.

  Opinion moves 2 a week, rivalry 3.

The full tables and thresholds are in `Documentation/Systems/Politics.md`.

## 7. Saves and migration

- **Schema:** schema v5 adds faction fields, relations, claims, settlement administrators and
  event faction IDs.
- **Old saves:**
  - they keep their factions' IDs and names;
  - the initializer fills in the political structure once, identical to a fresh world of that seed;
  - no settlement, resource, route or event changes;
  - a world with no factions gets none invented.
- **Tests:** they check that a v4 copy loads identical to a fresh v5 world, that save/load is
  exact, and that a reloaded world continues identically.

## 8. Tests

| Suite | Before | After | Result |
|---|---|---|---|
| .NET harness | 174 | 279 (+105) | **279 passed, 0 failed, 0 skipped** |
| Unity EditMode (batch mode) | 174 | 279 | **279 passed, 0 failed, 0 skipped** |
| Unity PlayMode (batch mode) | 37 | 43 (+6) | **43 passed, 0 failed, 0 skipped** |
| Unity compilation | — | — | 0 errors (one test-only compile error on the first run, fixed) |

**What the new tests cover**

| Test file | Cases | Covers |
|---|---|---|
| `PoliticalSimulationTests` | 48 | Influence, dues, membership, presence, relationships, goals, claims, status, cadence, determinism, atomicity |
| `PoliticalWorldTests` | 57 | 37 validator cases; generation; persistence and migration; every command; integration (economy → politics, exploration unchanged, pause); a simulated year; performance |
| `PoliticsPlayModeTests` | 5 | Faction list and inspector; settlement section; overlay; HUD buttons; the HUD following the simulation |
| `RealScenePlayModeTests` | 1 | Loads the shipped scene, sends a real mouse click on a city, enters and walks |

**Mutation checks:** I broke five things on purpose.
- Four were caught immediately.
- The fifth (the simulation overriding claims that were already decided) needed a new test,
  which now catches it.

## 9. Playtest findings and fixes (`d82a01b`)

| You reported | Cause | Fix |
|---|---|---|
| "Can't walk the cities" | At the very high frame rates the town scene reaches, each step was below Unity's `CharacterController.minMoveDistance` (1 mm) and was dropped. The old tests fixed frame time at 1/30 s, which hid it. | `minMoveDistance = 0`. The new real-scene test walks at about 9,000 fps. |
| "Can't click into cities" | The Game view's Scale slider was at 1.5×, which cropped the whole HUD off-screen. The click itself worked. | Scale set to 1×; nothing to change in code |
| "Graphics look horrendous" | Big floating overlay squares; a wrapped faction list; a cramped politics section | Ground rings, a one-line list, compact rows, actions behind Manage. The underlying placeholder art remains (section 14). |

You then confirmed: "aside from graphics, works well."

## 10. Performance (.NET 8 Release, Windows; not measured in Unity)

| World | Measurement |
|---|---|
| Default (3 settlements, 5 factions, 10 relations) | 30 days of all systems: 6.3 ms; one political tick: 22 µs; 84 KB allocated per day |
| Synthetic (30 settlements, 120 factions, 7,140 relations) | one political tick: 54.5 ms; 30 days of politics, with daily copy and validation: 304 ms |

Relationship cost grows with factions² × settlements. Nothing beyond these sizes is claimed.

## 11. Known limitations

- Placeholder art in towns and for characters; the map markers are simple (section 14).
- Map overlays find shaders by name; they're unverified in a player build.
- Old saves don't gain founding events for factions in their chronicle.
- Creating goals and changing their priority have no HUD controls yet (commands and tests exist).
- Out of scope, as briefed: warfare, rebellion, diplomacy trees, NPC political AI, religion
  and crime detail, multiple regions.

## 12. Files and systems changed

67 files, about 6,900 lines added, across two commits:

| Layer | Files |
|---|---|
| Domain | `Politics.cs` (new), `ClaimId`, `GoalId`, faction and event types, settlement administrator, world relations and claims, validator |
| Simulation | `PoliticalRules`, `PoliticalMath`, `PoliticalSystem` (new); registration; faction-aware events |
| Application | `PoliticalQueries`, `PoliticalCommands` (new); session queries; event wording |
| Infrastructure | `PoliticalInitializer` (new); save DTOs, mapper, v4→v5 migration |
| Presentation | `RealmkeeperHud.Politics` and `PoliticalOverlayLayer` (new); HUD layout and styles; bootstrap; map markers; screenshot tour |
| Exploration | Governing faction in the HUD; the `minMoveDistance` fix |
| Docs | `Systems/Politics.md` (new), Economy, Persistence, Roadmap, Test results |

**Screenshots:** `Documentation/Screenshots/realmkeeper-*.png`. These were taken before the
visual fixes and still show the old square overlay. Retake them with F10 for current visuals.

## 13. Git

| | |
|---|---|
| Branch | `feature/phase-0-1-foundation` |
| Commits | `5c89f8a` Phase 3: factions and political engine · `d82a01b` Phase 3 playtest fixes |
| Origin | Pushed; local and origin at `d82a01b` |
| Uncommitted | `Documentation/Briefs/GraphicsPass.md`, roadmap update, this report |

## 14. Next: the graphics pass (brief written, not started)

`Documentation/Briefs/GraphicsPass.md` now records your answers:

| Decision | Your answer | Still to confirm |
|---|---|---|
| Art source | Mix: procedural assembly with a small kit of hand-made or CC0 pieces | — |
| Packages | Any package the work needs is allowed (each gets a short ADR for the record) | — |
| Budget | "Both": targets apply in the editor and in a development build | The numbers. Proposed: 60 fps at 1080p on a mid-range GPU, at most 1,500 draw calls per town |
| Art direction | Unclear | Proposed: warm stylized realism, continuing the map's look. You'd confirm after the first before/after shots of one street |

No Phase 4 or graphics work has started.
