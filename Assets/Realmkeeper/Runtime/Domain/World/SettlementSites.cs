using System.Globalization;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// The stable keys of a settlement's sites, shared by the local-environment layout (which
    /// places a building or area under each key) and Phase 4 locations (which refer to them).
    /// One source, so a location always names a site the layout builds.
    /// </summary>
    public static class SettlementSites
    {
        /// <summary>The enterable hall on the plaza (the tavern).</summary>
        public const string Hall = "hall";

        public const string Granary = "granary";

        /// <summary>The tall landmark: a keep tower (town), chapel tower (village) or watchtower (hamlet).</summary>
        public const string Landmark = "landmark";

        public const string Plaza = "plaza";

        /// <summary>The south gate (walled settlements) or entrance.</summary>
        public const string Gate = "gate";

        public static string House(int index) => "house-" + index.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Houses the layout builds for a settlement type (house-0 … house-(n−1)). These are the lots the
        /// plan has room for: 10 in a town, 7 in a village, 2 in a hamlet.
        /// </summary>
        public static int HouseCount(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.City:
                case SettlementType.Town:
                case SettlementType.Fortress:
                    return 10;
                case SettlementType.Village:
                    return 7;
                default:
                    return 2;
            }
        }
    }
}
