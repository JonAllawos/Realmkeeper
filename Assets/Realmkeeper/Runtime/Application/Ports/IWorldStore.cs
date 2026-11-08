using System;
using System.Collections.Generic;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application.Ports
{
    public enum LoadStatus
    {
        Success = 0,
        NotFound = 1,

        /// <summary>Not parseable as a Realmkeeper world file.</summary>
        Corrupt = 2,

        /// <summary>Written by a newer build, or no migration path exists.</summary>
        UnsupportedVersion = 3,

        /// <summary>Parsed, but the content breaks world invariants.</summary>
        Invalid = 4,
    }

    public sealed class LoadResult
    {
        private LoadResult(LoadStatus status, WorldData world, IReadOnlyList<string> errors)
        {
            Status = status;
            World = world;
            Errors = errors;
        }

        public LoadStatus Status { get; }
        public WorldData World { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsSuccess => Status == LoadStatus.Success;

        public static LoadResult Success(WorldData world) => new LoadResult(LoadStatus.Success, world, Array.Empty<string>());

        public static LoadResult Failure(LoadStatus status, params string[] errors)
        {
            if (status == LoadStatus.Success) throw new ArgumentException("Use Success() for successful loads.", nameof(status));
            return new LoadResult(status, null, errors ?? Array.Empty<string>());
        }

        public static LoadResult Failure(LoadStatus status, IReadOnlyList<string> errors) =>
            new LoadResult(status, null, errors ?? Array.Empty<string>());
    }

    public sealed class SaveResult
    {
        private SaveResult(bool success, string location, string error)
        {
            IsSuccess = success;
            Location = location;
            Error = error;
        }

        public bool IsSuccess { get; }
        public string Location { get; }
        public string Error { get; }

        public static SaveResult Success(string location) => new SaveResult(true, location, null);
        public static SaveResult Failure(string error) => new SaveResult(false, null, error);
    }

    /// <summary>Port for persisting authoritative world data. Implemented by Realmkeeper.Infrastructure.</summary>
    public interface IWorldStore
    {
        bool Exists(string slotName);
        SaveResult Save(WorldData world, string slotName);
        LoadResult Load(string slotName);
    }
}
