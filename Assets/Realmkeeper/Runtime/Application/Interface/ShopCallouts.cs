using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Phase 27: what a shopkeeper calls out to a passer-by, by the kind of shop. Data, not code per keeper: a line is chosen
    /// from its kind's list (the general list when a kind has none). Plain trade talk; no campaign lore. The rules of when to
    /// call (distance, cooldowns, never in a window, a fight or a conversation) are the presentation's
    /// (<c>ExplorationController.Callouts</c>).
    /// </summary>
    public static class ShopCallouts
    {
        public static readonly IReadOnlyDictionary<ShopKind, string[]> Lines = new Dictionary<ShopKind, string[]>
        {
            [ShopKind.GeneralStore] = new[] { "Rope, lamp oil, candles — whatever the road needs!", "Come in, have a look, no charge for looking.", "Fair prices, honest weights!" },
            [ShopKind.Outfitter] = new[] { "Packs and boots for the road, traveller!", "Going far? You'll want a good cloak." },
            [ShopKind.Smithy] = new[] { "Blades sharpened, buckles mended!", "Good iron here — step in and see it.", "Anything needs mending, bring it to me." },
            [ShopKind.TavernKitchen] = new[] { "Hot food and a seat by the fire!", "Stew's on — come in out of the wind." },
            [ShopKind.GranaryYard] = new[] { "Grain by the sack, fair measure!" },
            [ShopKind.FarmStall] = new[] { "Fresh from the fields this morning!", "Eggs, greens, good bread — come and see." },
            [ShopKind.Provisioner] = new[] { "Bread and cheese for the road!", "Dried meat, hard biscuit — keeps for weeks!" },
            [ShopKind.Leatherworker] = new[] { "Good leather, well stitched!", "Belts, straps and satchels — come look." },
            [ShopKind.Tailor] = new[] { "Cloth and clothes, made to measure!", "A warm coat for the cold nights?" },
            [ShopKind.Bowyer] = new[] { "Bows and arrows, straight and true!", "Fresh fletching on every shaft." },
            [ShopKind.Apothecary] = new[] { "Remedies for what ails you!", "Salves and tinctures — ask and I'll help." },
            [ShopKind.Carpenter] = new[] { "Tools and timber, sound and seasoned!", "Need a handle or a chest? Step in." },
        };

        public static readonly string[] General = { "Come in, have a look!", "Good day! Need anything?" };

        public static string[] For(ShopKind kind) => Lines.TryGetValue(kind, out var l) ? l : General;

        /// <summary>The n-th call of a shop (so a keeper does not repeat the same line twice running).</summary>
        public static string Pick(ShopKind kind, int n)
        {
            var list = For(kind);
            return list[((n % list.Length) + list.Length) % list.Length];
        }
    }
}
