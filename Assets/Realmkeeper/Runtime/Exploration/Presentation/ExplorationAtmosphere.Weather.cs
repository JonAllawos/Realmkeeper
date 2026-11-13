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
        private Season _season = Season.Summer; // Phase 21 assets: the season the leaves show
        private static readonly string[] LeafWords = { "leaves", "leaf" };
        private static readonly string[] EvergreenWords = { "pine", "cherry" };
        private readonly List<Surface> _surfaces = new List<Surface>();

        private Texture2D _snowTexture;

        private struct Surface
        {
            public Material Material;
            public Color Base;
            public float Smoothness;
            public bool Roof;
            public bool Foliage;
            public Texture Texture;
            public Color LeafColour; // foliage: the colour the leaves show in summer (texture × tint), measured once
            public Texture LeafMask; // foliage: the kit's white texture of the same leaves, coloured outright by season
        }

        /// <summary>The kit's white texture for green-painted leaves ("Leaves_Birch_C" → "Leaves_Birch"), or null.</summary>
        private Texture LeafMaskFor(Texture painted)
        {
            if (painted == null || _catalog == null || !painted.name.EndsWith("_C", System.StringComparison.Ordinal)) return null;
            return _catalog.LeafMask(painted.name.Substring(0, painted.name.Length - 2));
        }

        /// <summary>
        /// The mean colour of a texture where it is opaque (gamma space), read back from the GPU so the texture need not be
        /// readable. The kits paint their leaves green into the texture, so a season's colour is reached by dividing by this.
        /// </summary>
        private static Color MeanColour(Texture t)
        {
            if (t == null) return Color.white;
            const int n = 32;
            var rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var active = RenderTexture.active;
            Graphics.Blit(t, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(n, n, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, n, n), 0, 0);
            read.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            float r = 0f, g = 0f, b = 0f, w = 0f;
            foreach (var p in read.GetPixels())
            {
                if (p.a < 0.5f) continue;
                r += p.r; g += p.g; b += p.b; w += 1f;
            }

            Object.Destroy(read);
            return w < 1f ? Color.white : new Color(r / w, g / w, b / w, 1f);
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
            _season = s == null ? Season.Summer : s.Season;
            if (s != null) SetSeason(s.Sunrise, s.Sunset, s.NoonElevation);
            AdoptScene(); // what was streamed in since (the countryside's trees) takes the weather and the season too
            ApplySurfaces();
            if (AppliedMinute >= 0) SetTime(AppliedMinute);
        }

        private Transform _weatherRoot;
        private ArtCatalog _catalog;
        private readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
        private readonly HashSet<Material> _copySet = new HashSet<Material>();

        /// <summary>After the environment is built: the weather view and per-visit copies of the ground and roof materials.</summary>
        private void PrepareWeather(Transform environmentRoot, ArtCatalog catalog)
        {
            _catalog = catalog;
            _weather = WeatherView.Create(environmentRoot);
            _weather.MoonLight = Moon;
            _weatherRoot = environmentRoot;
            Adopt(environmentRoot);
        }

        /// <summary>Every renderer in the exploration scene (the town and what was streamed in around it).</summary>
        private void AdoptScene()
        {
            if (_weatherRoot == null) return;
            foreach (var go in _weatherRoot.gameObject.scene.GetRootGameObjects()) Adopt(go.transform);
        }

        /// <summary>Gives the renderers under <paramref name="root"/> the per-visit copies of their ground, roof and leaf materials.</summary>
        private void Adopt(Transform root)
        {
            var copies = _copies;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var roof = Matches(r.gameObject.name, RoofWords);
                var ground = !roof && Matches(r.gameObject.name, GroundWords);
                var mats = r.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || _copySet.Contains(m) || !m.HasProperty("_BaseColor")) continue;
                    var isRoof = roof || Matches(m.name, RoofWords);
                    var isGround = ground || (!isRoof && Matches(m.name, GroundWords));
                    var isFoliage = !isRoof && Matches(m.name, LeafWords) && !Matches(m.name, EvergreenWords);
                    if (isFoliage) isGround = false;
                    if (!isRoof && !isGround && !isFoliage) continue;
                    if (!copies.TryGetValue(m, out var copy))
                    {
                        copy = Clone(m);
                        copies[m] = copy;
                        _copySet.Add(copy);
                        _surfaces.Add(new Surface
                        {
                            Material = copy, Base = m.GetColor("_BaseColor"), Smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.15f, Roof = isRoof,
                            Texture = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null, Foliage = isFoliage,
                            LeafColour = isFoliage ? Mul(m.GetColor("_BaseColor"), MeanColour(m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null)) : Color.white,
                            LeafMask = isFoliage ? LeafMaskFor(m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null) : null,
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

        /// <summary>
        /// The season in the broadleaf trees' leaves (the leaves' texture is kept; its tint moves): fresh in spring, as made in
        /// summer, gold and russet in autumn, dull and dusted with snow in winter. Pines and cherry blossom keep their colour.
        /// </summary>
        private void Leaves(Surface s)
        {
            // The colour the leaves should show, then the tint that makes the (green-painted) texture show it.
            var leaf = s.LeafColour;
            var want = leaf;
            switch (_season)
            {
                case Season.Spring: want = new Color(leaf.r * 0.92f, leaf.g * 1.08f, leaf.b * 0.9f); break;
                case Season.Autumn: want = Color.Lerp(leaf, new Color(0.86f, 0.42f, 0.1f), 0.8f); break;
                case Season.Winter: want = Color.Lerp(Color.Lerp(leaf, new Color(0.45f, 0.4f, 0.3f), 0.65f), SnowWhite, _snowCover * 0.5f); break;
            }

            if (_season == Season.Summer)
            {
                // As made.
                if (s.LeafMask != null) s.Material.SetTexture("_BaseMap", s.Texture);
                s.Material.SetColor("_BaseColor", s.Base);
                return;
            }

            if (s.LeafMask != null)
            {
                // The kit's white leaves, coloured outright (a green-painted texture cannot be tinted grey or white).
                s.Material.SetTexture("_BaseMap", s.LeafMask);
                s.Material.SetColor("_BaseColor", new Color(want.r, want.g, want.b, s.Base.a));
                return;
            }

            var c = new Color(s.Base.r * Ratio(want.r, leaf.r), s.Base.g * Ratio(want.g, leaf.g), s.Base.b * Ratio(want.b, leaf.b), s.Base.a);
            s.Material.SetColor("_BaseColor", c);
        }

        private static float Ratio(float want, float have) => Mathf.Clamp(want / Mathf.Max(have, 0.04f), 0f, 12f);

        private static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, 1f);

        /// <summary>Wet ground is darker and glossier; lying snow whitens the ground and, more, the roofs.</summary>
        private void ApplySurfaces()
        {
            foreach (var s in _surfaces)
            {
                if (s.Material == null) continue;
                if (s.Foliage) { Leaves(s); continue; }
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
            _copies.Clear();
            _copySet.Clear();
            _weatherRoot = null;
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
