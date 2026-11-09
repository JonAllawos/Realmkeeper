# ADR-0001: Unity 6, URP and UI Toolkit

**Context.** The product needs a stylized-realistic 3D isometric world on desktop, a
data-heavy interface, and a long maintenance life.

**Decision.** Unity 6 with the Universal Render Pipeline, and UI Toolkit (UXML/USS) for the
application interface. No ECS/DOTS, networking, cloud services or AI frameworks in this
phase. Editor version: **6000.0.47f1** (chosen 2026-11-09, when the template project was
created on the development PC).

**Consequences.** URP gives good-looking lighting (shadows, fog, post-processing) on modest
hardware. UI Toolkit suits the panel-heavy layout and styles like CSS. `ProjectSettings/`
and `Packages/` are committed only after Unity has generated them.

**Alternatives.** HDRP: heavier, with little benefit for an orthographic strategy view.
uGUI: more mature for in-world UI, weaker for large data panels. It can still be added later
for world-space labels.
