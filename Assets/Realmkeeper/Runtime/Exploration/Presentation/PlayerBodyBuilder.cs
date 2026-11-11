using System;
using System.Collections.Generic;
using System.Text;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>What the player's body shows: their chosen look and what they wear and wield (from world data).</summary>
    public sealed class PlayerLook
    {
        public BodyFrame Frame { get; set; }
        public string HairId { get; set; }
        public string BeardId { get; set; }
        public int SkinTone { get; set; }
        public int HairColour { get; set; }
        public int ClothTint { get; set; }

        /// <summary>Phase 6: body height relative to a human (a species without its own model is shown at its height).</summary>
        public float HeightScale { get; set; } = 1f;

        /// <summary>The item worn or wielded in each slot (item IDs).</summary>
        public Dictionary<EquipmentSlot, string> Items { get; } = new Dictionary<EquipmentSlot, string>();

        public static PlayerLook From(PlayerHub hub)
        {
            var look = new PlayerLook();
            if (hub == null) return look;
            var a = hub.Appearance;
            look.Frame = a.Frame == NpcSex.Female ? BodyFrame.Female : BodyFrame.Male;
            look.HairId = a.HairId;
            look.BeardId = a.BeardId;
            look.SkinTone = a.SkinTone;
            look.HairColour = a.HairColour;
            look.ClothTint = a.ClothTint;
            look.HeightScale = Realmkeeper.Domain.Rpg.RpgCatalog.FindSpecies(hub.SpeciesId)?.Appearance.HeightScale ?? 1f;
            foreach (var pair in hub.Equipment) look.Items[pair.Key] = pair.Value.ItemId;
            return look;
        }

        /// <summary>Changes whenever the body would look different.</summary>
        public string Signature
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append(Frame).Append('/').Append(HairId ?? "-").Append('/').Append(BeardId ?? "-").Append("/s").Append(SkinTone).Append("/h").Append(HairColour).Append("/t").Append(ClothTint).Append("/x").Append(HeightScale.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                    if (Items.TryGetValue(slot, out var item)) sb.Append('|').Append(slot).Append('=').Append(item);
                return sb.ToString();
            }
        }
    }

    /// <summary>
    /// Builds the player's body from <see cref="PlayerLook"/> (Phase 5). Clothing comes from the outfit
    /// models' own parts (each outfit holds its body, arms, legs, feet, hood and pauldrons as separate
    /// meshes on the shared skeleton): each worn item shows its part in its cloth; a part from the other
    /// outfit is moved onto the skeleton. An empty clothing slot shows plain undyed underclothes (the kit
    /// has no bare body fitted to the outfits). Hats sit on the head; a one-handed weapon is held in the
    /// right hand, a shield on the left forearm; long weapons (spear, staff, bow) are slung across the
    /// back, since this phase has no combat stance. Used by the exploration body and the hub preview.
    /// </summary>
    public static class PlayerBodyBuilder
    {
        /// <summary>The body part each clothing slot shows.</summary>
        public static readonly IReadOnlyDictionary<EquipmentSlot, string> SlotParts = new Dictionary<EquipmentSlot, string>
        {
            [EquipmentSlot.Chest] = "Body",
            [EquipmentSlot.Hands] = "Arms",
            [EquipmentSlot.Legs] = "Legs",
            [EquipmentSlot.Feet] = "Feet",
            [EquipmentSlot.Head] = "Hood",
            [EquipmentSlot.Back] = "Pauldrons",
        };

        private static readonly Color Undyed = new Color(0.93f, 0.9f, 0.84f);

        public static CharacterAnimationDriver Build(Transform parent, PlayerLook look, ArtCatalog catalog, GeneratedAssets assets, out PlayerBodyReport report)
        {
            report = new PlayerBodyReport();
            if (catalog == null || !catalog.HasCharacters) return null;
            var chest = Outfit(look, EquipmentSlot.Chest);
            var baseSet = chest.set ?? "Peasant";
            var frame = look.Frame == BodyFrame.Female ? "Female" : "Male";
            var appearance = new NpcAppearance
            {
                Frame = look.Frame,
                BodyId = frame + "_" + baseSet,
                OutfitMaterialId = chest.material ?? baseSet,
                HairId = look.HairId,
                BeardId = look.Frame == BodyFrame.Male ? look.BeardId : null,
                SkinTone = Mathf.Clamp(look.SkinTone, 0, NpcAppearanceResolver.SkinTones.Count - 1),
                HairColour = Mathf.Clamp(look.HairColour, 0, NpcAppearanceResolver.HairColours.Count - 1),
                OutfitTint = Mathf.Clamp(look.ClothTint, 0, NpcAppearanceResolver.OutfitTints.Count - 1),
                HeightScale = Mathf.Clamp(look.HeightScale, 0.5f, 1.4f),
            };
            var driver = HumanoidCharacterBuilder.Build(parent, appearance, catalog);
            if (driver == null) return null;
            var body = driver.transform;
            var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            var cloth = HumanoidCharacterBuilder.TintOf(NpcAppearanceResolver.OutfitTints[appearance.OutfitTint]);
            var skin = HumanoidCharacterBuilder.TintOf(NpcAppearanceResolver.SkinTones[appearance.SkinTone]);

            // The base outfit's own part renderers, by part.
            var baseParts = new Dictionary<string, List<SkinnedMeshRenderer>>(StringComparer.Ordinal);
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var part = PartOf(r.name);
                if (part == null || !r.name.StartsWith(frame + "_" + baseSet, StringComparison.Ordinal)) continue;
                if (!baseParts.TryGetValue(part, out var list)) baseParts[part] = list = new List<SkinnedMeshRenderer>();
                list.Add(r);
            }

            foreach (var pair in SlotParts)
            {
                var slot = pair.Key;
                var part = pair.Value;
                var worn = Outfit(look, slot);
                baseParts.TryGetValue(part, out var own);
                own = own ?? new List<SkinnedMeshRenderer>();
                var optional = part == "Hood" || part == "Pauldrons";
                if (worn.set == null)
                {
                    if (optional)
                    {
                        foreach (var r in own) r.enabled = false;
                        continue;
                    }

                    // Underclothes: the peasant part, undyed.
                    if (baseSet == "Peasant")
                    {
                        foreach (var r in own) Recolour(r, catalog.Material("Peasant"), Undyed);
                    }
                    else
                    {
                        foreach (var r in own) r.enabled = false;
                        foreach (var r in AttachPart(catalog, frame + "_Peasant", part, body, bones, catalog.Material("Peasant"))) Recolour(r, null, Undyed);
                    }

                    report.Shown[slot] = "underclothes";
                    continue;
                }

                var material = catalog.Material(worn.material) ?? catalog.Material(worn.set);
                if (worn.set == baseSet && own.Count > 0)
                {
                    foreach (var r in own)
                    {
                        r.enabled = true;
                        Recolour(r, material, cloth);
                    }
                }
                else
                {
                    foreach (var r in own) r.enabled = false;
                    foreach (var r in AttachPart(catalog, frame + "_" + worn.set, part, body, bones, material)) Recolour(r, null, cloth);
                }

                report.Shown[slot] = worn.set + ":" + part + ":" + worn.material;
            }

            // A hood covers the hair.
            if (Outfit(look, EquipmentSlot.Head).set != null && look.HairId != null)
                foreach (Transform child in body)
                    if (child.name.StartsWith(look.HairId + " ", StringComparison.Ordinal)) child.gameObject.SetActive(false);

            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.enabled && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("M_Body", StringComparison.Ordinal)) HumanoidCharacterBuilder.Colour(r, 0, skin);

            Props(look, body, bones, catalog, assets, report);
            return driver;
        }

        private static IEnumerable<SkinnedMeshRenderer> AttachPart(ArtCatalog catalog, string bodyId, string part, Transform body, Dictionary<string, Transform> bones, Material material)
        {
            var prefab = catalog.Body(bodyId);
            if (prefab == null) return Array.Empty<SkinnedMeshRenderer>();
            return HumanoidCharacterBuilder.Attach(prefab, body, bones, (r, i, m) => Color.white, src => PartOf(src.name) == part, material);
        }

        private static void Recolour(SkinnedMeshRenderer r, Material material, Color colour)
        {
            var mats = r.sharedMaterials;
            for (var i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.StartsWith("M_Body", StringComparison.Ordinal)) continue;
                if (material != null) mats[i] = material;
            }

            r.sharedMaterials = mats;
            for (var i = 0; i < mats.Length; i++)
                if (mats[i] == null || !mats[i].name.StartsWith("M_Body", StringComparison.Ordinal)) HumanoidCharacterBuilder.Colour(r, i, colour);
        }

        /// <summary>The clothing part a renderer of an outfit model is (by the CC0 kit's mesh names), or null.</summary>
        public static string PartOf(string rendererName)
        {
            if (rendererName == null) return null;
            if (rendererName.Contains("_Head_Hood")) return "Hood";
            if (rendererName.Contains("_Acc_Pauldron")) return "Pauldrons";
            if (rendererName.Contains("_Arms")) return "Arms";
            if (rendererName.Contains("_Legs")) return "Legs";
            if (rendererName.Contains("_Feet")) return "Feet";
            if (rendererName.Contains("_Body")) return "Body";
            return null;
        }

        /// <summary>The outfit set and material of the item worn in a slot ("outfit:Set:Part:Material"), or nulls.</summary>
        private static (string set, string part, string material) Outfit(PlayerLook look, EquipmentSlot slot)
        {
            if (!look.Items.TryGetValue(slot, out var item)) return (null, null, null);
            var visual = ItemCatalog.Find(item)?.Visual;
            if (visual == null || !visual.StartsWith("outfit:", StringComparison.Ordinal)) return (null, null, null);
            var p = visual.Split(':');
            return p.Length >= 4 ? (p[1], p[2], p[3]) : (null, null, null);
        }

        // ----- Held and worn props --------------------------------------------------------------------

        private static void Props(PlayerLook look, Transform body, Dictionary<string, Transform> bones, ArtCatalog catalog, GeneratedAssets assets, PlayerBodyReport report)
        {
            // Placed at bind time in the body's frame (the bind pose is a T-pose facing +Z), then parented to
            // the bone so they follow the animation.
            var up = body.up;
            var forward = body.forward;
            var right = body.right;

            if (look.Items.TryGetValue(EquipmentSlot.Head, out var hat) && ItemCatalog.Find(hat)?.Visual?.StartsWith("prop:", StringComparison.Ordinal) == true)
            {
                var head = Bone(bones, "Head", "head");
                if (head != null)
                {
                    var model = ItemModels.Build(head, hat, catalog, assets);
                    if (model != null)
                    {
                        model.transform.SetPositionAndRotation(head.position + up * 0.1f + forward * 0.01f, body.rotation);
                        report.Props.Add(hat + "@" + head.name);
                    }
                }
            }

            if (look.Items.TryGetValue(EquipmentSlot.MainHand, out var weapon))
            {
                var def = ItemCatalog.Find(weapon);
                if (def != null && def.TwoHanded)
                {
                    var spine = Bone(bones, "spine_03", "spine_02", "spine_01");
                    var model = spine == null ? null : ItemModels.Build(spine, weapon, catalog, assets);
                    if (model != null)
                    {
                        // Slung diagonally across the back.
                        model.transform.SetPositionAndRotation(spine.position - forward * 0.17f, body.rotation * Quaternion.Euler(0f, 0f, 38f));
                        report.Props.Add(weapon + "@" + spine.name);
                    }
                }
                else if (def != null)
                {
                    var hand = Bone(bones, "hand_r");
                    var model = hand == null ? null : ItemModels.Build(hand, weapon, catalog, assets);
                    if (model != null)
                    {
                        // Gripped in the fist, blade forward with its edge upright.
                        model.transform.SetPositionAndRotation(hand.position + right * 0.085f - up * 0.02f, body.rotation * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 90f, 0f));
                        report.Props.Add(weapon + "@" + hand.name);
                    }
                }
            }

            if (look.Items.TryGetValue(EquipmentSlot.OffHand, out var shield))
            {
                var arm = Bone(bones, "lowerarm_l");
                var model = arm == null ? null : ItemModels.Build(arm, shield, catalog, assets);
                if (model != null)
                {
                    // On the back of the forearm, face outward (up in the T-pose).
                    model.transform.SetPositionAndRotation(arm.position - right * 0.13f + up * 0.07f, body.rotation * Quaternion.Euler(-90f, 0f, 0f));
                    report.Props.Add(shield + "@" + arm.name);
                }
            }
        }

        private static Transform Bone(Dictionary<string, Transform> bones, params string[] names)
        {
            foreach (var n in names) if (bones.TryGetValue(n, out var t)) return t;
            return null;
        }
    }

    /// <summary>What a built player body actually shows (for tests and the hub).</summary>
    public sealed class PlayerBodyReport
    {
        /// <summary>Per clothing slot: "Set:Part:Material" or "underclothes".</summary>
        public Dictionary<EquipmentSlot, string> Shown { get; } = new Dictionary<EquipmentSlot, string>();

        /// <summary>Props built, as "itemId@bone".</summary>
        public List<string> Props { get; } = new List<string>();
    }
}
