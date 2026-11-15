using Silk.NET.OpenGL;
using VoxelChaos.Core;
using VoxelChaos.Graphics;
using raymarching;
using VoxelChaos.graphics;
using Shader = VoxelChaos.Graphics.Shader;

namespace VoxelChaos.Rendering;

/// <summary>DDA compute-пайплайн: SSBO + 3D-текстура + палитра/атлас.</summary>
public sealed class WorldRenderer
{
    private readonly GL _gl;
    private readonly PixelRenderer _screen;
    private readonly Shader _ddaShader;
    private readonly ByteShaderStorageBuffer _ssbo;
    private readonly VoxelTexture3D _voxelTexture;
    private readonly TextureAtlas _atlas;
    private readonly BlockPalette _palette;
    private readonly byte[] _screenImage;
    private readonly uint _renderW, _renderH, _realW, _realH;

    private bool _volumeDirty = true;

    public WorldRenderer(GL gl, string computeShaderPath,
        uint realWidth, uint realHeight, uint renderWidth, uint renderHeight)
    {
        _gl = gl;
        _renderW = renderWidth;
        _renderH = renderHeight;
        _realW = realWidth;
        _realH = realHeight;

        _screen = new PixelRenderer();
        _screen.Initialize(gl, renderWidth, renderHeight, realWidth, realHeight);
        _screenImage = new byte[renderWidth * renderHeight * 4];

        _ssbo = new ByteShaderStorageBuffer(gl);
        _ssbo.Initialize(_screenImage);

        _ddaShader = new Shader(gl, computeShaderPath);
        _voxelTexture = new VoxelTexture3D(gl);

        _atlas = new TextureAtlas(gl, "assets/atlas.png");
        _palette = new BlockPalette(gl);
    }

    public BlockPalette Palette => _palette;
    public void MarkVolumeDirty() => _volumeDirty = true;

    public void Render(
        byte[] volume, int sizeX, int sizeY, int sizeZ,
        int originBlockX, int originBlockY, int originBlockZ,
        Camera camera, Action<byte[], int, int>? overlay = null)
    {
        Array.Fill(_screenImage, (byte)255);
        _ddaShader.Bind();

        if (_volumeDirty)
        {
            _voxelTexture.Update(volume, (uint)sizeX, (uint)sizeY, (uint)sizeZ);
            _volumeDirty = false;
        }

        _voxelTexture.Bind(_ddaShader);

        // ВАЖНО: origin — целые мировые координаты блоков.
        // Шейдер ожидает float в текущем API, но по смыслу это int (см. примечание в отчёте).
        _ddaShader.SetUniformFloat("volOriginX", originBlockX);
        _ddaShader.SetUniformFloat("volOriginY", originBlockY);
        _ddaShader.SetUniformFloat("volOriginZ", originBlockZ);
        _ddaShader.SetUniformInt("volSizeX", sizeX);
        _ddaShader.SetUniformInt("volSizeY", sizeY);
        _ddaShader.SetUniformInt("volSizeZ", sizeZ);

        _atlas.Bind(_ddaShader, 1);
        _palette.Bind();

        camera.Render(_ddaShader);

        DispatchAndDownload();
        
        overlay?.Invoke(_screenImage, (int)_renderW, (int)_renderH);
        
        _screen.UploadLowResImage(_screenImage, _renderW, _renderH);
        _screen.Render(_realW, _realH);
    }

    public void UpdateVolumeBlock(int x, int y, int z, byte b)
        => _voxelTexture.UpdateBlock(x, y, z, b);
    
    private void DispatchAndDownload()
    {
        int totalPixels = _screenImage.Length / 4;
        uint groupsX = (uint)Math.Ceiling((double)totalPixels / 256);

        _ddaShader.Bind();
        _ssbo.Bind();
        _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, _ssbo.GetID());
        _ddaShader.SetUniformInt("screenWidth", (int)_renderW);
        _ddaShader.SetUniformInt("screenHeight", (int)_renderH);
        _gl.DispatchCompute(groupsX, 1, 1);
        _ssbo.DownloadData(_screenImage);
    }
}