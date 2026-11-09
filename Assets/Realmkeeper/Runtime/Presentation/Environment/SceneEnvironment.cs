using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Lighting and atmosphere: a warm low-angle sun with soft shadows, a gradient
    /// ambient, linear depth fog and a global post-processing volume (mild bloom, neutral
    /// tonemapping, a slight warm grade and vignette). Built in code so the scene file stays
    /// minimal; the generated profile is owned and destroyed by this component.
    /// </summary>
    public sealed class SceneEnvironment : MonoBehaviour
    {
        public const float SunPitch = 34f;
        public const float SunYaw = -38f;

        private VolumeProfile _profile;

        public Light Sun { get; private set; }

        public Volume Volume { get; private set; }

        public static SceneEnvironment Create(Transform parent)
        {
            var go = new GameObject("Environment");
            go.transform.SetParent(parent, false);
            var environment = go.AddComponent<SceneEnvironment>();
            environment.Setup();
            return environment;
        }

        /// <summary>Linear fog relative to how far the camera sits from its pivot.</summary>
        public static void ConfigureFog(float cameraDistance)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Haze;
            RenderSettings.fogStartDistance = cameraDistance + 10f;
            RenderSettings.fogEndDistance = cameraDistance + 380f;
        }

        private void Setup()
        {
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(transform, false);
            sunObject.transform.rotation = Quaternion.Euler(SunPitch, SunYaw, 0f);
            Sun = sunObject.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = Palette.Sun;
            Sun.intensity = 1.55f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.82f;
            RenderSettings.sun = Sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientProbe = GradientProbe(Palette.AmbientSky, Palette.AmbientEquator, Palette.AmbientGround);

            var volumeObject = new GameObject("Post Processing");
            volumeObject.transform.SetParent(transform, false);
            Volume = volumeObject.AddComponent<Volume>();
            Volume.isGlobal = true;
            Volume.priority = 10f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Realmkeeper Runtime Profile";

            var bloom = _profile.Add<Bloom>(true);
            bloom.intensity.Override(0.22f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);

            var tonemapping = _profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = _profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(-6f);

            var whiteBalance = _profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(6f);

            var vignette = _profile.Add<Vignette>(true);
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.5f);

            Volume.sharedProfile = _profile;
        }

        private void OnDestroy()
        {
            if (_profile != null) PresentationMaterials.DestroyObject(_profile);
        }

        /// <summary>Approximates Unity's trilight gradient as an L2 spherical-harmonic probe.</summary>
        private static SphericalHarmonicsL2 GradientProbe(Color sky, Color equator, Color ground)
        {
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(equator.linear);
            probe.AddDirectionalLight(Vector3.up, (sky.linear - equator.linear) * 1.2f, 1f);
            probe.AddDirectionalLight(Vector3.down, (ground.linear - equator.linear) * 1.2f, 1f);
            return probe;
        }
    }
}
