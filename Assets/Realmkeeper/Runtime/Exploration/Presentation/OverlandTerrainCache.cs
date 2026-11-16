using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 29 (ADR-0122): the Unity Terrain built for a region, kept for the next entry. The ground is a pure function of the
    /// session's <see cref="OverlandTerrain"/> (an immutable snapshot: its cells and corner heights are fixed when it is
    /// made, and its noise is a hash of its seed), so what a build produced from one terrain is exactly what it would produce
    /// again. An entry is found only for that same terrain object, and only when a fingerprint of the terrain (size, kind and
    /// a grid of its own heights and biomes) and the build's format still match.
    ///
    /// Invalidation is by construction, not by events:
    /// - a new, loaded or replaced world clears the session's terrains (WorldSession.Replace), so later entries bring new
    ///   terrain objects that no entry matches;
    /// - entries hold their terrain only weakly, so a discarded world's terrain is not kept alive, and its entry is dropped
    ///   when found dead;
    /// - the build's own inputs (heightmap resolution, texels per cell, format) are part of the fingerprint.
    ///
    /// Memory: at most <see cref="Capacity"/> regions, least recently used evicted first (their TerrainData and textures
    /// destroyed). The cache owns what it holds: the overland world that built or borrowed them does not destroy them.
    /// Main thread only (Unity objects); a lock keeps the bookkeeping whole if it is ever reached from elsewhere.
    /// Nothing here is state: losing the cache only costs a rebuild.
    /// </summary>
    public static class OverlandTerrainCache
    {
        /// <summary>Bump when the terrain build changes what it produces (heights, textures or their encoding).</summary>
        public const int FormatVersion = 2; // 2: the biome colour is linear half-float (Phase 29 parity fix)

        /// <summary>How many regions are kept (the region walked, the one before, and one more: a road there and back).</summary>
        public static int Capacity { get; set; } = 3;

        public sealed class Entry
        {
            internal WeakReference<OverlandTerrain> Source;
            public ulong Fingerprint { get; internal set; }
            public TerrainData Data { get; internal set; }
            public Texture2D Colour { get; internal set; }
            public Texture2D Weights { get; internal set; }

            /// <summary>The terrain object's height (the lowest ground), metres.</summary>
            public float Lo { get; internal set; }

            /// <summary>Native memory the entry holds (TerrainData and both textures), as Unity's profiler reports it.</summary>
            public long Bytes { get; internal set; }

            internal bool Alive => Data != null && Colour != null && Weights != null && Source.TryGetTarget(out _);
        }

        private static readonly object Gate = new object();
        private static readonly LinkedList<Entry> Lru = new LinkedList<Entry>(); // most recent first

        public static int Hits { get; private set; }
        public static int Misses { get; private set; }
        public static int Evictions { get; private set; }
        public static int Count { get { lock (Gate) return Lru.Count; } }

        public static long RetainedBytes
        {
            get
            {
                lock (Gate)
                {
                    long sum = 0;
                    foreach (var e in Lru) sum += e.Bytes;
                    return sum;
                }
            }
        }

        public static void ResetCounters()
        {
            lock (Gate) { Hits = 0; Misses = 0; Evictions = 0; }
        }

        /// <summary>The entry built from this very terrain, still whole and still matching it; null (a miss) otherwise.</summary>
        public static Entry Find(OverlandTerrain terrain)
        {
            if (terrain == null) return null;
            var print = Fingerprint(terrain);
            lock (Gate)
            {
                for (var node = Lru.First; node != null;)
                {
                    var next = node.Next;
                    var e = node.Value;
                    if (!e.Alive) { Drop(node); node = next; continue; } // its world is gone, or Unity unloaded its objects
                    if (e.Source.TryGetTarget(out var t) && ReferenceEquals(t, terrain))
                    {
                        if (e.Fingerprint != print) { Drop(node); break; } // same object, different ground: never reuse
                        Lru.Remove(node);
                        Lru.AddFirst(node);
                        Hits++;
                        Trim(); // a lowered capacity applies at once, not at the next store
                        return e;
                    }

                    node = next;
                }

                Misses++;
                Trim();
                return null;
            }
        }

        /// <summary>Keeps what a build made from <paramref name="terrain"/>; the cache owns these objects from now on.</summary>
        public static Entry Store(OverlandTerrain terrain, TerrainData data, Texture2D colour, Texture2D weights, float lo)
        {
            if (terrain == null || data == null || colour == null || weights == null || Capacity <= 0) return null;
            foreach (var o in new UnityEngine.Object[] { data, colour, weights }) o.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            var e = new Entry
            {
                Source = new WeakReference<OverlandTerrain>(terrain), Fingerprint = Fingerprint(terrain),
                Data = data, Colour = colour, Weights = weights, Lo = lo,
                Bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(data) + UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(colour) + UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(weights),
            };
            lock (Gate)
            {
                for (var node = Lru.First; node != null;)
                {
                    var next = node.Next;
                    if (!node.Value.Alive || (node.Value.Source.TryGetTarget(out var t) && ReferenceEquals(t, terrain))) Drop(node);
                    node = next;
                }

                Lru.AddFirst(e);
                Trim();
            }

            return e;
        }

        /// <summary>Evicts the least recently used entries beyond <see cref="Capacity"/> (call under the lock).</summary>
        private static void Trim()
        {
            while (Lru.Count > Math.Max(0, Capacity)) { Drop(Lru.Last); Evictions++; }
        }

        /// <summary>Whether these objects belong to a live cache entry (the overland world must not destroy them).</summary>
        public static bool Owns(TerrainData data)
        {
            if (data == null) return false;
            lock (Gate)
                foreach (var e in Lru)
                    if (e.Data == data) return true;
            return false;
        }

        /// <summary>Drops every entry and destroys what it held.</summary>
        public static void Clear()
        {
            lock (Gate)
                while (Lru.First != null) Drop(Lru.First);
        }

        private static void Drop(LinkedListNode<Entry> node)
        {
            var e = node.Value;
            Lru.Remove(node);
            // Another overland world may still be drawing these for a frame: destroy at the end of the frame, not now.
            foreach (var o in new UnityEngine.Object[] { e.Data, e.Colour, e.Weights })
                if (o != null)
                {
                    o.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
                    if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(o);
                    else UnityEngine.Object.DestroyImmediate(o);
                }
        }

        /// <summary>
        /// The terrain's identity for the build: its size and kind, the build's own constants and format, and a 33 × 33 grid
        /// of its heights (to the millimetre) and biomes. About a thousand height reads: well under a millisecond.
        /// </summary>
        public static ulong Fingerprint(OverlandTerrain t)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                void Mix(long v) { h ^= (ulong)v; h *= 1099511628211UL; }
                Mix(FormatVersion); Mix(OverlandWorld.HeightmapResolution); Mix(OverlandWorld.TexelsPerCell);
                Mix(t.Map.Width); Mix(t.Map.Depth); Mix((long)t.Kind);
                const int n = 33;
                var sx = t.SizeX;
                var sz = t.SizeZ;
                for (var z = 0; z < n; z++)
                    for (var x = 0; x < n; x++)
                    {
                        var px = x * sx / (n - 1);
                        var pz = z * sz / (n - 1);
                        Mix((long)Math.Round(t.HeightAt(px, pz) * 1000.0));
                        Mix((long)t.CellAt(Math.Min(px, sx - 0.01f), Math.Min(pz, sz - 0.01f)).Biome);
                    }

                return h;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // With domain reload off, statics outlive Play mode while its objects do not: start each session empty.
            lock (Gate) Lru.Clear();
            Hits = 0;
            Misses = 0;
            Evictions = 0;
        }
    }
}
