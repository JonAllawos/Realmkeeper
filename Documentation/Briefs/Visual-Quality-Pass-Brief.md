VISUAL-QUALITY PASS (Unity, current art)

Project: Realmkeeper
Engine: Unity 6 (6000.0.47f1) / URP
Branch: feature/phase-0-1-foundation (or a branch the owner names)
Roadmap place: an early slice of M8 (visual, audio, UX and performance polish). The approved order puts M1.2 next; this pass runs only if the owner chooses to bring it forward.
Companion brief: KitBash3D-Trial-Brief.md. This pass asks how far the current art goes with better light, materials and finish; the trial asks what new art would cost. Their reports are read side by side.
Earlier work this builds on: Briefs/GraphicsPass.md and Reports/GraphicsPass-Report.md (2026-11-10).

Status: written by Claude 2026-11-16 at the owner's request; the owner chose it first ("Visual pass"). Carried out 2026-11-16: see Reports/VisualPass-Report.md. Not committed.

⸻

PRIMARY OBJECTIVE

Make the game look clearly better at the same cameras, with the art it already has and without new gameplay. Most of the gap between "plain" and "good" in a stylized game is light, atmosphere, material finish and small detail, not polygon count.

Success is the owner's judgement of before/after screenshots, within the agreed budget.

⸻

WHAT IS ALREADY DECIDED (do not reopen)

- Budget: 60 fps at 1080p on a GTX 1660-class GPU; at most 1,500 draw calls in a town. The Graphics Pass measured at most 955 in a town and a worst p95 of 2.2 ms on an RTX 4070 Laptop GPU, so there is headroom, but not unlimited headroom on the target GPU.
- Art direction: warm stylized realism.
- URP only. No HDRP, no pipeline change.
- Packages are allowed; each gets a short ADR.
- Assets: owned and licence-clear (CC0 or similar) first, each recorded in an asset register.
- The world is generated at run time, so baked lighting does not apply. Every lighting improvement must work in real time or be computed on arrival.

⸻

RULES

1. Audit before changing: read SceneEnvironment, ExplorationAtmosphere, WeatherView, the PC and Mobile URP assets, the quality levels, and the RkSettings display options. Record findings in a preflight.
2. Presentation only. No change to simulation, saves (schema stays v28), commands, layouts or the meaning of any marker.
3. Every change is measured. Anything that costs more than it shows is reverted, and the report says so.
4. Each improvement must degrade cleanly on lower quality levels (see §6), so the Low level still runs on weak hardware.
5. Do not claim visual quality without screenshots, or development-build numbers that were not measured.
6. Testing: focused tests during the pass. Full EditMode, then PlayMode at the end, never in parallel, and only after asking the owner. Never weaken a test.
7. Preserve the uncommitted M_LampGlow.mat keyword, the Unity MCP lines in Packages/, and the untracked Assets/InitTestScene* scenes.
8. Commit locally only after verification. Do not push. Stop after the report. Do not start M1.2 from here.

⸻

0. BASELINE

- A "before" screenshot set with the capture tests (RK_CAPTURE_DIR) at the Graphics Pass cameras (gate, street, building, character, plaza, interior, overview) plus the strategic map and the overland country, at 1920×1080 and 2560×1440, under clear, overcast and rain weather, and at noon and dusk.
- Editor numbers per camera: draw calls, set-pass, triangles, frame mean and p95.
- A development build measured with -rkPerfProbe, if a build is practical in the session. Otherwise say so.

⸻

1. LIGHT

- Sun and sky: tune the sun's colour and angle across the day, and the ambient (currently Trilight) so shade is lit by the sky, not flat grey.
- Sky: replace the horizon band the Graphics Pass left (haze meeting sky at the overview) with a sky that matches the palette. A CC0 HDRI (for example from Poly Haven) is allowed; record its licence.
- Reflections: one real-time reflection probe per town, rendered once on arrival and on weather change, so wet stone and glass reflect the right sky.
- Shadows: review the cascade splits and distance for town and overland, so near shadows are sharp and far ones do not pop.
- Local light: lamps, windows and the hearth at dusk with a small, capped number of shadow-casting lights.

⸻

2. ATMOSPHERE AND POST-PROCESSING

- Fog: distance and height feel for towns and for the overland country, matched to the weather.
- Colour: a colour-grading LUT authored for the warm palette, one per view (map, town, interior, overland), blended by weather.
- Ambient occlusion: tune the existing SSAO radius and intensity at street scale.
- Anti-aliasing: compare MSAA, SMAA and TAA for shimmer on roofs, fences and foliage; pick per quality level.
- Keep the strategic map's profile unless a change is clearly better there too.

⸻

3. MATERIAL FINISH

- Consistent texel density across the kit pieces and the AmbientCG ground, so no piece looks blurry beside another.
- Detail normals or a shared grunge mask, so large walls and roofs are not uniform.
- URP decals for wear: dirt at wall bases, moss on north-facing stone, wheel ruts at gates, puddles in rain. Each decal type is placed deterministically from the layout seed.
- Wetness in rain: darker albedo and lower roughness, driven by the existing weather, not a new system.

⸻

4. SMALL DETAIL AND MOTION

- Ground: break the flat ground with grass tufts, stones and path edges, placed deterministically, instanced.
- Wind: gentle sway for grass, tree canopies, cloth on stalls and banners, through one shared shader parameter driven by the weather's wind.
- Chimney smoke and hearth sparks as small particle systems, culled by distance.

⸻

5. KNOWN GAPS FROM THE GRAPHICS PASS

Address where cheap, and list the rest:
- no plank or thatch wall modules (hamlets look like towns);
- only the tavern has an interior;
- no turn-in-place animation.

⸻

6. QUALITY LEVELS

Map every addition to the existing quality levels (RkSettings.Quality): what Low, Medium and High turn on. Low must stay close to today's cost. Each level is measured.

⸻

7. TESTS

- Existing EditMode, PlayMode and .NET suites keep passing.
- New: decal and detail placement is deterministic for the same layout; every shader found by name exists in a development build; switching quality level at run time changes what it should and leaves no stale objects; the reflection probe renders on arrival and on a weather change.

⸻

8. REPORT

Documentation/Reports/Visual-Quality-Pass-Report.md with:
- before and after screenshots at the same cameras, weather and time of day;
- numbers per camera before and after, per quality level, against the 1,500 draw-call budget;
- assets and packages added, with licences and ADRs;
- what was tried and reverted, and why;
- what still looks weak, and whether new art (see the KitBash3D trial) or more finish would fix it;
- an owner playtest checklist.

⸻

STOP

Stop after the report. Do not start M1.2 or any other milestone.
