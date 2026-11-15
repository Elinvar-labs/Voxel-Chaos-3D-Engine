using VoxelChaos.Scene;

namespace VoxelChaos.World;

/// <summary>Хранилище загруженных чанков + очередь отложенных модификаций (spillover).</summary>
public sealed class ChunkStorage
{
    private readonly Dictionary<ChunkPos, Chunk> _chunks = new();
    private readonly Dictionary<ChunkPos, List<VoxelMod>> _spillover = new();

    public IReadOnlyDictionary<ChunkPos, Chunk> Chunks => _chunks;

    public bool TryGet(ChunkPos pos, out Chunk chunk) => _chunks.TryGetValue(pos, out chunk!);
    public bool Contains(ChunkPos pos) => _chunks.ContainsKey(pos);

    public void Add(Chunk chunk)
    {
        _chunks[chunk.pos] = chunk;
        ApplySpilloverTo(chunk);
    }

    public void Remove(ChunkPos pos) => _chunks.Remove(pos);

    /// <summary>Записать блок, при необходимости — в отложенную очередь для ещё не загруженного чанка.</summary>
    public void SetWorld(Chunk source, int wx, int wy, int wz, byte block)
    {
        if (wy < 0 || wy >= WorldConfig.WorldHeight) return;

        int cx = wx >> Chunk.SHIFT, cy = wy >> Chunk.SHIFT, cz = wz >> Chunk.SHIFT;
        int lx = wx & Chunk.MASK, ly = wy & Chunk.MASK, lz = wz & Chunk.MASK;

        if (cx == source.pos.X && cy == source.pos.Y && cz == source.pos.Z)
        {
            source.Set(lx, ly, lz, block);
            return;
        }

        var targetPos = new ChunkPos(cx, cy, cz);
        if (_chunks.TryGetValue(targetPos, out var target))
        {
            target.Set(lx, ly, lz, block);
            return;
        }

        if (!_spillover.TryGetValue(targetPos, out var list))
            _spillover[targetPos] = list = new List<VoxelMod>(8);
        list.Add(new VoxelMod(wx, wy, wz, block));
    }

    private void ApplySpilloverTo(Chunk chunk)
    {
        if (!_spillover.TryGetValue(chunk.pos, out var mods)) return;
        foreach (var m in mods)
            chunk.Set(m.X & Chunk.MASK, m.Y & Chunk.MASK, m.Z & Chunk.MASK, m.Block);
    }

    /// <summary>Применить все отложенные модификации к уже загруженным чанкам (вызывается после пакетной генерации).</summary>
    public void FlushSpillover()
    {
        foreach (var (pos, mods) in _spillover)
        {
            if (!_chunks.TryGetValue(pos, out var chunk)) continue;
            foreach (var m in mods)
                chunk.Set(m.X & Chunk.MASK, m.Y & Chunk.MASK, m.Z & Chunk.MASK, m.Block);
        }
    }
}