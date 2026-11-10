using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Domain
{
    /// <summary>One named resident to place in the environment (persistent ID and role only).</summary>
    public readonly struct ResidentInput
    {
        public ResidentInput(NpcId npcId, NpcRole role, string siteKey = null, LocationKind? locationKind = null)
        {
            NpcId = npcId;
            Role = role;
            SiteKey = siteKey;
            LocationKind = locationKind;
        }

        public NpcId NpcId { get; }
        public NpcRole Role { get; }

        /// <summary>Phase 4: the layout site of where the world says they are today (null: place by role).</summary>
        public string SiteKey { get; }

        public LocationKind? LocationKind { get; }
    }

    /// <summary>
    /// Deterministic local-environment layout for a settlement: the same (world seed, settlement ID,
    /// type, residents) always gives the same streets, buildings, props, interaction points and
    /// arrival point. Vertical-slice generator: one plaza-centred plan scaled by settlement type.
    /// Layout randomness is visual only and never touches simulation streams.
    /// </summary>
    public static class LocalEnvironmentGenerator
    {
        public const float StreetWidth = 5f;
        public const int Version = 1;

        public static LocalEnvironmentLayout Generate(long worldSeed, SettlementId settlementId, RegionId regionId, SettlementType type,
            IReadOnlyList<ResidentInput> residents)
        {
            if (settlementId.IsEmpty) throw new ArgumentException("A settlement ID is required.", nameof(settlementId));
            if (regionId.IsEmpty) throw new ArgumentException("A region ID is required.", nameof(regionId));
            residents = residents ?? Array.Empty<ResidentInput>();

            var rng = new LayoutRandom(worldSeed, settlementId);
            var half = HalfExtentFor(type);
            var layout = new LocalEnvironmentLayout
            {
                SettlementId = settlementId,
                RegionId = regionId,
                Type = type,
                HalfExtent = half,
                PlazaRadius = type == SettlementType.Hamlet ? 8f : 11f,
                HasWalls = type == SettlementType.Town || type == SettlementType.City || type == SettlementType.Fortress,
                ArrivalPose = new LocalPose(0f, 0f, -half + 4f, 0f),
            };

            var plaza = layout.PlazaRadius;

            // Streets: gate to plaza (north–south) and one east–west street through the plaza.
            layout.Streets.Add(new LayoutStreet { From = new LocalPoint(0f, -half), To = new LocalPoint(0f, half - 14f), Width = StreetWidth });
            layout.Streets.Add(new LayoutStreet { From = new LocalPoint(-half + 6f, 0f), To = new LocalPoint(half - 6f, 0f), Width = StreetWidth });

            // Landmarks around the plaza: the enterable hall (north), the granary (east), the tower (far north).
            layout.Buildings.Add(new LayoutBuilding
            {
                Key = SettlementSites.Hall, Kind = BuildingKind.Hall, Center = new LocalPoint(0f, plaza + 8f),
                Width = 12f, Depth = 10f, Height = 6f, Yaw = 0f, Style = 0,
            });
            layout.Buildings.Add(new LayoutBuilding
            {
                Key = SettlementSites.Granary, Kind = BuildingKind.Granary, Center = new LocalPoint(plaza + 9f, 9f),
                Width = 9f, Depth = 7f, Height = 5f, Yaw = 270f, Style = 1,
            });
            layout.Buildings.Add(new LayoutBuilding
            {
                Key = SettlementSites.Landmark, Kind = BuildingKind.Landmark, Center = new LocalPoint(0f, half - 9f),
                Width = 6f, Depth = 6f, Height = type == SettlementType.Hamlet ? 9f : layout.HasWalls ? 18f : 13f, Yaw = 0f, Style = 2,
            });

            // Houses on lots along both streets, facing the street.
            var target = HouseCountFor(type);
            var lots = new List<(LocalPoint center, float yaw)>();
            for (var z = -half + 12f; z <= -plaza - 7f; z += 12f)
            {
                lots.Add((new LocalPoint(-(StreetWidth / 2f + 7f), z), 90f));
                lots.Add((new LocalPoint(StreetWidth / 2f + 7f, z), 270f));
            }

            for (var x = plaza + 18f; x <= half - 10f; x += 12f)
            {
                lots.Add((new LocalPoint(x, -(StreetWidth / 2f + 7f)), 180f));
                lots.Add((new LocalPoint(-x, -(StreetWidth / 2f + 7f)), 180f));
                lots.Add((new LocalPoint(-x, StreetWidth / 2f + 7f), 0f));
            }

            for (var i = 0; i < lots.Count && layout.Buildings.Count - 3 < target; i++)
            {
                var lot = lots[i];
                layout.Buildings.Add(new LayoutBuilding
                {
                    Key = SettlementSites.House(layout.Buildings.Count - 3),
                    Kind = BuildingKind.House,
                    Center = lot.center,
                    Width = rng.Range(6.5f, 8.5f),
                    Depth = rng.Range(7.5f, 10f),
                    Height = rng.Range(4f, type == SettlementType.Hamlet ? 5f : 7f),
                    Yaw = lot.yaw,
                    Style = rng.Int(0, 3),
                });
            }

            // Plaza furniture and street dressing.
            layout.Props.Add(new LayoutProp { Kind = PropKind.Well, Position = new LocalPoint(0f, 0f) });
            for (var i = 0; i < 4; i++)
            {
                var angle = (45f + 90f * i) * (float)Math.PI / 180f;
                layout.Props.Add(new LayoutProp
                {
                    Kind = PropKind.MarketStall,
                    Position = new LocalPoint((float)Math.Cos(angle) * (plaza - 3f), (float)Math.Sin(angle) * (plaza - 3f)),
                    Yaw = 225f - 90f * i,
                });
            }

            for (var z = -half + 8f; z < -plaza; z += 10f)
            {
                layout.Props.Add(new LayoutProp { Kind = PropKind.LampPost, Position = new LocalPoint(StreetWidth / 2f + 0.6f, z) });
            }

            for (var i = 0; i < 8; i++)
            {
                layout.Props.Add(new LayoutProp
                {
                    Kind = i % 2 == 0 ? PropKind.Barrel : PropKind.Crate,
                    // Stacked along the granary's south side, clear of its door and the ledger.
                    Position = new LocalPoint(plaza + 6.5f + i * 0.9f + rng.Range(-0.2f, 0.2f), 3.4f + rng.Range(-0.3f, 0.3f)),
                    Yaw = rng.Range(0f, 90f),
                });
            }

            for (var i = 0; i < 10; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                layout.Props.Add(new LayoutProp
                {
                    Kind = PropKind.Tree,
                    Position = new LocalPoint(side * rng.Range(half - 9f, half - 3f), rng.Range(-half + 6f, half - 16f)),
                });
            }

            // Hall interior furniture (local to the world; the hall faces south onto the plaza).
            var hall = layout.Buildings[0];
            layout.Props.Add(new LayoutProp { Kind = PropKind.Table, Position = new LocalPoint(hall.Center.X - 2f, hall.Center.Z) });
            layout.Props.Add(new LayoutProp { Kind = PropKind.Bench, Position = new LocalPoint(hall.Center.X - 2f, hall.Center.Z - 1.4f) });
            layout.Props.Add(new LayoutProp { Kind = PropKind.Hearth, Position = new LocalPoint(hall.Center.X + 4f, hall.Center.Z + 3.6f) });

            // Interaction points: the granary ledger by the granary door, the notice board by the well.
            layout.Interactables.Add(new LayoutInteractable { Kind = InteractableKind.GranaryLedger, Position = new LocalPoint(plaza + 4.2f, 6f), Yaw = 270f });
            layout.Interactables.Add(new LayoutInteractable { Kind = InteractableKind.LabourNoticeBoard, Position = new LocalPoint(-4f, -plaza + 2f), Yaw = 0f });

            PlaceResidents(layout, residents, rng);
            return layout;
        }

        public static float HalfExtentFor(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.City:
                case SettlementType.Town:
                case SettlementType.Fortress:
                    return 52f;
                case SettlementType.Village:
                    return 42f;
                default:
                    return 34f;
            }
        }

        private static int HouseCountFor(SettlementType type) => SettlementSites.HouseCount(type);

        private static void PlaceResidents(LocalEnvironmentLayout layout, IReadOnlyList<ResidentInput> residents, LayoutRandom rng)
        {
            var plaza = layout.PlazaRadius;
            var hall = layout.Buildings[0].Center;
            var used = new HashSet<NpcId>();
            var index = 0;
            var siteIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in residents)
            {
                if (r.NpcId.IsEmpty || !used.Add(r.NpcId)) continue; // one spot per persistent identity
                LocalPoint anchor;
                float wander;
                if (SiteAnchor(layout, r, siteIndex, out anchor, out wander))
                {
                    layout.Residents.Add(new LayoutResidentSpot
                    {
                        NpcId = r.NpcId,
                        Role = r.Role,
                        SiteKey = r.SiteKey,
                        Position = new LocalPoint(anchor.X + rng.Range(-0.4f, 0.4f), anchor.Z + rng.Range(-0.4f, 0.4f)),
                        WanderRadius = wander,
                    });
                    siteIndex[r.SiteKey + "|" + r.LocationKind] = (siteIndex.TryGetValue(r.SiteKey + "|" + r.LocationKind, out var c) ? c : 0) + 1;
                    continue;
                }

                switch (r.Role)
                {
                    case NpcRole.Innkeeper: anchor = new LocalPoint(hall.X + 1.5f, hall.Z + 1f); wander = 1.2f; break;
                    case NpcRole.Merchant: anchor = new LocalPoint(plaza - 5f, -(plaza - 5f)); wander = 1.5f; break;
                    case NpcRole.Guard: anchor = new LocalPoint(3.5f, -layout.HalfExtent + 7f); wander = 2f; break;
                    case NpcRole.Farmer: anchor = new LocalPoint(plaza + 3f, 0f); wander = 3f; break;
                    case NpcRole.Elder: anchor = new LocalPoint(-3f, 3f); wander = 2f; break;
                    default:
                        anchor = new LocalPoint(-plaza + 2f + index, -plaza - 4f - index); wander = 4f; break;
                }

                layout.Residents.Add(new LayoutResidentSpot
                {
                    NpcId = r.NpcId,
                    Role = r.Role,
                    Position = new LocalPoint(anchor.X + rng.Range(-0.5f, 0.5f), anchor.Z + rng.Range(-0.5f, 0.5f)),
                    WanderRadius = wander,
                });
                index++;
            }
        }

        /// <summary>
        /// Where someone stands at a site (Phase 4): inside the tavern, among the plaza stalls, by the
        /// gate, or in front of a building's door. Several people at one site stand side by side.
        /// </summary>
        private static bool SiteAnchor(LocalEnvironmentLayout layout, ResidentInput r, Dictionary<string, int> siteIndex, out LocalPoint anchor, out float wander)
        {
            anchor = default;
            wander = 1.5f;
            if (string.IsNullOrEmpty(r.SiteKey)) return false;
            var n = siteIndex.TryGetValue(r.SiteKey + "|" + r.LocationKind, out var c) ? c : 0;
            var side = (n % 2 == 0 ? 1f : -1f) * (0.9f + 1.3f * (n / 2));
            var plaza = layout.PlazaRadius;
            if (r.SiteKey == SettlementSites.Plaza)
            {
                var market = r.LocationKind == LocationKind.Market;
                var angle = (market ? 0.6f : 3.6f) + n * 0.55f;
                var radius = market ? plaza - 4.5f : plaza - 2.5f;
                anchor = new LocalPoint((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius);
                wander = 1.5f;
                return true;
            }

            if (r.SiteKey == SettlementSites.Gate)
            {
                anchor = new LocalPoint(side * 1.4f + 2.5f, -layout.HalfExtent + 6f);
                wander = 1.5f;
                return true;
            }

            LayoutBuilding b = null;
            foreach (var candidate in layout.Buildings) if (candidate.Key == r.SiteKey) b = candidate;
            if (b == null) return false;
            if (b.Kind == BuildingKind.Hall)
            {
                // Inside the tavern, between the tables and the hearth.
                anchor = new LocalPoint(b.Center.X + 1.5f + side * 0.8f, b.Center.Z + 0.6f);
                wander = 0.5f;
                return true;
            }

            var rad = b.Yaw * Math.PI / 180.0;
            var doorX = (float)Math.Sin(rad);
            var doorZ = -(float)Math.Cos(rad);
            var out_ = b.Depth / 2f + 1.8f;
            anchor = new LocalPoint(b.Center.X + doorX * out_ - doorZ * side, b.Center.Z + doorZ * out_ + doorX * side);
            wander = 1.2f;
            return true;
        }

        /// <summary>Small xorshift stream seeded by world seed and settlement ID. Layout only.</summary>
        private sealed class LayoutRandom
        {
            private ulong _state;

            public LayoutRandom(long worldSeed, SettlementId settlementId)
            {
                var bytes = settlementId.Value.ToByteArray();
                var h = 1469598103934665603UL ^ unchecked((ulong)worldSeed);
                foreach (var b in bytes) h = (h ^ b) * 1099511628211UL;
                _state = h == 0 ? 0x9E3779B97F4A7C15UL : h;
            }

            private ulong Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 7;
                _state ^= _state << 17;
                return _state;
            }

            public float Range(float min, float max) => min + (max - min) * (float)((Next() >> 11) * (1.0 / 9007199254740992.0));

            public int Int(int min, int maxExclusive) => min + (int)(Next() % (ulong)Math.Max(1, maxExclusive - min));
        }
    }
}
