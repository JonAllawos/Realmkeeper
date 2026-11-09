using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>Placeholder low-poly tree meshes (two colour variants per species), origin at the trunk base.</summary>
    public static class TreeMeshes
    {
        public static Mesh Build(TreeSpecies species, int variant)
        {
            var builder = new MeshBuilder();
            if (species == TreeSpecies.Conifer)
            {
                var foliage = variant == 0 ? Palette.ConiferA : Palette.ConiferB;
                builder.AddBox(Vector3.zero, new Vector3(0.22f, 0.9f, 0.22f), 0f, Palette.Bark, Palette.Bark);
                builder.AddCone(new Vector3(0f, 0.55f, 0f), 1.05f, 1.8f, 7, foliage);
                builder.AddCone(new Vector3(0f, 1.45f, 0f), 0.8f, 1.6f, 7, Palette.Scale(foliage, 1.06f));
                builder.AddCone(new Vector3(0f, 2.25f, 0f), 0.52f, 1.25f, 6, Palette.Scale(foliage, 1.12f));
                return builder.ToMesh($"Conifer {variant}");
            }

            var canopy = variant == 0 ? Palette.BroadleafA : Palette.BroadleafB;
            builder.AddBox(Vector3.zero, new Vector3(0.26f, 1.3f, 0.26f), 0f, Palette.Bark, Palette.Bark);
            builder.AddBlob(new Vector3(0f, 2.0f, 0f), new Vector3(1.15f, 0.95f, 1.15f), 7, 5, canopy);
            builder.AddBlob(new Vector3(0.45f, 2.55f, -0.2f), new Vector3(0.7f, 0.6f, 0.7f), 6, 4, Palette.Scale(canopy, 1.08f));
            return builder.ToMesh($"Broadleaf {variant}");
        }
    }
}
