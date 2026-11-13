using System;
using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Domain
{
    public enum BodyFrame
    {
        Male = 0,
        Female = 1,
    }

    /// <summary>
    /// How one resident looks, resolved from world data: the persistent <see cref="NpcId"/>, the role
    /// and the given name. Derived, never stored, and never a source of identity: the same NPC
    /// always resolves to the same appearance, and changing how a body looks cannot change who it is.
    /// IDs refer to the art catalog (bodies, parts and materials); indices pick from the presenter's
    /// palettes.
    /// </summary>
    public sealed class NpcAppearance
    {
        public NpcId NpcId { get; set; }
        public BodyFrame Frame { get; set; }

        /// <summary>Clothed body on the shared skeleton, e.g. "Male_Peasant".</summary>
        public string BodyId { get; set; }

        /// <summary>Material for the clothing (texture variant), e.g. "Peasant2".</summary>
        public string OutfitMaterialId { get; set; }

        public string HeadId => Frame == BodyFrame.Female ? "Head_Female" : "Head_Male";

        /// <summary>Hair part ID, or null for none.</summary>
        public string HairId { get; set; }

        /// <summary>Beard part ID, or null.</summary>
        public string BeardId { get; set; }

        /// <summary>Index into <see cref="NpcAppearanceResolver.SkinTones"/>.</summary>
        public int SkinTone { get; set; }

        /// <summary>Index into <see cref="NpcAppearanceResolver.HairColours"/>.</summary>
        public int HairColour { get; set; }

        /// <summary>Index into <see cref="NpcAppearanceResolver.OutfitTints"/>.</summary>
        public int OutfitTint { get; set; }

        /// <summary>Uniform scale of the body (about ±5% around 1).</summary>
        public float HeightScale { get; set; }

        /// <summary>Phase 20: extra width (the player's build); 1 for residents.</summary>
        public float WidthScale { get; set; } = 1f;

        /// <summary>Phase 20A: eye colour (PlayerAppearanceOptions.EyePalette); 0, the drawn brown, for residents.</summary>
        public int EyeColour { get; set; }

        /// <summary>A short, stable description for logs and tests.</summary>
        public string Signature =>
            $"{BodyId}/{OutfitMaterialId}/{HairId ?? "-"}/{BeardId ?? "-"}/s{SkinTone}/h{HairColour}/t{OutfitTint}/{HeightScale:F2}";
    }

    /// <summary>
    /// Maps a resident's stable identity to an appearance. Deterministic (a stable 64-bit hash of the
    /// NPC ID, never <c>GetHashCode</c>), engine-free and independent of iteration order.
    /// Role decides the outfit (guards in leather armour, elders and merchants in finer dress,
    /// labourers in work clothes); the hash varies hair, skin tone, tint and height.
    /// </summary>
    public static class NpcAppearanceResolver
    {
        /// <summary>Skin tone multipliers (linear RGB) applied to the shared skin textures.</summary>
        /// <remarks>Phase 20: the player's species-wide palette (<see cref="PlayerAppearanceOptions.SkinPalette"/>); residents draw from its human tones only.</remarks>
        public static readonly IReadOnlyList<(float r, float g, float b)> SkinTones = PlayerAppearanceOptions.SkinPalette.Select(t => (t.r, t.g, t.b)).ToList();

        public static readonly IReadOnlyList<(float r, float g, float b)> HairColours = new[]
        {
            (0.22f, 0.15f, 0.10f), (0.38f, 0.25f, 0.15f), (0.55f, 0.30f, 0.16f), (0.78f, 0.62f, 0.38f), (0.10f, 0.09f, 0.09f), (0.66f, 0.64f, 0.62f),
        };

        public static readonly IReadOnlyList<(float r, float g, float b)> OutfitTints = new[]
        {
            (1f, 1f, 1f), (0.92f, 0.96f, 1.0f), (1.0f, 0.94f, 0.86f), (0.9f, 1.0f, 0.9f),
        };

        /// <summary>Grey hair index (elders).</summary>
        public const int GreyHair = 5;

        private static readonly string[] MaleHair = { "Hair_SimpleParted", "Hair_Buzzed", "Hair_Long", "Hair_SimpleParted" };
        private static readonly string[] FemaleHair = { "Hair_Buns", "Hair_Long", "Hair_Buns", "Hair_BuzzedFemale" };

        /// <summary>Given names in the world generator's list that are conventionally feminine.</summary>
        private static readonly HashSet<string> FeminineNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cwen", "Edda", "Fenna", "Hild", "Isolde", "Kenna", "Maud", "Petra", "Quenby", "Rowena", "Tilda", "Wenna",
            "Agnes", "Merry", "Ottilie", "Sabine",
        };

        public static NpcAppearance Resolve(NpcId npcId, NpcRole role, string name, BodyFrame? frameFromIdentity = null)
        {
            if (npcId.IsEmpty) throw new ArgumentException("A persistent NPC ID is required.", nameof(npcId));
            var seed = SeedMixer.Combine(SeedMixer.HashString("appearance"), npcId.ToString());
            ulong Pick(int stream) => SeedMixer.Combine(seed, (ulong)stream);
            int Index(int stream, int count) => (int)(Pick(stream) % (ulong)count);

            var frame = frameFromIdentity ?? FrameFor(name, seed); // Phase 4 identity (sex) wins over the name hint
            var sex = frame == BodyFrame.Female ? "Female" : "Male";
            var (outfit, material) = OutfitFor(role, Index(1, 2));
            var appearance = new NpcAppearance
            {
                NpcId = npcId,
                Frame = frame,
                BodyId = sex + "_" + outfit,
                OutfitMaterialId = material,
                SkinTone = Index(2, PlayerAppearanceOptions.SkinTones), // human tones only (Phase 20 appended species tones)
                HairColour = role == NpcRole.Elder ? GreyHair : Index(3, HairColours.Count - 1),
                OutfitTint = Index(4, OutfitTints.Count),
                HeightScale = 0.95f + Index(5, 11) * 0.01f,
            };

            var hair = frame == BodyFrame.Female ? FemaleHair : MaleHair;
            appearance.HairId = hair[Index(6, hair.Length)];
            if (frame == BodyFrame.Male)
            {
                var beardChance = role == NpcRole.Elder || role == NpcRole.Smith ? 4 : 2; // out of 5
                if (Index(7, 5) < beardChance) appearance.BeardId = "Hair_Beard";
                if (role == NpcRole.Elder && Index(8, 3) == 0) appearance.HairId = null; // bald elder
            }

            return appearance;
        }

        /// <summary>The player's own body: a ranger, fixed so the character is recognisable.</summary>
        public static NpcAppearance Player() => new NpcAppearance
        {
            Frame = BodyFrame.Male,
            BodyId = "Male_Ranger",
            OutfitMaterialId = "Ranger",
            HairId = "Hair_SimpleParted",
            BeardId = "Hair_Beard",
            SkinTone = 1,
            HairColour = 1,
            OutfitTint = 0,
            HeightScale = 1f,
        };

        public static BodyFrame FrameFor(string name, ulong seed)
        {
            var given = name?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (given != null && given.Length > 0)
            {
                if (FeminineNames.Contains(given[0])) return BodyFrame.Female;
                return BodyFrame.Male;
            }

            return SeedMixer.Combine(seed, 99UL) % 2 == 0 ? BodyFrame.Male : BodyFrame.Female;
        }

        private static (string outfit, string material) OutfitFor(NpcRole role, int variant)
        {
            switch (role)
            {
                case NpcRole.Guard: return ("Ranger", "Ranger");
                case NpcRole.Elder: return ("Ranger", "Ranger3");
                case NpcRole.Merchant: return variant == 0 ? ("Ranger", "Ranger3") : ("Peasant", "Peasant");
                case NpcRole.Smith:
                case NpcRole.Labourer: return ("Peasant", "Peasant2");
                case NpcRole.Farmer: return ("Peasant", variant == 0 ? "Peasant" : "Peasant2");
                case NpcRole.Innkeeper: return ("Peasant", "Peasant");
                default: return ("Peasant", variant == 0 ? "Peasant" : "Peasant2");
            }
        }
    }
}
