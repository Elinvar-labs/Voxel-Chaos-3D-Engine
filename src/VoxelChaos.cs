using System.Diagnostics;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using VoxelChaos.Core;
using VoxelChaos.Gameplay;
using VoxelChaos.Rendering;
using VoxelChaos.World;

namespace VoxelChaos;

public sealed class Program : IDisposable
{
    private const uint WindowWidth  = 1280;
    private const uint WindowHeight = 720;
    private const uint RenderWidth  = 800;
    private const uint RenderHeight = 480;

    private const string WindowTitle = "VoxelChaos Project";

    private static readonly Vector3 SpawnFeet = new(64f, 62f, 64f);

    private IWindow? _window;
    private GL? _gl;
    private World.World? _world;

    private PlayerController? _player;
    private Inventory?        _inventory;
    private BlockInteraction? _interaction;
    private HotbarRenderer?   _hotbar;
    private HudRenderer?      _hud;

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

        // Включаем блендинг: он нужен для полупрозрачной воды (атлас alpha < 255)
        // и для UI-оверлеев с alpha < 255.
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _world = new World.World(_gl, SpawnFeet.X, SpawnFeet.Y + PlayerController.EyeHeight, SpawnFeet.Z,
                           WindowWidth, WindowHeight, RenderWidth, RenderHeight);

        _player = new PlayerController(SpawnFeet, 60, RenderWidth, RenderHeight, _world);
        _world.SetCamera(_player);

        _inventory   = new Inventory();
        _interaction = new BlockInteraction(_world, _player, _player, _inventory);
        _hotbar      = new HotbarRenderer(_inventory);
        _hud         = new HudRenderer();

        _fpsStopwatch.Start();
    }

    private void OnUpdate(double deltaTime)
    {
        if (Input.Jpressed(Key.Escape)) { _window!.Close(); return; }

        _world!.Update((float)deltaTime);       // двигает _player через Camera.ParseInput + streaming
        _inventory!.Update();   // hotbar 1..9 / колесо
        _interaction!.Update(); // hold-to-break + place
        Input.PullEvents();
        UpdateFpsTitle();
    }

    private void OnRender(double deltaTime)
    {
        _gl!.Clear(ClearBufferMask.ColorBufferBit);
        _world!.Render((img, w, h) =>
        {
            _hotbar!.Draw(img, w, h);
            _hud!.DrawDebug(img, w, h, _player!.BodyPosition, _player.Mode, _currentFps);
            _hud!.DrawCrosshair(img, w, h);
        });
    }

    private void UpdateFpsTitle()
    {
        _frameCount++;
        if (_fpsStopwatch.Elapsed.TotalSeconds < 1.0) return;
        _currentFps = _frameCount / _fpsStopwatch.Elapsed.TotalSeconds;
        _frameCount = 0;
        _fpsStopwatch.Restart();
        _window!.Title = $"{WindowTitle} | FPS: {_currentFps:F0} | {_player!.Mode}";
    }

    public void Dispose() => _window?.Dispose();
}