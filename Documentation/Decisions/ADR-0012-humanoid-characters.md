# ADR-0012: Humanoid characters whose look derives from identity

**Status.** Accepted, 2026-11-10 (graphics pass).

**Context.** The residents and the player were capsules. The graphics pass needs humanoid
bodies with basic animation. ADR-0009 requires that presentation never owns identity: a
resident is its `NpcId` in world data, and views are keyed by it.

**Decision.**

1. **Appearance is derived, not stored.** `NpcAppearanceResolver` (in `Exploration.Domain`,
   engine-free) maps an NPC's ID, role and given name to an `NpcAppearance`:
   - body frame, clothed body and outfit texture;
   - hair and beard;
   - skin tone, hair colour and cloth tint;
   - height.

   The same NPC always looks the same, and nothing about the look is saved. Role decides the
   outfit: guards wear leather armour, elders and merchants finer dress, labourers work clothes.
   The given name decides the body frame, using the generator's list of feminine names. Phase 4
   identity data may replace this hint.
2. **One skeleton, parts bound by bone name.** The CC0 outfits are headless bodies on a shared
   skeleton.
   - The setup cuts head-only meshes out of the base characters by bone weight.
   - `HumanoidCharacterBuilder` instantiates the body, then rebinds the head, hair and beard to
     the body's bones by name.
   - It tints skin, hair and cloth with material property blocks.
   - If a body's own avatar is invalid (Unity rejects Female_Peasant's auto-mapping), it uses
     another body's avatar from the same frame.
3. **Animation follows real state.** `CharacterAnimationDriver` measures the owner's planar
   speed and turn rate each frame and feeds a 1D blend: idle 0, walk 1.6, jog 3.6 and
   sprint 6 m/s. A successful `TalkToResidentCommand` plays the talking state for a few seconds.
   Turning on the spot shows as slow steps, because the CC0 library has no turn clip. The
   driver moves nothing.
4. **One body per identity.** `ExplorationController` keeps its dictionary keyed by `NpcId`. The
   resident view holds the ID, role and name read from the session, and its appearance. The
   body itself is anonymous.
5. **Fallback.** Without a catalog, the capsule bodies remain.

**Consequences.** Saves are unaffected. A rename or role change shows on the next build of the
scene. The mapping is pinned by a test, because changing it changes how everyone looks.
Characters cost about 10 draws each. With fewer than 20 on screen the animators always animate,
which avoids a bind-pose frame when one is first seen.

**Alternatives considered.**
- Storing appearance in `NpcData`: deferred to Phase 4, which may add identity fields; the
  resolver can then read them.
- One prefab per role: no individuality.
- Root motion: movement belongs to the character controller and the resident wander logic,
  not to animation.
