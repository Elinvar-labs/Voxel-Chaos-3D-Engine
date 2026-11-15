using System.Numerics;
using Silk.NET.Maths;
using VoxelChaos.World;

namespace VoxelChaos.Gameplay;

public static class VoxelRaycaster
{
    public static bool Raycast(World.World world, Vector3 origin, Vector3 dir, float maxDist,
        out Vector3D<int> hitBlock, out Vector3D<int> faceNormal)
    {
        hitBlock = default;
        faceNormal = default;

        dir = Vector3.Normalize(dir);

        int x = (int)MathF.Floor(origin.X);
        int y = (int)MathF.Floor(origin.Y);
        int z = (int)MathF.Floor(origin.Z);

        int stepX = dir.X > 0 ? 1 : dir.X < 0 ? -1 : 0;
        int stepY = dir.Y > 0 ? 1 : dir.Y < 0 ? -1 : 0;
        int stepZ = dir.Z > 0 ? 1 : dir.Z < 0 ? -1 : 0;

        const float INF = float.PositiveInfinity;
        float tDeltaX = stepX != 0 ? MathF.Abs(1f / dir.X) : INF;
        float tDeltaY = stepY != 0 ? MathF.Abs(1f / dir.Y) : INF;
        float tDeltaZ = stepZ != 0 ? MathF.Abs(1f / dir.Z) : INF;

        float tMaxX = stepX > 0 ? ((x + 1) - origin.X) / dir.X
                    : stepX < 0 ? (x - origin.X) / dir.X : INF;
        float tMaxY = stepY > 0 ? ((y + 1) - origin.Y) / dir.Y
                    : stepY < 0 ? (y - origin.Y) / dir.Y : INF;
        float tMaxZ = stepZ > 0 ? ((z + 1) - origin.Z) / dir.Z
                    : stepZ < 0 ? (z - origin.Z) / dir.Z : INF;

        int lastStep = 0;

        for (float t = 0f; t <= maxDist;)
        {
            if (world.IsSolid(x, y, z))
            {
                hitBlock = new Vector3D<int>(x, y, z);
                faceNormal = lastStep switch
                {
                    1 => new Vector3D<int>(-stepX, 0, 0),
                    2 => new Vector3D<int>(0, -stepY, 0),
                    3 => new Vector3D<int>(0, 0, -stepZ),
                    _ => default
                };
                return true;
            }

            if (tMaxX < tMaxY && tMaxX < tMaxZ) { x += stepX; t = tMaxX; tMaxX += tDeltaX; lastStep = 1; }
            else if (tMaxY < tMaxZ)             { y += stepY; t = tMaxY; tMaxY += tDeltaY; lastStep = 2; }
            else                                { z += stepZ; t = tMaxZ; tMaxZ += tDeltaZ; lastStep = 3; }
        }

        return false;
    }
}