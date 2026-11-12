# Phase 18 owner playtest checklist

**Why this exists.** Phase 18 asked for a hand-played session and short gameplay recordings. Claude did not hand-play the game and recorded no video:
- It drives Unity only through the batch test runner and editor test runs.
- No screen recorder is installed: no ffmpeg and no Unity Recorder package.

What *was* done is listed in the completion report, in four kinds that are kept apart:
- automated tests;
- automated UI interaction (PlayMode tests pressing the real controls);
- captured screenshots that Claude inspected;
- human feedback, of which there is none yet.

This list is for the owner's own session. Each step says what to look for. Expect about 60–90 minutes.

## Setup

1. Open `C:\Dev\Realmkeeper` in Unity 6000.0.47f1, then open `Assets/Realmkeeper/Scenes/Realmkeeper.unity`.
2. Press Play and start a **new world**. Phase 18 content is also added to old saves on load, once per region, but a new world exercises generation.
3. If you want to record, use Windows' own capture (Win+Alt+R through the Xbox Game Bar), or OBS if installed.
4. Save before each scenario (F5) under a new slot, so any step can be repeated.

## Scenario A: exploration (Greywater Drift, the Ringers' Vault, Hollin Tower)

- [ ] In the home town, walk the streets for a minute.
  - Trees are the modelled kit trees.
  - Where people who know each other stand together, a short overheard line appears above them and they turn to each other.
- [ ] Ask around; find the Phase 18 places through people and rumours. If none has been heard of yet, the atlas lists nearby places once discovered.
- [ ] **Greywater Drift** (a mine).
  - Read the pump drill board.
  - Work the pump *before* clearing the intake grate: the barrel should crack, and stay cracked after a save and reload.
  - Mend it with smith's tools or the pump-wright's wrench.
  - Clear the grate (Athletics; failures cost a little and can be retried).
  - Work the pump twice: FULL → HALF → DRY.
  - Check that the flooded gallery is now walkable, and that companions now follow you into it. Before draining, they refuse with "the water is too deep for them".
- [ ] **The Ringers' Vault** (a crypt).
  - Read the verse by the bells, and ring one bell out of order: grit falls (a Dexterity save) and the sequence resets.
  - Ring out of order a second time: the dead by the tomb should wake.
  - Ring the verse's order: the door opens, and stays open after a save and reload.
- [ ] **Hollin Tower** (a ruin).
  - Hold a flame to the scored lintel (the action needs a light; the shuttered lantern works).
  - The hidden way beneath it should become findable.
- [ ] Open the Hub (H), then Journal, then **Discoveries**.
  - Entries are grouped by kind; ◆ marks major ones; [unverified] marks guesses.
  - Text wraps rather than running off the panel.
- [ ] Tell a discovery to someone who cares (an elder, a guard or a merchant offers "share:" lines).
  - The reward comes once per settlement.
  - Telling the same thing again in the same town gives nothing.

## Scenario B: a quest chain

- [ ] Take **The Drowned Drift** from a labourer, smith, farmer or resident of the town nearest the drift.
  - Drain the drift (A), and come back.
  - The follow-up, **Greywater's Future**, should be offered.
  - Choose *reopen*. That is only possible once drained; check that it is refused before.
- [ ] Save, reload, and check the outcome stuck:
  - the town's prosperity or stability note;
  - the giver's regard;
  - the journal's Completed tab.
- [ ] Optional: **Ashes in the Record-House** → **The Reeve's Accounts** (Hollin Tower).
  - The true ledger is found below the reeve's step.
  - Choose among exposing the reeve, making them restore what was taken, or taking their offer.
- [ ] Optional: **The Miller's Measure** → **A Mill for Two**.
- [ ] Optional, only where two factions exist: **The Cairn Question** → **Who Keeps the Cairn**.
  - Cairn Watch's watch log can also be argued in *The Waystone Count*'s shared-count debate (provisional campaign).

## Scenario C: an emergent story

- [ ] Let days pass (Wait 1 h repeatedly, or rest) until the journal's Available tab shows one of:
  - *Room at the Hearth*;
  - *Hard Words in …*;
  - *The Long Way Round*;
  - *Word for a Friend*;
  - *Bad Blood at the Crossing*;
  - *What Became of the Caravan*.
  These come only from real events, at most one new story a day.
- [ ] Speak to the person who asks, and resolve it, or leave it for 10 days.
  - Resolved: the event it was about should end (refugees settled, unrest calmed) or change (the faction opinion moves).
  - Left alone: the offer should lapse from Available after 10 days.

## Scenario D: party

- [ ] Recruit a companion (the Party tab or conversation), and take them into the Drift.
- [ ] Notice remarks. Companions comment on:
  - major discoveries their nature cares about;
  - quest outcomes;
  - hardship in a hungry or restless town;
  - each other (banter between companions who like each other, at most once a week a pair).
- [ ] Fight the den's creatures with the tactical grid.
  - The spider, wolves and other animals should be drawn as animal models facing their direction of travel.
  - Fallen static animals (bear, boar, goat) tip onto their side.
  - The bottom place card and the room text should step aside during the fight.
- [ ] Ask a companion to wait, leave the room, come back: they regroup. Leave the place: the party leaves together.

## Scenario E: regional world

- [ ] Open the world map (M) and travel to a neighbouring region.
  - Its own Phase 18 places appear there once discovered (each region gets its own set).
- [ ] Watch for caravans and groups on the roads; inspect one.
- [ ] Look at a faction conflict, if one exists (rival or hostile factions sharing a border). The event's history should show its stage changes. From skirmishes up, weekly garrison and strain notes appear.
- [ ] Save, quit Play, press Play, load. Check that:
  - places, the drained drift, the opened vault door, discoveries and quest outcomes are all as left;
  - nothing is offered twice.

## What to send back

- For each scenario: what worked, what felt wrong (pacing, unclear text, missing feedback), and anything broken. Include the save slot if you can reproduce it.
- Screenshots or a recording of anything that looks wrong.
- This becomes the "human playtest feedback" category, which is currently empty.
