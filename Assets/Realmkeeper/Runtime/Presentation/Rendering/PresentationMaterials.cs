using System;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// The two shared materials every world visual uses: one opaque vertex-colour material
    /// (terrain, roads, trees, buildings) and one transparent water material.
    /// Created at runtime from shader references so no material assets need maintaining.
    /// </summary>
    public sealed class PresentationMaterials : IDisposable
    {
        public const string VertexColorShaderName = "Realmkeeper/VertexColorLit";
        public const string WaterShaderName = "Realmkeeper/Water";

        public PresentationMaterials(Shader vertexColorShader, Shader waterShader)
        {
            vertexColorShader = vertexColorShader != null ? vertexColorShader : Shader.Find(VertexColorShaderName);
            waterShader = waterShader != null ? waterShader : Shader.Find(WaterShaderName);
            if (vertexColorShader == null) throw new InvalidOperationException($"Shader '{VertexColorShaderName}' was not found.");
            if (waterShader == null) throw new InvalidOperationException($"Shader '{WaterShaderName}' was not found.");

            Opaque = new Material(vertexColorShader) { name = "Realmkeeper Vertex Colour", enableInstancing = true };
            Water = new Material(waterShader) { name = "Realmkeeper Water" };

            // Separate instance so the selection ring's pulsing tint never touches world materials.
            Highlight = new Material(vertexColorShader) { name = "Realmkeeper Selection Highlight" };
        }

        public Material Highlight { get; }

        public Material Opaque { get; }

        public Material Water { get; }

        public void Dispose()
        {
            DestroyObject(Opaque);
            DestroyObject(Water);
            DestroyObject(Highlight);
        }

        internal static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
