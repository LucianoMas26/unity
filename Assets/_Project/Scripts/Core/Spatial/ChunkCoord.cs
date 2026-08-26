using System;
using UnityEngine;

namespace Survival.Core
{
    /// <summary>
    /// Integer address of one world cell on the XZ plane. Every generator keys its
    /// deterministic output off this, which is what lets the same cell be unloaded and
    /// rebuilt later, byte for byte.
    /// </summary>
    [Serializable]
    public struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int X;
        public int Z;

        public ChunkCoord(int x, int z) { X = x; Z = z; }

        public static ChunkCoord FromWorld(Vector3 worldPosition, float chunkSize)
            => new ChunkCoord(
                Mathf.FloorToInt(worldPosition.x / chunkSize),
                Mathf.FloorToInt(worldPosition.z / chunkSize));

        public Vector3 ToWorldOrigin(float chunkSize) => new Vector3(X * chunkSize, 0f, Z * chunkSize);

        public Vector3 ToWorldCentre(float chunkSize)
            => new Vector3((X + 0.5f) * chunkSize, 0f, (Z + 0.5f) * chunkSize);

        /// <summary>Ring distance. Chunks stream in square rings, so this is the natural metric.</summary>
        public static int ChebyshevDistance(ChunkCoord a, ChunkCoord b)
            => Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Z - b.Z));

        public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);
        public override int GetHashCode() => unchecked((int)DeterministicHash.Hash(0x5EEDu, X, Z));
        public override string ToString() => $"({X}, {Z})";

        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);
    }
}
