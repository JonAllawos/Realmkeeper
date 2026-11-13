using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 21 (ADR-0105): the season's daylight and the simulated weather in the street — the sun's hours and height from
    /// the calendar, cloud dimming the sun and greying the light, haze thickening in rain, snow and fog, wet ground darker
    /// and glossier, lying snow whitening ground and roofs, and the <see cref="WeatherView"/> (rain, snow, clouds, stars,
    /// lightning, sound). It shows what the world decided; nothing here changes the world.
    /// </summary>
    public sealed partial class ExplorationAtmosphere
    {
        private static readonly Color OvercastGrey = new Color(0.6f, 0.62f, 0.65f);
        private static readonly Color SnowWhite = new Color(0.9f, 0.92f, 0.95f);
        private static readonly string[] GroundWords = { "terrain", "ground", "plaza", "road", "street", "path", "grass", "cobble", "dirt", "gravel" };
        private static readonly string[] RoofWords = { "roof", "thatch", "slate", "shingle" };

        private int _sunrise = Sunrise;
        private int _sunset = Sunset;
        private float _noon = NoonElevation;
        private WeatherView _weather;
        private WeatherLook _look = WeatherLook.Fair;
        private float _groundWet;
        private float _snowCover;
        private readonly List<Surface> _surfaces = new List<Surface>();

        private Texture2D _snowTexture;

        private struct Surface
        {
            public Material Material;
            public Color Base;
            public float Smoothness;
            public bool Roof;
            public Texture Texture;
        }

        /// <summary>A soft white with faint blue-grey drifts, tiling (snow lying on a textured surface replaces its texture).</summary>
        private Texture2D SnowTexture()
        {
            if (_snowTexture != null) return _snowTexture;
            const int size = 128;
            _snowTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "rk-snow", wrapMode = TextureWrapMode.Repeat };
            var random = new System.Random(4409);
            var px = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var drift = 0.5f + 0.5f * Mathf.Sin(x * 0.098f + Mathf.Sin(y * 0.071f) * 2.2f) * Mathf.Sin(y * 0.049f + 1.3f);
                var grain = (float)random.NextDouble() * 0.05f;
                var v = 0.86f + 0.1f * drift + grain;
                px[y * size + x] = new Color(v * 0.97f, v * 0.985f, v, 1f);
            }

            _snowTexture.SetPixels(px);
            _snowTexture.Apply(true);
            return _snowTexture;
        }

        public WeatherView Weather => _weather;
        public WeatherLook CurrentLook => _look;
        public int SunriseMinute => _sunrise;
        public int SunsetMinute => _sunset;

        /// <summary>The season's daylight (from <see cref="SeasonRules"/>); re-applies the current minute.</summary>
        public void SetSeason(int sunrise, int sunset, float noonElevation)
        {
            if (sunrise == _sunrise && sunset == _sunset && Mathf.Approximately(noonElevation, _noon)) return;
            _sunrise = Mathf.Clamp(sunrise, 3 * 60, 11 * 60);
            _sunset = Mathf.Clamp(sunset, 14 * 60, 22 * 60);
            _noon = Mathf.Clamp(noonElevation, 20f, 75f);
            if (AppliedMinute >= 0) SetTime(AppliedMinute);
        }

        /// <summary>The sun's elevation at a minute under the season's daylight.</summary>
        public float Elevation(int minute)
        {
            if (minute >= _sunrise && minute <= _sunset)
                return _noon * Mathf.Sin(Mathf.PI * (minute - _sunrise) / (_sunset - _sunrise));
            var fromHorizon = minute > _sunset ? minute - _sunset : _sunrise - minute;
            return -Mathf.Min(18f, fromHorizon * 0.36f);
        }

        public float Azimuth(int minute) => -110f + 220f * (minute - _sunrise) / (_sunset - _sunrise);

        /// <summary>How a simulated weather looks (the presentation's reading of it).</summary>
        public static WeatherLook LookFor(WeatherSummary s)
        {
            if (s == null) return WeatherLook.Fair;
            var i = Mathf.Clamp01(s.Intensity / 100f);
            var look = new WeatherLook { Cloud = Mathf.Clamp01(s.Cloud / 100f), WindKph = s.WindKph };
            if (WeatherWords.IsRain(s.Kind)) look.Rain = Mathf.Max(0.22f, i);
            if (WeatherWords.IsSnow(s.Kind)) look.Snow = Mathf.Max(0.22f, i);
            if (s.Kind == WeatherKind.Thunderstorm) { look.Storm = true; look.Rain = Mathf.Max(look.Rain, 0.8f); }
            if (s.Kind == WeatherKind.Fog) look.Fog = Mathf.Max(0.3f, i);
            look.Fog = Mathf.Max(look.Fog, Mathf.Max(look.Rain, look.Snow) * 0.3f);
            return look;
        }

        /// <summary>Shows the simulated weather (null: fair). Re-applies the current minute so light and haze follow at once.</summary>
        public void SetWeather(WeatherSummary s)
        {
            _look = LookFor(s);
            _groundWet = s == null ? 0f : Mathf.Clamp01(s.GroundWetness / 70f);
            _snowCover = s == null ? 0f : Mathf.Clamp01(s.SnowCover / 60f);
            if (s != null) SetSeason(s.Sunrise, s.Sunset, s.NoonElevation);
            ApplySurfaces();
            if (AppliedMinute >= 0) SetTime(AppliedMinute);
        }

        /// <summary>After the environment is built: the weather view and per-visit copies of the ground and roof materials.</summary>
        private void PrepareWeather(Transform environmentRoot)
        {
            _weather = WeatherView.Create(environmentRoot);
            _weather.MoonLight = Moon;
            var copies = new Dictionary<Material, Material>();
            foreach (var r in environmentRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var roof = Matches(r.gameObject.name, RoofWords);
                var ground = !roof && Matches(r.gameObject.name, GroundWords);
                var mats = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.HasProperty("_BaseColor")) continue;
                    var isRoof = roof || Matches(m.name, RoofWords);
                    var isGround = ground || (!isRoof && Matches(m.name, GroundWords));
                    if (!isRoof && !isGround) continue;
                    if (!copies.TryGetValue(m, out var copy))
                    {
                        copy = Clone(m);
                        copies[m] = copy;
                        _surfaces.Add(new Surface
                        {
                            Material = copy, Base = m.GetColor("_BaseColor"), Smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.15f, Roof = isRoof,
                            Texture = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null,
                        });
                    }

                    mats[i] = copy;
                    changed = true;
                }

                if (changed) r.sharedMaterials = mats;
            }
        }

        private static bool Matches(string name, string[] words)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.ToLowerInvariant();
            foreach (var w in words) if (n.Contains(w)) return true;
            return false;
        }

        /// <summary>Wet ground is darker and glossier; lying snow whitens the ground and, more, the roofs.</summary>
        private void ApplySurfaces()
        {
            foreach (var s in _surfaces)
            {
                if (s.Material == null) continue;
                var wet = s.Roof ? _groundWet * 0.6f : _groundWet;
                var snow = _snowCover * (s.Roof ? 1f : 0.85f);
                Color c;
                if (snow >= 0.45f)
                {
                    // Lying snow: its own white surface (a tint cannot whiten a textured surface), brighter as it deepens.
                    if (s.Material.HasProperty("_BaseMap")) s.Material.SetTexture("_BaseMap", SnowTexture());
                    c = SnowWhite * Mathf.Lerp(0.82f, 1f, snow);
                }
                else
                {
                    if (s.Material.HasProperty("_BaseMap")) s.Material.SetTexture("_BaseMap", s.Texture);
                    c = s.Base * Mathf.Lerp(1f, 0.68f, wet);
                    // A dusting: paler and greyer.
                    var grey = (c.r + c.g + c.b) / 3f;
                    c = Color.Lerp(c, new Color(grey, grey, grey) * 1.35f, snow * 1.4f);
                }

                c.a = s.Base.a;
                s.Material.SetColor("_BaseColor", c);
                if (s.Material.HasProperty("_Smoothness")) s.Material.SetFloat("_Smoothness", snow >= 0.45f ? 0.22f : Mathf.Lerp(s.Smoothness, 0.62f, wet * (1f - snow)));
            }
        }

        /// <summary>The weather's share of a minute's light (called at the end of <see cref="SetTime"/>).</summary>
        private void ApplyWeatherToLight(float day, float night)
        {
            var cloud = _look.Cloud;
            var wet = Mathf.Max(_look.Rain, _look.Snow);
            var dull = Mathf.Clamp01(cloud * cloud * 1.1f + wet * 0.35f);

            if (Sun != null)
            {
                Sun.intensity *= Mathf.Lerp(1f, 0.22f, dull);
                Sun.shadowStrength = Mathf.Lerp(0.82f, 0.2f, dull);
                if (dull > 0.85f) Sun.shadows = LightShadows.None;
            }

            if (Moon != null) Moon.intensity *= Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(cloud * 1.2f));

            // Overcast light is flat and grey; lying snow lifts the light from below.
            var greyDay = OvercastGrey * Mathf.Lerp(0.12f, 1f, day);
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, greyDay, dull * 0.7f);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, greyDay * 0.95f, dull * 0.6f);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, SnowWhite * Mathf.Lerp(0.1f, 0.7f, day), _snowCover * 0.55f);
            var probe = new UnityEngine.Rendering.SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientEquatorColor.linear);
            probe.AddDirectionalLight(Vector3.up, (RenderSettings.ambientSkyColor - RenderSettings.ambientEquatorColor).linear, 0.9f);
            probe.AddDirectionalLight(Vector3.down, (RenderSettings.ambientGroundColor - RenderSettings.ambientEquatorColor).linear * 0.6f, 0.9f);
            RenderSettings.ambientProbe = probe;

            // Haze: rain and snow close the distance in; fog closes it most.
            RenderSettings.fogDensity *= 1f + 1.3f * wet + 3.6f * _look.Fog + 0.25f * dull;
            // The haze takes the brightness of the dulled day (a pale haze over a dark rainy street reads as cut-outs).
            var hazeGrey = OvercastGrey * Mathf.Lerp(0.1f, 1f, day) * Mathf.Lerp(1f, 0.62f, wet) * Mathf.Lerp(1f, 0.85f, dull);
            if (_look.Fog > wet) hazeGrey = Color.Lerp(hazeGrey, OvercastGrey * Mathf.Lerp(0.12f, 1.08f, day), _look.Fog); // fog itself glows pale
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, hazeGrey, Mathf.Max(dull * 0.75f, _look.Fog));

            // The sky: greyer under cloud; at night a deep blue rather than black, for the stars and moon to sit in.
            if (_sky != null)
            {
                if (_sky.HasProperty("_Exposure"))
                {
                    var e = _sky.GetFloat("_Exposure") * Mathf.Lerp(1f, 0.6f, dull);
                    _sky.SetFloat("_Exposure", Mathf.Max(e, 0.34f * night * (1f - dull * 0.6f) + 0.12f));
                }

                // (A thick procedural atmosphere scatters to sunset orange; cloud greys the tint instead and thins the blue.)
                if (_sky.HasProperty("_AtmosphereThickness")) _sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1f, 0.55f, night));
                if (_sky.HasProperty("_SkyTint")) _sky.SetColor("_SkyTint", Color.Lerp(Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.62f, 0.62f, 0.62f), dull), new Color(0.2f, 0.28f, 0.55f), night));
                if (_sky.HasProperty("_GroundColor")) _sky.SetColor("_GroundColor", Color.Lerp(new Color(0.37f, 0.35f, 0.34f), RenderSettings.fogColor, Mathf.Max(dull, night)));
            }

            if (_colour != null) _colour.postExposure.Override(_colour.postExposure.value - 0.15f * dull);

            // A dark storm brings the lamps on early.
            if (dull > 0.75f && day > 0f) LampLevel = Mathf.Max(LampLevel, 0.35f * dull);

            if (_weather != null)
            {
                var look = _look;
                look.Night = night;
                look.Daylight = day;
                _weather.Show(look);
            }
        }

        private void RestoreWeather()
        {
            if (_weather != null)
            {
                if (UnityEngine.Application.isPlaying) Object.Destroy(_weather.gameObject);
                else Object.DestroyImmediate(_weather.gameObject);
            }

            _weather = null;
            _surfaces.Clear(); // the copies are in _clones and destroyed with them
            if (_snowTexture != null)
            {
                if (UnityEngine.Application.isPlaying) Object.Destroy(_snowTexture);
                else Object.DestroyImmediate(_snowTexture);
            }

            _snowTexture = null;
            _look = WeatherLook.Fair;
            _groundWet = _snowCover = 0f;
            _sunrise = Sunrise;
            _sunset = Sunset;
            _noon = NoonElevation;
        }
    }
}
