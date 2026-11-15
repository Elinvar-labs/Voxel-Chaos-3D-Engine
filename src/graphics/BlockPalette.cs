using System;
using Silk.NET.OpenGL;

namespace VoxelChaos.Graphics;

// Packed layout: byte0=side, byte1=top, byte2=bottom, byte3=unused.
public class BlockPalette : IDisposable
{
    public const int MaxBlocks = 256;
    private const uint BindingPoint = 1;

    private readonly GL _gl;
    private uint _ssboId;
    private readonly uint[] _data = new uint[MaxBlocks];

    public BlockPalette(GL gl)
    {
        _gl = gl;
        _ssboId = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _ssboId);
        unsafe
        {
            fixed (uint* ptr = _data)
                _gl.BufferData(BufferTargetARB.ShaderStorageBuffer,
                    (nuint)(MaxBlocks * sizeof(uint)), ptr, BufferUsageARB.DynamicDraw);
        }
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }

    // Atlas tile indices, one byte per face
    public void SetBlock(byte blockId, byte top, byte bottom, byte side)
    {
        _data[blockId] = (uint)side | ((uint)top << 8) | ((uint)bottom << 16);
    }

    public void Upload()
    {
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _ssboId);
        unsafe
        {
            fixed (uint* ptr = _data)
                _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0,
                    (nuint)(MaxBlocks * sizeof(uint)), ptr);
        }
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }

    public void Bind()
    {
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, BindingPoint, _ssboId);
    }

    public void Dispose()
    {
        if (_ssboId != 0) { _gl.DeleteBuffer(_ssboId); _ssboId = 0; }
    }
}