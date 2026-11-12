# ADR-0082: A campaign is data laid over the one world; its instance points at the world's own entities and quests

**Context.** Phase 17 asks for an authored campaign framework that lives inside the persistent world. It must not be a second game mode, quest engine, NPC model or political simulation, and it must not freeze the world to protect a script. The quest system (Phase 12) already has role-bound templates, stages, alternative groups, hidden, optional and timed objectives, a checked state machine, and keyed rewards and consequences. The setting's canon (kingdoms, antagonist, history) is not approved, so content must be clearly provisional.

**Decision.**
1. **Definition (`CampaignDefinition`) is data.** It is versioned (`Version`), carries its source, and has a `Provisional` flag and a plain-words `Requires`. It defines:
   - **roles:** how each is filled from the world, with deterministic rules (the home town, a road out of the home region, the far end of a road, a person in a settlement by preferred trades, a settlement's faction, someone's household, a region), plus two dynamic kinds that are read when asked (`FactionLeader`, `FirstAvailable`);
   - **quests:** existing quest definitions, with their roles filled from campaign roles;
   - **chapters** and **arcs**;
   - **decisions** (ADR-0083) and **endings**;
   - a **final arc**.
2. **Validation before entry.** `CampaignValidator` checks:
   - missing references (roles, quests, dialogues, arcs, chapters, decisions, items);
   - duplicate IDs;
   - circular prerequisites, and arcs waiting on a later chapter;
   - arcs that can never finish, and quests nothing starts;
   - decisions with no choices, choices that change nothing, decisions with no unconditional choice;
   - dialogue graphs, including decision choices named in dialogue;
   - endings without a fallback.

   `CampaignCatalog.Register` admits a definition only if it validates. A rejected one is kept in `Rejected` with its errors, and the start command reports them.
3. **The instance (`CampaignInstanceData`) lives in `WorldData.Campaigns`.** It holds:
   - its bindings (role → entity ID) and its state;
   - chapter and arc progress, and objective progress;
   - decisions, pending consequences and applied keys;
   - flags, its quest map (key → quest instance ID), and a bounded history (80 lines).

   It never holds a copy of a person, settlement, faction or quest.
4. **State machines are checked.**
   - Campaign: Active, Suspended, Completed, Failed or Abandoned. Closed states are final, so re-reading an old objective can never reopen a finished campaign.
   - Arc: Locked, Active, Completed, Failed or Abandoned.
   - Chapter: Locked, Active or Completed.
5. **Arc objectives reuse existing machinery; there is no second objective engine.** An objective is one of:
   - a linked quest reaching Completed (optionally with a given outcome);
   - a decision made (optionally a given choice);
   - a world-state condition read by the shared evaluator.

   Stages and alternative groups work as they do for quests.
6. **Starting.** `StartCampaignCommand` checks compatibility in plain words:
   - the definition is valid;
   - it has not already been played here;
   - every role can be cast;
   - the start conditions hold.

   It then binds roles to the world as it stands (rewriting nothing), opens the first chapter, and offers its quests through the ordinary quest lifecycle, with source key `campaign:{campaign}:{key}`. Campaign quests are never pruned.
7. **Sandbox by default.** A world with no campaign is open-ended. Nothing needs a campaign, and the daily pass still offers the world's own stories (ADR-0085).
8. **Content is provisional.** The one campaign, *The Waystone Count*, is labelled provisional demonstration content in the definition and in the journal. It names only bound world entities and establishes no canon.

**Consequences.**
- Campaign content is authored as data and validated. Adding a campaign needs no simulation change unless it needs a new condition or effect kind.
- A campaign can begin in an old world (when it has a road out of the home region). It says plainly why it cannot when it cannot.
- A save from a newer campaign version is refused by the world validator. An instance whose definition is missing from the build is kept unchanged and cannot advance.

**Alternatives considered.** A campaign as one large quest (rejected: it cannot hold parallel arcs, chapters and lasting decisions). A scripting language (rejected by the brief: untyped). Campaign-only copies of people (rejected: the world must stay the one truth).
