using AVAPacMan.Audio;

namespace AVAPacMan.Engine;

/// <summary>
/// Plays the game's sounds through OpenAL Soft, which mixes as many at once as the game needs. The
/// tunes play on the music channel; the siren, the frightened warble and the eyes' whoosh loop on the
/// ambience channel, one at a time. In a virtual machine a larger mixing buffer keeps the sound from
/// breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;
    private bool _wakaB;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.35f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    private string File(string name) => Path.Combine(_folder, name + ".wav");

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.TitleTune: _engine.PlayMusic(File("TitleTune"), loop: true); return;
            case Sfx.StartTune or Sfx.Intermission or Sfx.GameOver: _engine.PlayMusic(File(sfx.ToString()), loop: false); return;
            case Sfx.Siren1 or Sfx.Siren2 or Sfx.Siren3 or Sfx.Siren4 or Sfx.Fright or Sfx.Eyes:
                _engine.PlayAmbience(File(sfx.ToString()));
                return;
            case Sfx.AmbienceStop: _engine.StopAmbience(); return;
            case Sfx.Waka:
                // The two halves of the chomp alternate, dot by dot.
                _wakaB = !_wakaB;
                _engine.PlayEffect(File(_wakaB ? "WakaB" : "WakaA"), 0.5f);
                return;
        }
        _engine.PlayEffect(File(sfx.ToString()), 1f);
    }

    public void StopAll()
    {
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}
