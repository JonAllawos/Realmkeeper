# ADR-0098: One design system, and a shared UI assembly

**Status.** Accepted, 2026-11-12 (Phase 19).

**Context.**
- **Two stylesheets.** Realmkeeper's interface had grown phase by phase in two UI Toolkit stylesheets, with about 420 literal colours (180 distinct), two copies of the same button, and no icons.
- **Default controls.** Unity's light default text fields and dropdowns showed on a dark interface.
- **Invisible tooltips.** The tooltips written since Phase 2 were never visible, because UI Toolkit shows `tooltip` only in the Editor.
- **Scaling.** At 1366×768, panel scaling shrank 14 px text to about 10 px.
- **Visual direction.** The owner chose direction A ("Cartographer's Ledger") from the Phase 19A research.

**Decision.**
1. **Tokens.** Design tokens are USS custom properties on `:root` (`RealmkeeperTokens.uss`). Shared components and restyled built-in controls live in `RealmkeeperComponents.uss`. The theme imports both, so every document inherits them. Screens use `var(--rk-*)` and the shared classes.
2. **Shared assembly.** A new assembly, `Realmkeeper.UI`, has no game references and is used by both presentation assemblies and the tests. It holds:
   - `RkIcons`: icons by meaning, tinted. A control's icon is its own background image, so the control keeps measuring its text.
   - `RkTooltips`: runtime tooltips.
   - `RkModal`: the confirmation dialog.
   - `RkSettings` and `RkSettingsWindow`: the settings.
3. **One shared runtime panel.** A runtime copy of the panel settings is shared by every document, so the interface scale applies everywhere and the asset is never edited at run time. Its Auto scale enlarges the interface on screens up to 1000 px tall.
4. **No second screen manager.** Windows keep their own roots. The title screen is an overlay over the strategic view; it opens when the game launches with the main scene and from the map's Menu button.
5. **Never colour alone.** Used combat resources, closed roads, unknown land, warnings and destructive actions each carry an icon, a shape or words as well as colour.
6. **Destructive actions are confirmed in words:**
   - deleting a world ("cannot be undone");
   - reloading a save ("everything since will be lost");
   - founding a new world over a running one.

**Consequences.**
- **Restyling is cheaper.** A screen can be restyled by changing tokens or shared classes.
- **Remaining literals.** 188 one-off literal colours remain in the screen stylesheets, mostly combat-grid and politics accents. `Tools/UI/tokenize_uss.py` lists them, and they are folded in as those screens are touched.
- **Tests.** Tests can find a control's icon (`RkIcons.LeadOf`).
- **Changed test.** The session test that pressed Load now confirms the dialog. That is a deliberate change of behaviour, not a regression.

**Alternatives considered.**
- **uGUI or a third-party UI framework:** rejected (ADR-0001: UI Toolkit for data-heavy panels).
- **A screen-manager rewrite:** rejected. The existing class-per-window model works, and the brief forbids a second system.
- **SVG icons through the Vector Graphics package:** rejected. It is a preview package in Unity 6.0; tinted PNGs give the same result.
