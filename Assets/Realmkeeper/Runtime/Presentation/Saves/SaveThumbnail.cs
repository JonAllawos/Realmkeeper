using System;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// M1.1: the picture on a save slot. The camera showing the world is rendered once into a small texture (the
    /// interface is drawn separately, so menus never appear in it) and encoded as a JPEG. Any failure returns null: a
    /// missing thumbnail never affects a save.
    /// </summary>
    public static class SaveThumbnail
    {
        public const int Width = 320;
        public const int Height = 180;

        public static byte[] Capture()
        {
            var camera = FindCamera();
            if (camera == null) return null;
            RenderTexture rt = null;
            Texture2D tex = null;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply(false);
                return tex.EncodeToJPG(80);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Realmkeeper: no save thumbnail (" + e.Message + ").");
                return null;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
        }

        /// <summary>The enabled camera drawing to the screen with the highest depth (the one the player sees).</summary>
        private static Camera FindCamera()
        {
            Camera best = null;
            foreach (var c in Camera.allCameras)
            {
                if (c == null || !c.isActiveAndEnabled || c.targetTexture != null) continue;
                if (best == null || c.depth > best.depth) best = c;
            }

            return best;
        }

        /// <summary>Loads a thumbnail file into a texture the caller destroys, or null.</summary>
        public static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (tex.LoadImage(bytes)) return tex;
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
            }

            return null;
        }
    }
}
