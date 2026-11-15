using System.Diagnostics;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using VoxelChaos.Core;   // Input — обёртка над Silk.NET.Input
using VoxelChaos.World;  // World (было VoxelChaos.scene)

namespace VoxelChaos;

/// <summary>Точка входа: окно Silk.NET, игровой цикл, FPS-счётчик.</summary>
public sealed class Program : IDisposable
{
    private const uint WindowWidth  = 1280;
    private const uint WindowHeight = 720;

    // Внутреннее разрешение compute-шейдера. Уменьшить (например, 640×360)
    // для большего FPS — PixelRenderer затем апскейлит.
    private const uint RenderWidth  = 800;
    private const uint RenderHeight = 480;

    private const string WindowTitle = "VoxelChaos Project";

    private static readonly Vector3 SpawnPosition = new(64f, 64f, 64f);

    private IWindow? _window;
    private GL? _gl;
    private World.World? _world;

    private readonly Stopwatch _fpsStopwatch = new();
    private int _frameCount;
    private double _currentFps;

    public static void Main(string[] args)
    {
        using var app = new Program();
        app.Run();
    }

    private void Run()
    {
        var options = WindowOptions.Default with
        {
            Size  = new Vector2D<int>((int)WindowWidth, (int)WindowHeight),
            Title = WindowTitle
        };

        _window = Window.Create(options);
        _window.Load   += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Run();
    }

    private void OnLoad()
    {
        Input.Initialize(_window!);
        _gl = _window!.CreateOpenGL();

        _world = new World.World(
            _gl,
            SpawnPosition.X, SpawnPosition.Y, SpawnPosition.Z,
            WindowWidth, WindowHeight,
            RenderWidth, RenderHeight);

        _fpsStopwatch.Start();
    }

    private void OnUpdate(double deltaTime)
    {
        if (Input.Jpressed(Key.Escape))
        {
            _window!.Close();
            return;
        }

        _world!.Update();
        Input.PullEvents();
        UpdateFpsTitle();
    }

    private void OnRender(double deltaTime)
    {
        _gl!.Clear(ClearBufferMask.ColorBufferBit);
        _world!.Render();
    }

    private void UpdateFpsTitle()
    {
        _frameCount++;
        if (_fpsStopwatch.Elapsed.TotalSeconds < 1.0) return;

        _currentFps = _frameCount / _fpsStopwatch.Elapsed.TotalSeconds;
        _frameCount = 0;
        _fpsStopwatch.Restart();
        _window!.Title = $"{WindowTitle} | FPS: {_currentFps:F0}";
    }

    public void Dispose()
    {
        // TODO: когда World/WorldRenderer реализуют IDisposable — вызвать _world?.Dispose()
        //       ДО _window.Dispose(), иначе GL-ресурсы (текстуры, шейдеры) утекут.
        _window?.Dispose();
    }
}