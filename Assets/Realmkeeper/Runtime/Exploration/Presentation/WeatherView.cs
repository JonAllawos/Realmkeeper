using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Realmkeeper.UI;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>What the weather looks like at a moment, for the presentation (all 0–1 except the wind).</summary>
    public struct WeatherLook
    {
        public float Rain;
        public float Snow;
        public float Fog;
        public float Cloud;
        public float WindKph;
        public bool Storm;

        /// <summary>0 by day … 1 at full night.</summary>
        public float Night;

        /// <summary>0 at full night … 1 in full daylight.</summary>
        public float Daylight;

        public static WeatherLook Fair => new WeatherLook { Cloud = 0.2f };
    }

    /// <summary>
    /// Phase 21 (ADR-0105): the weather drawn around the camera in a settlement — rain and snow, a moving cloud layer, stars
    /// and the moon on clear nights, lightning in a storm, and the sound of rain, wind and thunder. Presentation only: it
    /// shows the weather the world simulation decided (<see cref="Realmkeeper.Application.WeatherSummary"/>) and decides
    /// nothing. Everything is generated in code (no third-party weather asset; the owner's decision to keep weather inside
    /// Unity), and owned and destroyed with the view.
    /// </summary>
    public sealed class WeatherView : MonoBehaviour
    {
        // The cloud layer is a flattened dome: 110 m overhead (near enough to see through the haze), 560 m out to the horizon
        // (inside the exploration camera's 600 m far plane), so overcast reaches the horizon with no edge.
        private const float CloudHeight = 110f;
        private const float CloudRadius = 560f;
        private const float CloudRepeat = 420f; // metres per repeat of the cloud texture
        private const float StarRadius = 60f;   // inside the haze, above the roofs (see the remarks on Create)

        private readonly List<Object> _owned = new List<Object>();
        private ParticleSystem _rain;
        private ParticleSystem _snow;
        private ParticleSystem _stars;
        private Transform _moon;
        private Material _moonMat;
        private Material _starMat;
        private Material _rainMat;
        private Material _snowMat;
        private MeshRenderer _clouds;
        private Material _cloudMat;
        private Texture2D _cloudTex;
        private float _cloudCoverBuilt = -1f;
        private Light _lightning;
        private AudioSource _rainAudio;
        private AudioSource _windAudio;
        private AudioSource _thunderAudio;
        private WeatherLook _look = WeatherLook.Fair;
        private Vector2 _cloudDrift;
        private float _nextFlash = -1f;
        private float _flashStart = -1f;
        private float _thunderAt = -1f;

        /// <summary>The moon's light, whose direction the moon's disc follows (may be null).</summary>
        public Light MoonLight { get; set; }

        /// <summary>The camera the weather falls around (the exploration camera; it is not tagged as the main camera).</summary>
        public Camera Follow { get; set; }

        private Camera Viewer()
        {
            if (Follow != null && Follow.isActiveAndEnabled) return Follow;
            if (Camera.main != null) return Camera.main;
            Camera best = null;
            foreach (var c in Camera.allCameras) if (c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
            return best;
        }

        /// <summary>The lightning's brightness now (0–1), for anything that wants to flash with it.</summary>
        public float Flash { get; private set; }

        public WeatherLook Look => _look;
        public int RainParticles => _rain != null ? _rain.particleCount : 0;
        public int SnowParticles => _snow != null ? _snow.particleCount : 0;

        /// <summary>Takes away the rain and snow already falling (a jump in time; weather shown afresh).</summary>
        public void Clear()
        {
            if (_rain != null) _rain.Clear(true);
            if (_snow != null) _snow.Clear(true);
        }

        /// <summary>For tests and diagnostics: the precipitation systems' state.</summary>
        public string Describe() =>
            $"rain {(_rain != null ? $"{_rain.isPlaying}/{_rain.isEmitting} {_rain.emission.rateOverTime.constant:0}/s {_rain.particleCount}" : "-")}, " +
            $"snow {(_snow != null ? $"{_snow.isPlaying}/{_snow.isEmitting} {_snow.emission.rateOverTime.constant:0}/s {_snow.particleCount} at {_snow.transform.position}" : "-")}";

        /// <summary>
        /// Builds the view under <paramref name="parent"/>. Stars are drawn on a 60 m shell above 12° of elevation: far
        /// enough to sit behind anything near, near enough that the street haze does not swallow them, and high enough not to
        /// cross the roofs of buildings beyond them.
        /// </summary>
        public static WeatherView Create(Transform parent)
        {
            var go = new GameObject("Weather");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<WeatherView>();
            v.Build();
            return v;
        }

        private void Build()
        {
            var dot = Own(SoftDot(32));
            _rainMat = Own(ParticleMaterial(dot, new Color(0.78f, 0.82f, 0.9f, 0.42f)));
            _snowMat = Own(ParticleMaterial(dot, new Color(1f, 1f, 1f, 0.9f)));
            _starMat = Own(ParticleMaterial(dot, new Color(0.9f, 0.93f, 1f, 1f)));
            _moonMat = Own(ParticleMaterial(Own(MoonDisc(96)), new Color(0.92f, 0.93f, 0.86f, 1f)));

            _rain = Precipitation("Rain", _rainMat, true);
            _snow = Precipitation("Snow", _snowMat, false);
            _stars = Stars();
            _moon = Quad("Moon", _moonMat, 5.5f);
            _clouds = Dome();
            _cloudMat = Own(ParticleMaterial(null, Color.white));
            if (_cloudMat.HasProperty("_Cull")) _cloudMat.SetFloat("_Cull", 0f); // seen from inside
            _clouds.sharedMaterial = _cloudMat;

            var flash = new GameObject("Lightning");
            flash.transform.SetParent(transform, false);
            flash.transform.rotation = Quaternion.Euler(62f, 30f, 0f);
            _lightning = flash.AddComponent<Light>();
            _lightning.type = LightType.Directional;
            _lightning.color = new Color(0.82f, 0.86f, 1f);
            _lightning.intensity = 0f;
            _lightning.shadows = LightShadows.None;
            _lightning.enabled = false;

            _rainAudio = Loop("Rain sound", Own(NoiseClip("rk-rain", 2.0f, 0.55f, 0f, 11)));
            _windAudio = Loop("Wind sound", Own(NoiseClip("rk-wind", 4.0f, 0.06f, 0.6f, 23)));
            _thunderAudio = Loop("Thunder sound", Own(ThunderClip(37)));
            _thunderAudio.loop = false;
        }

        /// <summary>Shows a weather. Cheap to call often; the cloud texture is only redrawn when the cover changes by a step.</summary>
        public void Show(WeatherLook look)
        {
            _look = look;
            SetRate(_rain, look.Rain * 2600f);
            SetRate(_snow, look.Snow * 900f);
            var cover = Mathf.Round(look.Cloud * 10f) / 10f;
            if (!Mathf.Approximately(cover, _cloudCoverBuilt)) RebuildClouds(cover);
            if (!look.Storm) { _nextFlash = -1f; Flash = 0f; if (_lightning != null) _lightning.enabled = false; }
            Apply(Time.time);
        }

        private void Update()
        {
            var cam = Viewer();
            if (cam == null) return;
            var p = cam.transform.position;

            // The rain and snow fall in a box over the camera, a little upwind so the slant brings it overhead.
            var wind = new Vector3(_look.WindKph / 3.6f * 0.35f, 0f, _look.WindKph / 3.6f * 0.12f);
            if (_rain != null) _rain.transform.position = p + Vector3.up * 14f - wind * 0.6f;
            if (_snow != null) _snow.transform.position = p + Vector3.up * 10f - wind * 4f;
            if (_stars != null) _stars.transform.position = p;

            // The cloud layer drifts with the wind and is pinned to the world (it does not slide with the camera).
            _cloudDrift += new Vector2(wind.x, wind.z) * (Time.deltaTime * 3f);
            if (_clouds != null)
            {
                // The dome rides with the camera; its texture is offset by the camera's position, so the clouds stay put.
                _clouds.transform.position = p;
                _cloudMat.mainTextureScale = Vector2.one;
                _cloudMat.mainTextureOffset = new Vector2((p.x + _cloudDrift.x) / CloudRepeat, (p.z + _cloudDrift.y) / CloudRepeat);
                if (_cloudMat.HasProperty("_BaseMap"))
                {
                    _cloudMat.SetTextureScale("_BaseMap", _cloudMat.mainTextureScale);
                    _cloudMat.SetTextureOffset("_BaseMap", _cloudMat.mainTextureOffset);
                }
            }

            if (_moon != null && MoonLight != null)
            {
                _moon.position = p - MoonLight.transform.forward * 55f;
                _moon.rotation = Quaternion.LookRotation(_moon.position - p);
            }

            Apply(Time.time);
        }

        private void Apply(float now)
        {
            var day = _look.Daylight;
            var clear = 1f - Mathf.Clamp01(_look.Cloud * 1.15f);

            // Clouds: white and thin on a fair day, grey and heavy in rain, dark at night.
            var grey = Mathf.Lerp(0.96f, 0.5f, Mathf.Clamp01(_look.Cloud * _look.Cloud + Mathf.Max(_look.Rain, _look.Snow) * 0.5f));
            var lit = Mathf.Lerp(0.07f, 1f, day);
            SetColour(_cloudMat, new Color(grey * lit, grey * lit, (grey * 1.02f + 0.02f) * lit, Mathf.Clamp01(0.25f + _look.Cloud * 0.9f)));

            // Stars and the moon: at night, through the gaps in the cloud.
            var stars = _look.Night * clear * (1f - _look.Fog);
            SetColour(_starMat, new Color(0.9f, 0.93f, 1f, stars));
            if (_stars != null)
            {
                var r = _stars.GetComponent<ParticleSystemRenderer>();
                r.enabled = stars > 0.01f;
            }

            var moon = Mathf.Clamp01(_look.Night * 1.3f) * Mathf.Clamp01(1.15f - _look.Cloud) * (1f - _look.Fog * 0.8f);
            SetColour(_moonMat, new Color(0.92f, 0.93f, 0.86f, moon));
            if (_moon != null) _moon.gameObject.SetActive(moon > 0.01f);

            // Lightning: a double flash every 7–22 s in a storm, the thunder 0.6–3 s later.
            if (_look.Storm)
            {
                if (_nextFlash < 0f) _nextFlash = now + Random.Range(2f, 8f);
                if (now >= _nextFlash)
                {
                    _flashStart = now;
                    _nextFlash = now + Random.Range(7f, 22f);
                    _thunderAt = now + Random.Range(0.6f, 3f);
                }

                var t = now - _flashStart;
                Flash = _flashStart < 0f || t > 0.45f ? 0f : Mathf.Max(Pulse(t, 0f), Pulse(t, 0.18f) * 0.7f);
                if (_lightning != null)
                {
                    _lightning.enabled = Flash > 0.01f;
                    _lightning.intensity = 3.2f * Flash;
                }

                if (_thunderAt > 0f && now >= _thunderAt)
                {
                    _thunderAt = -1f;
                    if (_thunderAudio != null) { _thunderAudio.volume = 0.8f * RkSettings.SoundVolume; _thunderAudio.Play(); }
                }
            }

            var volume = RkSettings.SoundVolume;
            SetVolume(_rainAudio, Mathf.Clamp01(_look.Rain * 1.1f) * 0.55f * volume);
            SetVolume(_windAudio, Mathf.Clamp01((_look.WindKph - 12f) / 50f) * 0.45f * volume + _look.Snow * 0.08f * volume);
        }

        private static float Pulse(float t, float at)
        {
            var x = t - at;
            return x < 0f ? 0f : Mathf.Exp(-x * 22f);
        }

        private static void SetVolume(AudioSource s, float v)
        {
            if (s == null) return;
            s.volume = v;
            if (v > 0.001f && !s.isPlaying) s.Play();
            else if (v <= 0.001f && s.isPlaying) s.Stop();
        }

        private static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var e = ps.emission;
            e.enabled = true;
            e.rateOverTime = rate;
            if (rate > 0.5f && (!ps.isPlaying || !ps.isEmitting)) ps.Play(true);
        }

        private static void SetColour(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        // ----- Construction ---------------------------------------------------------------------------------

        private ParticleSystem Precipitation(string name, Material material, bool rain)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // the box emits along its local +Z: straight down
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // An empty system has empty bounds, counts as unseen, and automatic culling would pause it before it starts.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = rain ? 4000 : 3500;
            main.startLifetime = rain ? 0.95f : 7f;
            main.startSpeed = rain ? new ParticleSystem.MinMaxCurve(17f, 21f) : new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
            main.startSize = rain ? new ParticleSystem.MinMaxCurve(0.018f, 0.028f) : new ParticleSystem.MinMaxCurve(0.045f, 0.1f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = rain ? new Vector3(36f, 36f, 1f) : new Vector3(44f, 44f, 1f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            if (!rain)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 0.35f;
                noise.scrollSpeed = 0.2f;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (rain)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.045f;
                r.lengthScale = 1f;
            }

            return ps;
        }

        private ParticleSystem Stars()
        {
            var go = new GameObject("Stars");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1400;
            main.startLifetime = float.PositiveInfinity;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _starMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.02f;

            // A fixed sky: the same stars every night (seeded), brighter ones fewer.
            var random = new System.Random(7701);
            var stars = new ParticleSystem.Particle[1200];
            for (var i = 0; i < stars.Length; i++)
            {
                var az = (float)(random.NextDouble() * Mathf.PI * 2);
                var el = Mathf.Asin(Mathf.Lerp(Mathf.Sin(12f * Mathf.Deg2Rad), 1f, (float)random.NextDouble()));
                var dir = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az));
                var bright = (float)random.NextDouble();
                bright = bright * bright * bright;
                stars[i] = new ParticleSystem.Particle
                {
                    position = dir * StarRadius,
                    startSize = Mathf.Lerp(0.06f, 0.2f, bright),
                    startColor = Color.Lerp(new Color(0.75f, 0.8f, 1f, 0.55f), new Color(1f, 0.97f, 0.9f, 1f), bright),
                    remainingLifetime = float.PositiveInfinity,
                    startLifetime = float.PositiveInfinity,
                };
            }

            ps.SetParticles(stars, stars.Length);
            ps.Pause();
            return ps;
        }

        private Transform Quad(string name, Material material, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size, size, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (material != null) r.sharedMaterial = material;
            return go.transform;
        }

        private AudioSource Loop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        private void RebuildClouds(float cover)
        {
            _cloudCoverBuilt = cover;
            const int size = 256;
            if (_cloudTex == null)
            {
                _cloudTex = Own(new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "rk-clouds", wrapMode = TextureWrapMode.Repeat });
                _cloudMat.mainTexture = _cloudTex;
                if (_cloudMat.HasProperty("_BaseMap")) _cloudMat.SetTexture("_BaseMap", _cloudTex);
            }

            // Tiling value-noise fbm; the cover sets how much of the sky the density threshold lets through.
            var px = new Color32[size * size];
            var threshold = Mathf.Lerp(0.72f, 0.2f, cover);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float n = 0f, amp = 0.5f, total = 0f;
                for (var o = 0; o < 5; o++)
                {
                    var period = 4 << o;
                    n += amp * TileNoise(x * period / (float)size, y * period / (float)size, period, o);
                    total += amp;
                    amp *= 0.5f;
                }

                n /= total;
                var a = Mathf.Clamp01((n - threshold) / 0.22f);
                a = a * a * (3f - 2f * a);
                a = Mathf.Max(a, Mathf.Pow(cover, 3f) * 0.95f); // a heavy sky closes its gaps
                var shade = (byte)(200 + 55 * (1f - a * 0.6f));
                px[y * size + x] = new Color32(shade, shade, shade, (byte)(255 * a));
            }

            _cloudTex.SetPixels32(px);
            _cloudTex.Apply(true);
            if (_clouds != null)
            {
                _clouds.enabled = cover > 0.05f;
                // Fair skies thin toward the horizon; an overcast one is closed all the way down.
                var mesh = _clouds.GetComponent<MeshFilter>().sharedMesh;
                var v = mesh.vertices;
                var colours = new Color[v.Length];
                for (var i = 0; i < v.Length; i++)
                {
                    var up = Mathf.Clamp01((v[i].y + 8f) / CloudHeight);
                    var fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(up / 0.35f));
                    colours[i] = new Color(1f, 1f, 1f, Mathf.Lerp(fade, 1f, cover * cover));
                }

                mesh.colors = colours;
            }
        }

        /// <summary>The cloud dome: rings from just below the horizon to the zenith, UVs laid flat in repeats of the texture.</summary>
        private MeshRenderer Dome()
        {
            const int rings = 12, segments = 48;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (var r = 0; r <= rings; r++)
            {
                var phi = Mathf.PI / 2f * r / rings;
                for (var s = 0; s <= segments; s++)
                {
                    var theta = Mathf.PI * 2f * s / segments;
                    var x = CloudRadius * Mathf.Cos(phi) * Mathf.Cos(theta);
                    var z = CloudRadius * Mathf.Cos(phi) * Mathf.Sin(theta);
                    verts.Add(new Vector3(x, CloudHeight * Mathf.Sin(phi) - 8f, z));
                    uvs.Add(new Vector2(x / CloudRepeat, z / CloudRepeat));
                }
            }

            for (var r = 0; r < rings; r++)
            for (var s = 0; s < segments; s++)
            {
                var a = r * (segments + 1) + s;
                var b = a + segments + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }

            var mesh = Own(new Mesh { name = "rk-cloud-dome" });
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.colors = new Color[verts.Count];
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * CloudRadius * 2.2f);
            var go = new GameObject("Clouds");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static float TileNoise(float x, float y, int period, int octave)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = x - x0;
            var fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float H(int i, int j)
            {
                i = ((i % period) + period) % period;
                j = ((j % period) + period) % period;
                var h = (uint)(i * 73856093) ^ (uint)(j * 19349663) ^ (uint)(octave * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xffff) / 65535f;
            }

            var a = Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx);
            var b = Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static Material ParticleMaterial(Texture texture, Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = "rk-weather" };
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);     // alpha
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            if (texture != null)
            {
                m.mainTexture = texture;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            }

            SetColour(m, colour);
            return m;
        }

        private static Texture2D SoftDot(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "rk-dot", wrapMode = TextureWrapMode.Clamp };
            var c = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                var a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }

            t.Apply();
            return t;
        }

        private static Texture2D MoonDisc(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "rk-moon", wrapMode = TextureWrapMode.Clamp };
            var c = (size - 1) / 2f;
            var r = c * 0.82f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                var disc = Mathf.Clamp01((r - d) / 1.5f);
                var glow = Mathf.Clamp01(1f - (d - r) / (c - r)) * 0.25f;
                // Faint maria: two soft darker patches.
                var m1 = Mathf.Exp(-((x - c * 0.8f) * (x - c * 0.8f) + (y - c * 1.15f) * (y - c * 1.15f)) / (r * r * 0.12f));
                var m2 = Mathf.Exp(-((x - c * 1.2f) * (x - c * 1.2f) + (y - c * 0.85f) * (y - c * 0.85f)) / (r * r * 0.07f));
                var shade = 1f - 0.18f * m1 - 0.14f * m2;
                t.SetPixel(x, y, new Color(shade, shade, shade * 0.97f, Mathf.Max(disc, disc > 0f ? disc : glow * glow)));
            }

            t.Apply();
            return t;
        }

        /// <summary>A looping noise: a one-pole low-pass of white noise (rain: bright; wind: dark and swelling).</summary>
        private static AudioClip NoiseClip(string name, float seconds, float lowPass, float swell, int seed)
        {
            const int rate = 22050;
            var n = (int)(rate * seconds);
            var data = new float[n];
            var random = new System.Random(seed);
            float y = 0f, peak = 0.0001f;
            for (var i = 0; i < n; i++)
            {
                var white = (float)(random.NextDouble() * 2 - 1);
                y += lowPass * (white - y);
                var env = 1f - swell * 0.5f * (1f + Mathf.Cos(2f * Mathf.PI * i / n)); // one slow swell per loop
                data[i] = y * env;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }

            // Fade the seam so the loop does not click, then normalise.
            var fade = rate / 20;
            for (var i = 0; i < fade; i++)
            {
                var a = i / (float)fade;
                data[i] = data[i] * a + data[n - fade + i] * (1f - a);
            }

            System.Array.Resize(ref data, n - fade);
            for (var i = 0; i < data.Length; i++) data[i] = data[i] / peak * 0.8f;
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A roll of thunder: brown noise with a sharp crack and a long decay.</summary>
        private static AudioClip ThunderClip(int seed)
        {
            const int rate = 22050;
            var n = rate * 4;
            var data = new float[n];
            var random = new System.Random(seed);
            float y = 0f, peak = 0.0001f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)rate;
                y = y * 0.985f + (float)(random.NextDouble() * 2 - 1) * 0.06f;
                var crack = t < 0.25f ? (float)(random.NextDouble() * 2 - 1) * (1f - t / 0.25f) * 0.5f : 0f;
                var env = Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t * 0.9f) * (1f + 0.4f * Mathf.Sin(t * 5.3f));
                data[i] = (y + crack) * env;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }

            for (var i = 0; i < n; i++) data[i] = data[i] / peak * 0.9f;
            var clip = AudioClip.Create("rk-thunder", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private T Own<T>(T o) where T : Object
        {
            _owned.Add(o);
            return o;
        }

        private void OnDestroy()
        {
            foreach (var o in _owned)
                if (o != null) Destroy(o);
            _owned.Clear();
        }
    }
}
