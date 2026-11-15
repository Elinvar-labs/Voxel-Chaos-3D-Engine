using System.Numerics;
using Silk.NET.OpenGL;
using VoxelChaos.Core;
using VoxelChaos.Graphics;
using VoxelChaos.Rendering;
using VoxelChaos.Scene;

namespace VoxelChaos.World;

public sealed class World
{
    private const string DdaShaderPath = "assets/shaders/DDA_Voxeling.glsl";
    private const int WorldSeed = 1337;

    private readonly TerrainGenerator _generator;
    private readonly StructurePlacer _structures;
    private readonly ChunkStorage _storage = new();
    private readonly VolumeBuilder _volume = new();
    private readonly WorldRenderer _renderer;

    public Camera camera { get; set; }

    public World(GL gl, float camX, float camY, float camZ,
        uint realWidth, uint realHeight, uint renderWidth, uint renderHeight)
    {
        camera = new Camera(new Vector3(camX, camY, camZ), 60, renderWidth, renderHeight);

        _renderer = new WorldRenderer(gl, DdaShaderPath, realWidth, realHeight, renderWidth, renderHeight);
        _generator = new TerrainGenerator { Seed = WorldSeed };
        _structures = new StructurePlacer(WorldSeed);

        ConfigurePalette(_renderer.Palette);
        _renderer.ApplyWaterAlpha(160);
        
        UpdateStreaming();
    }

    public void SetCamera(Camera cam) => camera = cam;   // поле camera → property без readonly

    public byte GetBlock(int wx, int wy, int wz)
    {
        if (wy < 0 || wy >= WorldConfig.WorldHeight) return Blocks.Air;
        int cx = wx >> Chunk.SHIFT, cy = wy >> Chunk.SHIFT, cz = wz >> Chunk.SHIFT;
        if (!_storage.TryGet(new ChunkPos(cx, cy, cz), out var ch)) return Blocks.Air;
        return ch.Get(wx & Chunk.MASK, wy & Chunk.MASK, wz & Chunk.MASK);
    }

    public bool IsSolid(int wx, int wy, int wz)
    {
        var b = GetBlock(wx, wy, wz);
        return b != Blocks.Air && b != Blocks.Water && b != Blocks.Lava;
    }

    public void SetBlock(int wx, int wy, int wz, byte block)
    {
        if (wy < 0 || wy >= WorldConfig.WorldHeight) return;
        int cx = wx >> Chunk.SHIFT, cy = wy >> Chunk.SHIFT, cz = wz >> Chunk.SHIFT;
        if (!_storage.TryGet(new ChunkPos(cx, cy, cz), out var ch)) return;

        ch.Set(wx & Chunk.MASK, wy & Chunk.MASK, wz & Chunk.MASK, block);

        int vx = wx - _volume.OriginChunkX * Chunk.SIZE;
        int vy = wy - _volume.OriginChunkY * Chunk.SIZE;
        int vz = wz - _volume.OriginChunkZ * Chunk.SIZE;

        if ((uint)vx < (uint)VolumeBuilder.SizeX &&
            (uint)vy < (uint)VolumeBuilder.SizeY &&
            (uint)vz < (uint)VolumeBuilder.SizeZ)
        {
            int idx = vx + vy * VolumeBuilder.SizeX + vz * VolumeBuilder.SizeX * VolumeBuilder.SizeY;
            _volume.Data[idx] = block;
            _renderer.UpdateVolumeBlock(vx, vy, vz, block);
        }
    }

    public void Render(Action<byte[], int, int>? overlay = null)
    {
        _renderer.Render(_volume.Data, VolumeBuilder.SizeX, VolumeBuilder.SizeY, VolumeBuilder.SizeZ,
            _volume.OriginChunkX * Chunk.SIZE,
            _volume.OriginChunkY * Chunk.SIZE,
            _volume.OriginChunkZ * Chunk.SIZE,
            camera, overlay);
    }
    private static void ConfigurePalette(BlockPalette palette)
    {
        palette.SetBlock(Blocks.Dirt,        55, 55, 55);
        palette.SetBlock(Blocks.Grass,       83, 55, 83);
        palette.SetBlock(Blocks.Stone,      128, 128, 128);
        palette.SetBlock(Blocks.Wood,       132, 132, 131);
        palette.SetBlock(Blocks.Leaves,      96, 96, 96);
        palette.SetBlock(Blocks.Sand,       116, 116, 116);
        palette.SetBlock(Blocks.Ice,         87, 87, 87);
        palette.SetBlock(Blocks.Snow,       125, 125, 125);
        palette.SetBlock(Blocks.Basalt,     102, 102, 102);
        palette.SetBlock(Blocks.Lava,        94, 94, 94);
        palette.SetBlock(Blocks.TempleStone,118, 118, 118);
        palette.SetBlock(Blocks.Water,      133, 133, 133);
        palette.Upload();
    }

