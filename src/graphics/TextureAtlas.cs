using System;
using System.IO;
using Silk.NET.OpenGL;
using StbImageSharp;
using Shader = VoxelChaos.Graphics.Shader;

namespace VoxelChaos.Graphics;

public class TextureAtlas : IDisposable
{
    public const int Size = 256;
    public const int TileSize = 16;
    public const int TilesPerRow = Size / TileSize;

    private readonly GL _gl;
    private uint _textureId;
    private byte[] _imageData; // храним CPU-копию для модификаций

    public TextureAtlas(GL gl, string path)
    {
        _gl = gl;

        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        if (image.Width != Size || image.Height != Size)
            throw new ArgumentException($"Atlas must be {Size}x{Size}, got {image.Width}x{image.Height}");

        _imageData = image.Data;

        _textureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _textureId);

        unsafe
        {
            fixed (byte* ptr = _imageData)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    (uint)Size, (uint)Size, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>Выставляет альфа-канал всем пикселям тайла (например, для полупрозрачной воды).</summary>
    public unsafe void ApplyTileAlpha(int tileIndex, byte alpha)
    {
        if (_imageData == null) return;

        int tx = (tileIndex % TilesPerRow) * TileSize;
        int ty = (tileIndex / TilesPerRow) * TileSize;

        for (int y = 0; y < TileSize; y++)
        for (int x = 0; x < TileSize; x++)
        {
            int idx = ((ty + y) * Size + (tx + x)) * 4 + 3;
            _imageData[idx] = alpha;
        }

        _gl.BindTexture(TextureTarget.Texture2D, _textureId);
        fixed (byte* ptr = _imageData)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                (uint)Size, (uint)Size,
                PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Bind(Shader shader, uint unit)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)unit);
        _gl.BindTexture(TextureTarget.Texture2D, _textureId);
        shader.SetUniformInt("atlasTexture", (int)unit);
    }

    public void Dispose()
    {
        if (_textureId != 0)
        {
            _gl.DeleteTexture(_textureId);
            _textureId = 0;
        }
    }
}