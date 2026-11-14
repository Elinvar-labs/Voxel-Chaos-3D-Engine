using System.Diagnostics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using VoxelChaos.Core;
using VoxelChaos.scene;

namespace VoxelChaos;

public class VoxelChaos
{
    private static IWindow _window;
    private static GL _gl;
    private static uint _program;
    private static uint _texture;
    private static World world;
    private static readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private static int _frameCount;
    private static double _currentFps;
    private static readonly uint WIN_WIDTH = 1280;
    private static readonly uint WIN_HEIGHT = 720;

    public static void Main(string[] args)
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>((int)WIN_WIDTH, (int)WIN_HEIGHT),
            Title = "VoxelChaos Project"
        };
        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Run();
    }

    private static void OnLoad()
    {
        Input.Initialize(_window);
        _gl = _window.CreateOpenGL();
        // _gl.ClearColor(Color.CornflowerBlue);
        // _gl.Enable(EnableCap.Blend);
        // _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        world = new World(_gl, 100, 37, 100, WIN_WIDTH, WIN_HEIGHT, 640, 360);
    }


    private static void OnUpdate(double deltaTime)
    {
        if (Input.Jpressed(Key.Escape)) _window.Close();


        world.Update();
        Input.PullEvents();
        _frameCount++;
        if (_fpsStopwatch.Elapsed.TotalSeconds >= 1.0)
        {
            _currentFps = _frameCount / _fpsStopwatch.Elapsed.TotalSeconds;
            _frameCount = 0;
            _fpsStopwatch.Restart();
            _window.Title = $"VoxelChaos Project | [FPS: {_currentFps:F0}]";
        }
    }

    private static void OnRender(double deltaTime)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        world.Render();
    }
}