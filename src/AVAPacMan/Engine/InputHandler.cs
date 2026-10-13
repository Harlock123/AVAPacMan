using Avalonia.Input;
using AVAPacMan.Models;

namespace AVAPacMan.Engine;

/// <summary>Keyboard and gamepad boiled down to the controls: the way the player wants to go.</summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key)
    {
        _held.Remove(key);
        _held.Add(key);
    }

    public void OnKeyUp(Key key) => _held.Remove(key);
    public bool Has(Key key) => _held.Contains(key);

    public void Update() => _pad.Update();

    private static Dir DirOf(Key k) => k switch
    {
        Key.Up or Key.W => Dir.Up,
        Key.Down or Key.S => Dir.Down,
        Key.Left or Key.A => Dir.Left,
        Key.Right or Key.D => Dir.Right,
        _ => Dir.None,
    };

    public Controls Controls
    {
        get
        {
            // The most recently pressed direction key still held wins.
            for (int i = _held.Count - 1; i >= 0; i--)
                if (DirOf(_held[i]) is var d && d != Dir.None) return new Controls(d);
            if (_pad.DPadUp || _pad.LeftStickY < -0.5f) return new Controls(Dir.Up);
            if (_pad.DPadDown || _pad.LeftStickY > 0.5f) return new Controls(Dir.Down);
            if (_pad.DPadLeft || _pad.LeftStickX < -0.5f) return new Controls(Dir.Left);
            if (_pad.DPadRight || _pad.LeftStickX > 0.5f) return new Controls(Dir.Right);
            return default;
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.Space) || Has(Key.D1) || _pad.ButtonStart || _pad.ButtonA;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
