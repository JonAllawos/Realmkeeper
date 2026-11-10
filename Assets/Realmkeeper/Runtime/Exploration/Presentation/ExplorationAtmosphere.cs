using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Street-level lighting and atmosphere, applied while a settlement is explored and fully
    /// restored on exit (the strategic map keeps its own): a warm late-morning sun with shadows
    /// tuned for walking distances, a sky-coloured gradient ambient that lifts walls in shade,
    /// a procedural sky, light exponential haze, and a street-level post-processing volume
    /// (ACES tonemapping, gentle warm grade, mild bloom and vignette) that overrides the global one.
    /// </summary>
    public sealed class ExplorationAtmosphere
    {
        public static readonly Color SunColour = new Color(1f, 0.9f, 0.76f);
        public static readonly Color AmbientSky = new Color(0.56f, 0.62f, 0.72f);
        public static readonly Color AmbientEquator = new Color(0.58f, 0.54f, 0.48f);
        public static readonly Color AmbientGround = new Color(0.30f, 0.27f, 0.22f);
        public static readonly Color Haze = new Color(0.76f, 0.75f, 0.72f);

        public const float StreetShadowDistance = 90f;

        private Snapshot _snapshot;
        private VolumeProfile _profile;

        public Light Sun { get; private set; }
        public Volume Volume { get; private set; }
        public bool IsApplied => _snapshot != null;

        /// <summary>
        /// Records the current render settings. Call it while the strategic scene is still active: render
        /// settings belong to the active scene, so a snapshot taken after the exploration scene is activated
        /// would hold that new scene's defaults and write them into the strategic scene on exit.
        /// </summary>
        public void Capture() => _snapshot = Snapshot.Capture();

        /// <summary>Creates the sun and the volume under <paramref name="parent"/> and sets render settings.</summary>
        public void Apply(Transform parent, ArtCatalog catalog)
        {
            if (_snapshot == null) _snapshot = Snapshot.Capture();

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = SunColour;
            Sun.intensity = 1.55f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.82f;
            Sun.shadowBias = 0.04f;
            Sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = Sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1.15f;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(AmbientEquator.linear);
            probe.AddDirectionalLight(Vector3.up, (AmbientSky - AmbientEquator).linear, 0.9f);
            probe.AddDirectionalLight(Vector3.down, (AmbientGround - AmbientEquator).linear * 0.6f, 0.9f);
            RenderSettings.ambientProbe = probe;

            var sky = catalog != null ? catalog.Material("Sky") : null;
            if (sky != null) RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Haze;
            RenderSettings.fogDensity = 0.0048f;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = StreetShadowDistance;
                urp.shadowCascadeCount = 2; // two cascades cover walking distances; halves shadow draws
            }

            var volumeGo = new GameObject("Street-level volume");
            volumeGo.transform.SetParent(parent, false);
            Volume = volumeGo.AddComponent<Volume>();
            Volume.isGlobal = true;
            Volume.priority = 50f; // above the strategic global volume
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Realmkeeper Street Profile";
            var tone = _profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var colour = _profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(0.35f);
            colour.contrast.Override(10f);
            colour.saturation.Override(6f);
            var white = _profile.Add<WhiteBalance>(true);
            white.temperature.Override(7f);
            white.tint.Override(2f);
            var smh = _profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.96f, 0.98f, 1.06f, 0f)); // cool shade, warm light
            smh.highlights.Override(new Vector4(1.05f, 1.0f, 0.94f, 0f));
            var bloom = _profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);
            var vignette = _profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.5f);
            Volume.sharedProfile = _profile;
        }

        /// <summary>Configures an exploration camera: sky, anti-aliasing and post-processing.</summary>
        public static void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;
            if (RenderSettings.skybox != null) camera.clearFlags = CameraClearFlags.Skybox;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
        }

        public void Restore()
        {
            _snapshot?.Restore();
            _snapshot = null;
            if (_profile != null)
            {
                if (UnityEngine.Application.isPlaying) Object.Destroy(_profile);
                else Object.DestroyImmediate(_profile);
            }

            _profile = null;
            Sun = null;
            Volume = null;
        }

        /// <summary>The render settings exploration changes, captured on entry.</summary>
        private sealed class Snapshot
        {
            private bool _fog;
            private FogMode _fogMode;
            private Color _fogColor;
            private float _fogStart, _fogEnd, _fogDensity;
            private Light _sun;
            private AmbientMode _ambientMode;
            private Color _sky, _equator, _ground;
            private float _ambientIntensity;
            private SphericalHarmonicsL2 _probe;
            private Material _skybox;
            private float _shadowDistance = -1f;
            private int _cascades = -1;

            public static Snapshot Capture()
            {
                var s = new Snapshot
                {
                    _fog = RenderSettings.fog,
                    _fogMode = RenderSettings.fogMode,
                    _fogColor = RenderSettings.fogColor,
                    _fogStart = RenderSettings.fogStartDistance,
                    _fogEnd = RenderSettings.fogEndDistance,
                    _fogDensity = RenderSettings.fogDensity,
                    _sun = RenderSettings.sun,
                    _ambientMode = RenderSettings.ambientMode,
                    _sky = RenderSettings.ambientSkyColor,
                    _equator = RenderSettings.ambientEquatorColor,
                    _ground = RenderSettings.ambientGroundColor,
                    _ambientIntensity = RenderSettings.ambientIntensity,
                    _probe = RenderSettings.ambientProbe,
                    _skybox = RenderSettings.skybox,
                };
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) { s._shadowDistance = urp.shadowDistance; s._cascades = urp.shadowCascadeCount; }
                return s;
            }

            public void Restore()
            {
                RenderSettings.fog = _fog;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogStartDistance = _fogStart;
                RenderSettings.fogEndDistance = _fogEnd;
                RenderSettings.fogDensity = _fogDensity;
                RenderSettings.sun = _sun;
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientSkyColor = _sky;
                RenderSettings.ambientEquatorColor = _equator;
                RenderSettings.ambientGroundColor = _ground;
                RenderSettings.ambientIntensity = _ambientIntensity;
                RenderSettings.ambientProbe = _probe;
                RenderSettings.skybox = _skybox;
                if (_shadowDistance >= 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                {
                    urp.shadowDistance = _shadowDistance;
                    if (_cascades > 0) urp.shadowCascadeCount = _cascades;
                }
            }
        }
    }
}
