using System.Numerics;
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
    private const int WaterAtlasTile = 133; // должен совпадать с ConfigurePalette

    private float _totalTime = 0f;
    private float _timeOfDay = 0.25f; // 0.25 = полдень

    private Vector3 _sunDir = Vector3.Normalize(new Vector3(0.55f, 1.0f, 0.75f));
    private Vector3 _skyTop = new(0.26f, 0.50f, 0.92f);
    private Vector3 _skyHorizon = new(0.74f, 0.86f, 0.98f);
    private Vector3 _sunColor = new(1.00f, 0.95f, 0.82f);
    private Vector3 _ambientCol = new(0.30f, 0.35f, 0.40f);
    
    public float TimeOfDay => _timeOfDay;
    
    public void UpdateDayNight(float deltaTime)
    {
        _totalTime += deltaTime;
        // Полный цикл за 120 секунд (2 минуты)
        _timeOfDay = (_timeOfDay + deltaTime * (1.0f / 120.0f)) % 1.0f;

        // Угол солнца: полдень сверху, полночь снизу
        float sunAngle = (_timeOfDay - 0.25f) * MathF.PI * 2.0f;
        _sunDir = Vector3.Normalize(new Vector3(MathF.Cos(sunAngle), MathF.Sin(sunAngle), 0.3f));

        // Вычисляем факторы дня, заката и ночи
        float sunHeight = _sunDir.Y;
        float dayBlend = Math.Clamp(sunHeight * 5.0f, 0.0f, 1.0f);
        float nightBlend = Math.Clamp(-sunHeight * 5.0f, 0.0f, 1.0f);
        float sunsetBlend = Math.Clamp(1.0f - dayBlend - nightBlend, 0.0f, 1.0f);

        // Цвета для разных времен суток
        Vector3 dTop = new(0.26f, 0.50f, 0.92f),
            dHor = new(0.74f, 0.86f, 0.98f),
            dSun = new(1.00f, 0.95f, 0.82f),
            dAmb = new(0.30f, 0.35f, 0.40f);
        Vector3 sTop = new(0.20f, 0.25f, 0.50f),
            sHor = new(0.90f, 0.45f, 0.20f),
            sSun = new(1.00f, 0.50f, 0.20f),
            sAmb = new(0.25f, 0.20f, 0.20f);
        Vector3 nTop = new(0.02f, 0.02f, 0.05f),
            nHor = new(0.05f, 0.05f, 0.15f),
            nSun = new(0.20f, 0.25f, 0.40f),
            nAmb = new(0.08f, 0.08f, 0.15f);

        // Интерполяция
        _skyTop = dTop * dayBlend + sTop * sunsetBlend + nTop * nightBlend;
        _skyHorizon = dHor * dayBlend + sHor * sunsetBlend + nHor * nightBlend;
        _sunColor = dSun * dayBlend + sSun * sunsetBlend + nSun * nightBlend;
        _ambientCol = dAmb * dayBlend + sAmb * sunsetBlend + nAmb * nightBlend;
    }

    public void RenderClearScreen(byte r, byte g, byte b, Action<byte[], int, int>? overlay)
    {
        Array.Fill(_screenImage, (byte)255); // Очистка
        // Заливаем фон
        UiPainter.FillRect(_screenImage, (int)_renderW, (int)_renderH, 0, 0, (int)_renderW, (int)_renderH, r, g, b, 255);
    
        overlay?.Invoke(_screenImage, (int)_renderW, (int)_renderH);
        _screen.UploadLowResImage(_screenImage, _renderW, _renderH);
        _screen.Render(_realW, _realH);
    }
    
    public void SetTimeOfDay(float time)
    {
        _timeOfDay = time;
    }
    
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
        Camera camera,
        int breakX, int breakY, int breakZ, float breakProgress,
        Action<byte[], int, int>? overlay = null)
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
        // _ddaShader.SetUniformFloat("uTime", (float)DateTime.Now.TimeOfDay.TotalSeconds);
        _ddaShader.SetUniformFloat("uTime", _totalTime);
        _ddaShader.SetUniformVec3f("uSunDir", _sunDir);
        _ddaShader.SetUniformVec3f("uSkyTop", _skyTop);
        _ddaShader.SetUniformVec3f("uSkyHorizon", _skyHorizon);
        _ddaShader.SetUniformVec3f("uSunColor", _sunColor);
        _ddaShader.SetUniformVec3f("uAmbientColor", _ambientCol);


        int localBreakX = breakX - originBlockX;
        int localBreakY = breakY - originBlockY;
        int localBreakZ = breakZ - originBlockZ;

        _ddaShader.SetUniformInt("uBreakX", localBreakX);
        _ddaShader.SetUniformInt("uBreakY", localBreakY);
        _ddaShader.SetUniformInt("uBreakZ", localBreakZ);
        _ddaShader.SetUniformFloat("uBreakProgress", breakProgress);
        _atlas.Bind(_ddaShader, 1);
        _palette.Bind();

        camera.Render(_ddaShader);

        DispatchAndDownload();

        overlay?.Invoke(_screenImage, (int)_renderW, (int)_renderH);

        _screen.UploadLowResImage(_screenImage, _renderW, _renderH);
        _screen.Render(_realW, _realH);
    }

    /// <summary>Полупрозрачная вода: 0 = полностью прозрачна, 255 = полностью непрозрачна.</summary>
    public void ApplyWaterAlpha(byte alpha) => _atlas.ApplyTileAlpha(WaterAtlasTile, alpha);


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