    // ============================================================
    public void Update()
    {
        camera.Update();

        var p = camera.GetPosition();
        float clampedY = Math.Clamp(p.Y, 3f, 140f);
        if (clampedY != p.Y)
            camera.SetPosition(new Vector3(p.X, clampedY, p.Z));

        UpdateStreaming();
    }

    public void Render()
    {
        _renderer.Render(
            _volume.Data, VolumeBuilder.SizeX, VolumeBuilder.SizeY, VolumeBuilder.SizeZ,
            _volume.OriginChunkX * Chunk.SIZE,
            _volume.OriginChunkY * Chunk.SIZE,
            _volume.OriginChunkZ * Chunk.SIZE,
            camera);
    }

    // ============================================================
    private void UpdateStreaming()
    {
        var camPos = camera.GetPosition();
        int camCX = (int)MathF.Floor(camPos.X / Chunk.SIZE);
        int camCY = (int)MathF.Floor(camPos.Y / Chunk.SIZE);
        int camCZ = (int)MathF.Floor(camPos.Z / Chunk.SIZE);

        int newOX = camCX - WorldConfig.VolChunksX / 2;
        int newOY = Math.Clamp(camCY - WorldConfig.VolChunksY / 2,
                               0, WorldConfig.WorldHeightChunks - WorldConfig.VolChunksY);
        int newOZ = camCZ - WorldConfig.VolChunksZ / 2;

        bool originChanged = newOX != _volume.OriginChunkX
                          || newOY != _volume.OriginChunkY
                          || newOZ != _volume.OriginChunkZ;

        if (!_volume.NeedsRebuild && !originChanged) return;

        _volume.SetOrigin(newOX, newOY, newOZ);

        // 1) Генерация отсутствующих чанков в новом окне
        var toGen = new List<ChunkPos>(32);
        for (int cx = 0; cx < WorldConfig.VolChunksX; cx++)
        for (int cy = 0; cy < WorldConfig.VolChunksY; cy++)
        for (int cz = 0; cz < WorldConfig.VolChunksZ; cz++)
        {
            var pos = new ChunkPos(newOX + cx, newOY + cy, newOZ + cz);
            if (!_storage.Contains(pos)) toGen.Add(pos);
        }

        if (toGen.Count > 0)
        {
            var fresh = new Chunk[toGen.Count];
            Parallel.For(0, toGen.Count, i => fresh[i] = _generator.GenerateChunk(toGen[i]));
            for (int i = 0; i < fresh.Length; i++) _storage.Add(fresh[i]);

            foreach (var ch in fresh) _structures.PlaceAll(ch, _storage);
            _storage.FlushSpillover();
            _volume.MarkDirty();
        }

        // 2) Выгрузка чанков, вышедших из окна
        if (originChanged)
        {
            var toDel = new List<ChunkPos>();
            foreach (var pos in _storage.Chunks.Keys)
            {
                if (pos.X < newOX || pos.X >= newOX + WorldConfig.VolChunksX ||
                    pos.Y < newOY || pos.Y >= newOY + WorldConfig.VolChunksY ||
                    pos.Z < newOZ || pos.Z >= newOZ + WorldConfig.VolChunksZ)
                    toDel.Add(pos);
            }
            foreach (var d in toDel) _storage.Remove(d);

            // ФИКС БАГА: даже если toGen пуст, при смене origin объём устарел.
            _volume.MarkDirty();
        }

        // 3) Пересборка объёма при необходимости
        if (_volume.NeedsRebuild)
        {
            _volume.Rebuild(_storage);
            _renderer.MarkVolumeDirty();
        }
    }
}