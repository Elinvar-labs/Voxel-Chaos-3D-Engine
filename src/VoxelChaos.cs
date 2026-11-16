using System;
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
    private const int DefaultSeed = 1337;

    // Добавили состояние Paused
    private enum GameState { MainMenu, Playing, Paused }
    private GameState _state = GameState.MainMenu;

    private IWindow? _window;
    private GL? _gl;
    private World.World? _world;

    private PlayerController? _player;
    private Inventory?        _inventory;
    private BlockInteraction? _interaction;
    private HotbarRenderer?   _hotbar;
    private HudRenderer?      _hud;

    private PixelRenderer? _menuRenderer;
    private byte[]? _menuImage;

    // Таймер для сообщения "Сохранено!"
    private float _saveMessageTimer = 0f;

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

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        Input.SetCursorMode(true);

        _menuRenderer = new PixelRenderer();
        _menuRenderer.Initialize(_gl, RenderWidth, RenderHeight, WindowWidth, WindowHeight);
        _menuImage = new byte[RenderWidth * RenderHeight * 4];

        _hud = new HudRenderer();
        _fpsStopwatch.Start();
    }

    private void OnUpdate(double deltaTime)
    {
        if (Input.Jpressed(Key.Escape))
        {
            switch (_state)
            {
                case GameState.Playing:
                    PauseGame();
                    break;
                case GameState.Paused:
                    ResumeGame();
                    break;
                case GameState.MainMenu:
                    _window!.Close();
                    break;
            }
            return;
        }

        if (_state == GameState.Playing)
        {
            _world!.Update((float)deltaTime);
            _inventory!.Update();
            _interaction!.Update();
            
            if (_saveMessageTimer > 0f) _saveMessageTimer -= (float)deltaTime;
        }

        UpdateFpsTitle();
    }

    private void OnRender(double deltaTime)
    {
        _gl!.Clear(ClearBufferMask.ColorBufferBit);

        if (_state == GameState.MainMenu)
        {
            DrawMainMenu();
        }
        else // Playing или Paused
        {
            _world!.Render((img, w, h) =>
            {
                _hotbar!.Draw(img, w, h);
                _hud!.DrawDebug(img, w, h, _player!.BodyPosition, _player.Mode, _currentFps);
                _hud!.DrawCrosshair(img, w, h);

                // Если игра на паузе — рисуем поверх 3D-сцены меню паузы
                if (_state == GameState.Paused)
                {
                    DrawPauseMenu(img, w, h);
                }
            });
        }

        Input.PullEvents(); // В самом конце кадра
    }

    // ---------------------------------------------------------
    //  UI МЕНЮ
    // ---------------------------------------------------------
    private void DrawMainMenu()
    {
        int w = (int)RenderWidth;
        int h = (int)RenderHeight;

        Array.Fill(_menuImage!, (byte)255);
        UiPainter.FillRect(_menuImage!, w, h, 0, 0, w, h, 30, 30, 40, 255);

        int titleX = w / 2 - (9 * 6) / 2;
        HudRenderer.DrawTextShadow(_menuImage!, w, h, titleX, 40, "VOXEL CHAOS");

        int btnW = 200, btnH = 40;
        int btnX = (w - btnW) / 2;

        if (MenuRenderer.DrawButton(_menuImage!, w, h, btnX, 100, btnW, btnH, "NEW WORLD", IsHovered(btnX, 100, btnW, btnH)))
            StartNewWorld();

        if (SaveManager.GetSaves().Contains("QuickSave"))
        {
            if (MenuRenderer.DrawButton(_menuImage!, w, h, btnX, 150, btnW, btnH, "LOAD QUICKSAVE", IsHovered(btnX, 150, btnW, btnH)))
                LoadWorld("QuickSave");
        }

        if (MenuRenderer.DrawButton(_menuImage!, w, h, btnX, 200, btnW, btnH, "EXIT", IsHovered(btnX, 200, btnW, btnH)))
            _window!.Close();

        _menuRenderer!.UploadLowResImage(_menuImage!, RenderWidth, RenderHeight);
        _menuRenderer.Render(WindowWidth, WindowHeight);
    }

    private void DrawPauseMenu(byte[] img, int w, int h)
    {
        // 1. Затемняем экран
        UiPainter.FillRect(img, w, h, 0, 0, w, h, 0, 0, 0, 160);

        // 2. Заголовок
        int titleX = w / 2 - (6 * 6) / 2; // "PAUSED"
        HudRenderer.DrawTextShadow(img, w, h, titleX, 80, "PAUSED");

        // 3. Кнопки
        int btnW = 200, btnH = 40;
        int btnX = (w - btnW) / 2;
        int startY = 140;

        // Resume (Продолжить)
        if (MenuRenderer.DrawButton(img, w, h, btnX, startY, btnW, btnH, "RESUME", IsHovered(btnX, startY, btnW, btnH)))
            ResumeGame();

        // Save (Сохранить)
        if (MenuRenderer.DrawButton(img, w, h, btnX, startY + 50, btnW, btnH, "SAVE", IsHovered(btnX, startY + 50, btnW, btnH)))
        {
            SaveCurrentGame("QuickSave");
            _saveMessageTimer = 2.0f; // Показываем сообщение на 2 секунды
        }

        // Save and Exit
        if (MenuRenderer.DrawButton(img, w, h, btnX, startY + 100, btnW, btnH, "SAVE AND EXIT", IsHovered(btnX, startY + 100, btnW, btnH)))
        {
            SaveCurrentGame("QuickSave");
            ExitToMenu();
        }

        // Exit (Без сохранения)
        if (MenuRenderer.DrawButton(img, w, h, btnX, startY + 150, btnW, btnH, "EXIT TO MENU", IsHovered(btnX, startY + 150, btnW, btnH)))
            ExitToMenu();

        // Сообщение об успешном сохранении
        if (_saveMessageTimer > 0f)
        {
            int msgX = w / 2 - (10 * 6) / 2;
            HudRenderer.DrawTextShadow(img, w, h, msgX, startY + 210, "GAME SAVED!");
        }
    }

    private bool IsHovered(int x, int y, int w, int h)
    {
        float scaleX = (float)RenderWidth / WindowWidth;
        float scaleY = (float)RenderHeight / WindowHeight;
        float mx = Input.MouseX * scaleX;
        float my = Input.MouseY * scaleY;

        return mx >= x && mx <= x + w && my >= y && my <= y + h;
    }

    // ---------------------------------------------------------
    //  УПРАВЛЕНИЕ СОСТОЯНИЯМИ
    // ---------------------------------------------------------
    private void PauseGame()
    {
        _state = GameState.Paused;
        Input.SetCursorMode(true); // Показываем курсор
    }

    private void ResumeGame()
    {
        _state = GameState.Playing;
        Input.SetCursorMode(false); // Прячем курсор
        _saveMessageTimer = 0f;
    }

    private void StartNewWorld()
    {
        _world = new World.World(_gl!, SpawnFeet.X, SpawnFeet.Y + PlayerController.EyeHeight, SpawnFeet.Z,
            WindowWidth, WindowHeight, RenderWidth, RenderHeight, DefaultSeed);
        SetupPlayer(SpawnFeet);
        _state = GameState.Playing;
        Input.SetCursorMode(false);
    }

    private void LoadWorld(string slot)
    {
        var data = SaveManager.Load(slot);
        if (data == null) return;

        // Создаем мир с правильным сидом
        _world = new World.World(_gl!, data.PlayerPos.X, data.PlayerPos.Y + PlayerController.EyeHeight, data.PlayerPos.Z,
            WindowWidth, WindowHeight, RenderWidth, RenderHeight, data.Seed);
    
        _world.ApplySaveData(data);

        // Создаем игрока сразу в сохраненной точке
        SetupPlayer(data.PlayerPos);
    
        // Применяем поворот камеры
        _player!.SetRotation(data.PlayerYaw, data.PlayerPitch); 
     
        _state = GameState.Playing;
        Input.SetCursorMode(false);
    }

    private void SetupPlayer(Vector3 startFeet)
    {
        _player = new PlayerController(startFeet, 60, RenderWidth, RenderHeight, _world);
        _world!.SetCamera(_player);

        _inventory   = new Inventory();
        _interaction = new BlockInteraction(_world, _player, _player, _inventory);
        _hotbar      = new HotbarRenderer(_inventory);
    }

    private void ExitToMenu()
    {
        _state = GameState.MainMenu;
        Input.SetCursorMode(true); 
        _world = null;
        _player = null;
        _inventory = null;
        _interaction = null;
        _hotbar = null;
        _saveMessageTimer = 0f;
        
        GC.Collect(); // Очистка памяти от старого мира
    }

    private void SaveCurrentGame(string slot)
    {
        if (_world == null || _player == null) return;
        var data = _world.GetSaveData(_player);
        SaveManager.Save(slot, data);
    }

    private void UpdateFpsTitle()
    {
        _frameCount++;
        if (_fpsStopwatch.Elapsed.TotalSeconds < 1.0) return;
        _currentFps = _frameCount / _fpsStopwatch.Elapsed.TotalSeconds;
        _frameCount = 0;
        _fpsStopwatch.Restart();
        
        string mode = _state == GameState.Playing && _player != null ? _player.Mode.ToString() : _state.ToString();
        _window!.Title = $"{WindowTitle} | FPS: {_currentFps:F0} | {mode}";
    }

    public void Dispose() => _window?.Dispose();
}