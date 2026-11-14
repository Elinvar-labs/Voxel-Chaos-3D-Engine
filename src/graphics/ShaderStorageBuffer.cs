using Silk.NET.OpenGL;
using System;

public class ByteShaderStorageBuffer
{
    private uint _ssboId;
    private GL _gl;

    public ByteShaderStorageBuffer(GL gl)
    {
        _gl = gl;
    }

    public uint GetID()
    {
        return _ssboId;
    }

    public void Initialize(byte[] initialData)
    {
        _ssboId = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _ssboId);

        unsafe
        {
            // Fix byte array in memory
            fixed (byte* ptr = initialData)
            {
                nuint size = (nuint)initialData.Length;
                // Send raw bytes to GPU
                _gl.BufferData(BufferTargetARB.ShaderStorageBuffer, size, ptr, BufferUsageARB.DynamicDraw);
            }
        }

        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _ssboId);
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }

    public void UpdateData(byte[] updatedData)
    {
        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _ssboId);

        unsafe
        {
            fixed (byte* ptr = updatedData)
            {
                nuint size = (nuint)updatedData.Length;
                _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, size, ptr);
            }
        }

        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }

    public void DownloadData(byte[] destinationArray)
    {
        // Wait until shader finish writing to SSBO
        _gl.MemoryBarrier(MemoryBarrierMask.ShaderStorageBarrierBit);

        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, _ssboId);
        nuint size = (nuint)destinationArray.Length;

        unsafe
        {
            void* gpuPtr = _gl.MapBufferRange(
                BufferTargetARB.ShaderStorageBuffer,
                0,
                size,
                (uint)MapBufferAccessMask.ReadBit
            );

            if (gpuPtr == null)
                throw new InvalidOperationException("Error: SSBO: Cannot project GPU memory.");

            fixed (byte* destPtr = destinationArray)
            {
                // Copy from GPU pointer to byte array
                System.Buffer.MemoryCopy(gpuPtr, destPtr, size, size);
            }

            _gl.UnmapBuffer(BufferTargetARB.ShaderStorageBuffer);
        }

        _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
    }

    public void Bind(uint bindingPoint = 0)
    {
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, bindingPoint, _ssboId);
    }
}