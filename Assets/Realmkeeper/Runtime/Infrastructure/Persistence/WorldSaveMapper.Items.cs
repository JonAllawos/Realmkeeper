using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>Schema v13 (Phase 11): containers, the player's work, gathering sites and the item catalog version.</summary>
    public static partial class WorldSaveMapper
    {
        private static void WriteItemWorld(WorldData w, WorldDto dto)
        {
            dto.ItemCatalogVersion = w.ItemCatalogVersion;
            dto.Containers = new List<ContainerDto>();
            foreach (var c in w.Containers)
            {
                dto.Containers.Add(new ContainerDto
                {
                    Key = c.Key, Kind = c.Kind.ToString(), Name = c.Name, SettlementId = c.SettlementId.IsEmpty ? null : c.SettlementId.ToString(), LayoutKey = c.LayoutKey,
                    PlayerAccess = c.PlayerAccess, AcceptsDeposits = c.AcceptsDeposits, Items = c.Items.ConvertAll(StackDto), Coin = c.Coin, CapacityGrams = c.CapacityGrams,
                    SourceKey = c.SourceKey, CreatedAt = c.CreatedAt, Opened = c.Opened,
                });
            }

            dto.WorkJobs = new List<WorkJobDto>();
            foreach (var j in w.WorkJobs)
            {
                dto.WorkJobs.Add(new WorkJobDto
                {
                    Key = j.Key, Kind = j.Kind.ToString(), SettlementId = j.SettlementId.IsEmpty ? null : j.SettlementId.ToString(), RecipeId = j.RecipeId, SiteKey = j.SiteKey,
                    StackId = j.StackId.IsEmpty ? null : j.StackId.ToString(), Held = j.Held.ConvertAll(StackDto), StartedAt = j.StartedAt, ReadyAt = j.ReadyAt,
                    Roll = j.Roll, Total = j.Total, Dc = j.Dc, Succeeded = j.Succeeded, OutputItemId = j.OutputItemId, OutputQuantity = j.OutputQuantity,
                    OutputQuality = j.OutputQuality.ToString(), Outcome = j.Outcome, Done = j.Done,
                });
            }

            dto.GatheringSites = new List<GatheringSiteDto>();
            foreach (var s in w.GatheringSites)
            {
                dto.GatheringSites.Add(new GatheringSiteDto
                {
                    Key = s.Key, SettlementId = s.SettlementId.ToString(), Kind = s.Kind, Name = s.Name, ItemId = s.ItemId, ToolDiscipline = s.ToolDiscipline,
                    Available = s.Available, Capacity = s.Capacity, PerWeek = s.PerWeek, Yield = s.Yield, Minutes = s.Minutes,
                });
            }
        }

        private static void ReadItemWorld(WorldDto dto, WorldData world)
        {
            world.ItemCatalogVersion = dto.ItemCatalogVersion ?? 1;
            foreach (var d in dto.Containers ?? new List<ContainerDto>())
            {
                if (d == null) throw new SaveDataException("Null container entry.");
                if (string.IsNullOrEmpty(d.Key)) throw new SaveDataException("A container has no key.");
                var c = new ContainerData
                {
                    Key = d.Key, Kind = ParseEnum<ContainerKind>(d.Kind, "container kind"), Name = d.Name,
                    SettlementId = string.IsNullOrEmpty(d.SettlementId) ? default : new SettlementId(RequiredGuid(d.SettlementId, "container settlementId")),
                    LayoutKey = d.LayoutKey, PlayerAccess = d.PlayerAccess, AcceptsDeposits = d.AcceptsDeposits, Coin = d.Coin, CapacityGrams = d.CapacityGrams,
                    SourceKey = d.SourceKey, CreatedAt = d.CreatedAt, Opened = d.Opened,
                };
                foreach (var s in d.Items ?? new List<ItemStackDto>()) c.Items.Add(Stack(s, "container item"));
                world.Containers.Add(c);
            }

            foreach (var d in dto.WorkJobs ?? new List<WorkJobDto>())
            {
                if (d == null) throw new SaveDataException("Null work entry.");
                if (string.IsNullOrEmpty(d.Key)) throw new SaveDataException("A piece of work has no key.");
                var j = new WorkJobData
                {
                    Key = d.Key, Kind = ParseEnum<WorkKind>(d.Kind, "work kind"),
                    SettlementId = string.IsNullOrEmpty(d.SettlementId) ? default : new SettlementId(RequiredGuid(d.SettlementId, "work settlementId")),
                    RecipeId = d.RecipeId, SiteKey = d.SiteKey,
                    StackId = string.IsNullOrEmpty(d.StackId) ? default : new ItemStackId(RequiredGuid(d.StackId, "work stackId")),
                    StartedAt = d.StartedAt, ReadyAt = d.ReadyAt, Roll = d.Roll, Total = d.Total, Dc = d.Dc, Succeeded = d.Succeeded,
                    OutputItemId = d.OutputItemId, OutputQuantity = d.OutputQuantity,
                    OutputQuality = string.IsNullOrEmpty(d.OutputQuality) ? ItemQuality.Common : ParseEnum<ItemQuality>(d.OutputQuality, "work outputQuality"),
                    Outcome = d.Outcome ?? string.Empty, Done = d.Done,
                };
                foreach (var s in d.Held ?? new List<ItemStackDto>()) j.Held.Add(Stack(s, "work item"));
                world.WorkJobs.Add(j);
            }

            foreach (var d in dto.GatheringSites ?? new List<GatheringSiteDto>())
            {
                if (d == null) throw new SaveDataException("Null gathering site entry.");
                world.GatheringSites.Add(new GatheringSiteData
                {
                    Key = d.Key, SettlementId = new SettlementId(RequiredGuid(d.SettlementId, "gathering site settlementId")), Kind = d.Kind, Name = d.Name, ItemId = d.ItemId,
                    ToolDiscipline = d.ToolDiscipline, Available = d.Available, Capacity = d.Capacity, PerWeek = d.PerWeek, Yield = d.Yield, Minutes = d.Minutes,
                });
            }

            ItemCatalog.Upgrade(world); // an older catalog's items follow their renames; the world records this version
        }
    }
}
