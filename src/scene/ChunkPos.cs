namespace VoxelChaos.Scene;

public readonly struct ChunkPos : IEquatable<ChunkPos>
{
    public readonly int X, Y, Z;

    public ChunkPos(int x, int y, int z) { X = x; Y = y; Z = z; }

    public bool Equals(ChunkPos o) => X == o.X && Y == o.Y && Z == o.Z;
    public override bool Equals(object? o) => o is ChunkPos p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(ChunkPos a, ChunkPos b) => a.Equals(b);
    public static bool operator !=(ChunkPos a, ChunkPos b) => !a.Equals(b);
}