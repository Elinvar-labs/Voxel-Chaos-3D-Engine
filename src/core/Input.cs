using Silk.NET.Input;
using Silk.NET.Windowing;

namespace VoxelChaos.Core;

public static class Input
{
    private static IInputContext _inputContext;

    private static readonly bool[] _keys = new bool[1032];
    private static readonly int[] _frames = new int[1032];

    private static float _x;
    private static float _y;
    private static bool _cursorStarted = true;
    private static int _current;

    private static readonly int _MOUSE_BUTTONS_OFFSET = 512;

    public static float DeltaX { get; private set; }
    public static float DeltaY { get; private set; }

    public static void Initialize(IWindow window)
    {
        _inputContext = window.CreateInput();

        Console.WriteLine("INFO::INIT_INPUT");

        if (_inputContext.Keyboards.Count > 0)
        {
            var keyboard = _inputContext.Keyboards[0];

            if (_inputContext.Mice.Count > 0) _inputContext.Mice[0].Cursor.CursorMode = CursorMode.Disabled;

            keyboard.KeyDown += (kbd, key, scancode) =>
            {
                var keyCode = (int)key;
                if (keyCode >= 0 && keyCode < _MOUSE_BUTTONS_OFFSET)
                {
                    _keys[keyCode] = true;
                    _frames[keyCode] = _current;
                }
                else
                {
                    Console.WriteLine("INPUT: Bad key: " + key);
                }
            };

            keyboard.KeyUp += (kbd, key, scancode) =>
            {
                var keyCode = (int)key;
                if (keyCode >= 0 && keyCode < _MOUSE_BUTTONS_OFFSET)
                {
                    _keys[keyCode] = false;
                    _frames[keyCode] = _current;
                }
                else
                {
                    Console.WriteLine("INPUT: Bad key: " + key);
                }
            };
        }

        if (_inputContext.Mice.Count > 0)
        {
            var mouse = _inputContext.Mice[0];

            mouse.MouseDown += (m, button) =>
            {
                var index = _MOUSE_BUTTONS_OFFSET + (int)button;
                if (index < _keys.Length)
                {
                    _keys[index] = true;
                    _frames[index] = _current;
                }
            };

            mouse.MouseUp += (m, button) =>
            {
                var index = _MOUSE_BUTTONS_OFFSET + (int)button;
                if (index < _keys.Length)
                {
                    _keys[index] = false;
                    _frames[index] = _current;
                }
            };

            mouse.MouseMove += (m, position) =>
            {
                var xp = position.X;
                var yp = position.Y;

                if (_cursorStarted)
                {
                    _x = xp;
                    _y = yp;
                    _cursorStarted = false;
                }
                else
                {
                    DeltaX += xp - _x;
                    DeltaY += _y - yp;
                    _x = xp;
                    _y = yp;
                }
            };
        }
    }

    public static bool Pressed(Key key)
    {
        var keycode = (int)key;
        if (keycode < 0 || keycode >= _MOUSE_BUTTONS_OFFSET) return false;
        return _keys[keycode];
    }

    public static bool Jpressed(Key key)
    {
        var keycode = (int)key;
        if (keycode < 0 || keycode >= _MOUSE_BUTTONS_OFFSET) return false;
        return _keys[keycode] && _frames[keycode] == _current;
    }

    public static bool Clicked(MouseButton button)
    {
        var index = _MOUSE_BUTTONS_OFFSET + (int)button;
        if (index >= _keys.Length) return false;
        return _keys[index];
    }

    public static bool Jclicked(MouseButton button)
    {
        var index = _MOUSE_BUTTONS_OFFSET + (int)button;
        if (index >= _keys.Length) return false;
        return _keys[index] && _frames[index] == _current;
    }

    public static void PullEvents()
    {
        _current++;
        DeltaX = 0F;
        DeltaY = 0F;
    }
}