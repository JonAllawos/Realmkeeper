using System.Collections.Generic;
using Realmkeeper.Exploration.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 5: the light follows the world's clock (<c>WorldData.MinuteOfDay</c>). The sun rises at
    /// 06:15 and sets at 19:30 on an east–west arc, warm and low at golden hour; after sunset it sinks
    /// below the horizon (the procedural sky darkens with it) and a cool, faint moon takes over. Ambient
    /// light, haze and exposure follow. From dusk the street lamps and door lanterns light (real point
    /// lights) and windows glow. Pure function of the minute: the same time always looks the same.
    /// </summary>
    public sealed partial class ExplorationAtmosphere
    {
        public const int Sunrise = 6 * 60 + 15;
        public const int Sunset = 19 * 60 + 30;
        public const float NoonElevation = 54f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly Color NightSky = new Color(0.09f, 0.11f, 0.19f);
        private static readonly Color NightEquator = new Color(0.07f, 0.08f, 0.12f);
        private static readonly Color NightGround = new Color(0.035f, 0.035f, 0.045f);
        private static readonly Color DuskEquator = new Color(0.66f, 0.47f, 0.36f);
        private static readonly Color DuskHaze = new Color(0.82f, 0.6f, 0.46f);
        private static readonly Color NightHaze = new Color(0.05f, 0.06f, 0.09f);
        private static readonly Color Golden = new Color(1f, 0.58f, 0.32f);
        private static readonly Color LampColour = new Color(1f, 0.72f, 0.42f);
        private static readonly Color LampEmission = new Color(2.2f, 1.5f, 0.8f);
        private static readonly Color WindowEmission = new Color(1.25f, 0.78f, 0.36f);

        private readonly List<Light> _lamps = new List<Light>();
        private readonly List<Material> _clones = new List<Material>();
        private ColorAdjustments _colour;
        private Material _glass;
        private Material _glow;
        private Material _sky;

        public Light Moon { get; private set; }

        /// <summary>The minute last applied, or −1.</summary>
        public int AppliedMinute { get; private set; } = -1;

        /// <summary>Sun elevation in degrees at the applied minute (negative below the horizon).</summary>
        public float SunElevation { get; private set; }

        /// <summary>0 at full night, 1 in full daylight.</summary>
        public float Daylight { get; private set; }

        /// <summary>0 by day, 1 when the lamps are fully lit.</summary>
        public float LampLevel { get; private set; }

        public IReadOnlyList<Light> Lamps => _lamps;

        /// <summary>
        /// After the environment is built: the moon, lamp lights at the street lamps and door lanterns, and
        /// per-visit copies of the window, lantern and sky materials (so night never edits the art assets).
        /// </summary>
        public void PrepareNight(Transform environmentRoot, LocalEnvironmentLayout layout, ArtCatalog catalog)
        {
            if (environmentRoot == null || catalog == null) return;
            var moonGo = new GameObject("Moon");
            moonGo.transform.SetParent(environmentRoot, false);
            moonGo.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
            Moon = moonGo.AddComponent<Light>();
            Moon.type = LightType.Directional;
            Moon.color = new Color(0.56f, 0.66f, 0.96f);
            Moon.intensity = 0f;
            Moon.shadows = LightShadows.None;

            _glass = Clone(catalog.Material("WindowGlass"));
            _glow = Clone(catalog.Material("LampGlow"));
            if (_glass != null) _glass.EnableKeyword("_EMISSION");
            var original = new Dictionary<Material, Material>();
            if (_glass != null) original[catalog.Material("WindowGlass")] = _glass;
            if (_glow != null) original[catalog.Material("LampGlow")] = _glow;
            foreach (var r in environmentRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                    if (mats[i] != null && original.TryGetValue(mats[i], out var copy)) { mats[i] = copy; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }

            if (RenderSettings.skybox != null && RenderSettings.skybox == catalog.Material("Sky"))
            {
                _sky = Clone(RenderSettings.skybox);
                RenderSettings.skybox = _sky;
            }

            var lamps = new GameObject("Night lamps").transform;
            lamps.SetParent(environmentRoot, false);
            foreach (var p in layout.Props)
            {
                if (p.Kind != PropKind.LampPost) continue;
                var rot = Quaternion.Euler(0f, -p.Yaw, 0f);
                AddLamp(lamps, new Vector3(p.Position.X, 0f, p.Position.Z) + rot * new Vector3(0f, 2.6f, -0.55f), 10f);
            }

            foreach (var b in layout.Buildings)
            {
                var plan = BuildingKitPlanner.Plan(b, layout.Type, layout.SettlementId.ToString());
                var rot = Quaternion.Euler(0f, -b.Yaw, 0f);
                foreach (var piece in plan.Pieces)
                {
                    if (piece.ModuleId != "@Lantern") continue;
                    AddLamp(lamps, new Vector3(b.Center.X, 0f, b.Center.Z) + rot * new Vector3(piece.X, piece.Y - 0.2f, piece.Z - 0.35f), 7f);
                }
            }
        }

        private void AddLamp(Transform parent, Vector3 position, float range)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LampColour;
            light.range = range;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            _lamps.Add(light);
        }

        private Material Clone(Material source)
        {
            if (source == null) return null;
            var m = new Material(source) { name = source.name + " (visit)" };
            _clones.Add(m);
            return m;
        }

        /// <summary>The sun's elevation (degrees) at a minute of the day: an arc between sunrise and sunset, below the horizon at night.</summary>
        public static float ElevationAt(int minute)
        {
            if (minute >= Sunrise && minute <= Sunset)
                return NoonElevation * Mathf.Sin(Mathf.PI * (minute - Sunrise) / (Sunset - Sunrise));
            var fromHorizon = minute > Sunset ? minute - Sunset : Sunrise - minute;
            return -Mathf.Min(18f, fromHorizon * 0.36f);
        }

        /// <summary>Compass yaw of the sun: from the east-south-east at sunrise round to the west-south-west at sunset.</summary>
        public static float AzimuthAt(int minute)
        {
            var t = (minute - Sunrise) / (float)(Sunset - Sunrise);
            return -110f + 220f * t;
        }

        /// <summary>Applies the lighting of a minute of the day (0–1439).</summary>
        public void SetTime(int minute)
        {
            minute = Mathf.Clamp(minute, 0, 1439);
            AppliedMinute = minute;
            var elevation = ElevationAt(minute);
            SunElevation = elevation;
            Daylight = Mathf.Clamp01((elevation + 4f) / 16f);
            var night = Mathf.Clamp01(-elevation / 10f);
            var golden = Mathf.Clamp01(1f - Mathf.Abs(elevation - 3f) / 14f) * (elevation > -4f ? 1f : 0f);
            LampLevel = Mathf.Clamp01((6f - elevation) / 10f);

            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Euler(Mathf.Max(elevation, -20f), AzimuthAt(minute), 0f);
                var lift = Mathf.Clamp01(elevation / 14f);
                Sun.intensity = 1.55f * lift;
                Sun.color = Color.Lerp(Golden, SunColour, Mathf.Clamp01((elevation - 3f) / 22f));
                Sun.shadows = lift > 0.02f ? LightShadows.Soft : LightShadows.None;
            }

            if (Moon != null)
            {
                Moon.intensity = 0.24f * night;
                Moon.shadows = night > 0.6f ? LightShadows.Soft : LightShadows.None;
                Moon.shadowStrength = 0.55f;
            }

            var day = Mathf.Clamp01(elevation / 12f + 0.15f);
            var sky = Color.Lerp(NightSky, AmbientSky, day);
            var equator = Color.Lerp(Color.Lerp(NightEquator, AmbientEquator, day), DuskEquator, golden * 0.55f);
            var ground = Color.Lerp(NightGround, AmbientGround, day);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = Mathf.Lerp(0.95f, 1.15f, day);
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(equator.linear);
            probe.AddDirectionalLight(Vector3.up, (sky - equator).linear, 0.9f);
            probe.AddDirectionalLight(Vector3.down, (ground - equator).linear * 0.6f, 0.9f);
            RenderSettings.ambientProbe = probe;

            RenderSettings.fogColor = Color.Lerp(Color.Lerp(NightHaze, Haze, day), DuskHaze, golden * 0.6f);
            RenderSettings.fogDensity = Mathf.Lerp(0.0062f, 0.0048f, day);
            if (_sky != null && _sky.HasProperty("_Exposure")) _sky.SetFloat("_Exposure", Mathf.Lerp(0.18f, 1f, Mathf.Clamp01(day + golden * 0.3f)));
            if (_colour != null) _colour.postExposure.Override(Mathf.Lerp(0.2f, 0.35f, day));

            foreach (var lamp in _lamps)
            {
                if (lamp == null) continue;
                lamp.enabled = LampLevel > 0.01f;
                lamp.intensity = 2.4f * LampLevel;
            }

            if (_glow != null) _glow.SetColor(EmissionColor, LampEmission * (0.12f + 1.4f * LampLevel));
            if (_glass != null) _glass.SetColor(EmissionColor, WindowEmission * (0.85f * LampLevel));
        }

        private void RestoreNight()
        {
            foreach (var m in _clones)
            {
                if (m == null) continue;
                if (UnityEngine.Application.isPlaying) Object.Destroy(m);
                else Object.DestroyImmediate(m);
            }

            _clones.Clear();
            _lamps.Clear();
            _glass = _glow = _sky = null;
            _colour = null;
            Moon = null;
            AppliedMinute = -1;
        }
    }
}
