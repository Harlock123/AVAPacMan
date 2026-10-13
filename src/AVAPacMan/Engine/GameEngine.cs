using AVAPacMan.Models;

namespace AVAPacMan.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

/// <summary>What is happening within a game.</summary>
public enum Phase
{
    /// <summary>"READY!": everyone in place, a moment before the off.</summary>
    Ready,
    Playing,
    /// <summary>A ghost was just eaten: everything stops for a moment while its points show.</summary>
    GhostEaten,
    /// <summary>Caught: a pause, then the player shrivels away.</summary>
    Dying,
    /// <summary>Every dot eaten: the maze flashes.</summary>
    LevelClear,
    /// <summary>A short scene between levels.</summary>
    Intermission,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Waka, EatFruit, EatGhost, Death, ExtraLife, StartTune, Intermission, GameOver, TitleTune,
    Siren1, Siren2, Siren3, Siren4, Fright, Eyes, AmbienceStop,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it
/// runs in fixed 60 Hz steps, so it plays the same at any frame rate and a seed always plays the
/// same game.
/// </summary>
public sealed partial class GameEngine
{
    public const int StartingLives = 3;
    public const int ExtraLifeAt = 10000;
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 45;
    public const double HighScoreSeconds = 9;

    private readonly Random _rng;
    private double _accumulator;
    private bool _startPrev;
    private bool _titleTunePending = true;
    private bool _extraLifeGiven;

    public GameEngine(int seed = 1980, HighScoreTable? table = null)
    {
        _rng = new Random(seed);
        HighScores = table ?? HighScoreTable.Load();
        Dots = new bool[Maze.Cols, Maze.Rows];
        Energizers = new bool[Maze.Cols, Maze.Rows];
        Ghosts = Enumerable.Range(0, 4).Select(i => new Ghost { Id = i }).ToArray();
        FillMaze();
        PlaceEveryone();
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Ready;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public AttractPilot? Pilot { get; private set; }
    public bool Silent { get; set; }

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Level { get; private set; } = 1;
    /// <summary>The fruit of each level played so far, for the panel.</summary>
    public List<Fruit> FruitHistory { get; } = [];

    public List<Effect> Effects { get; } = [];

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    public void Update(double dt, Controls controls, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Maze.Step)
        {
            _accumulator -= Maze.Step;
            Step(controls, start);
        }
    }

    /// <summary>One 1/60 s step.</summary>
    public void Step(Controls c, bool start = false)
    {
        double dt = Maze.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        foreach (var e in Effects) e.Age += dt;
        Effects.RemoveAll(e => e.Done);

        switch (State)
        {
            case GameState.Title:
                if (_titleTunePending) { _titleTunePending = false; Play(Sfx.TitleTune); }
                if (startPressed) StartGame();
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilot!.Decide(this));
                if (startPressed) { StartGame(); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(dt, c);
                break;
            case GameState.GameOver:
                if (StateTime > 3)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, Level);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    public void StartGame()
    {
        Pilot = null;
        Begin(GameState.Playing);
    }

    private void StartAttract()
    {
        Pilot = new AttractPilot(_rng.Next());
        Begin(GameState.Attract);
    }

    private void Begin(GameState state)
    {
        State = state;
        StateTime = 0;
        Score = 0;
        Lives = StartingLives;
        _extraLifeGiven = false;
        NewEntryIndex = -1;
        Effects.Clear();
        FruitHistory.Clear();
        StartLevel(1, firstOfGame: true);
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        Pilot = null;
        _titleTunePending = true;
        Ambience(Sfx.AmbienceStop);
        Effects.Clear();
        Level = 1;
        FillMaze();
        PlaceEveryone();
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilot = null;
        Ambience(Sfx.AmbienceStop);
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Ambience(Sfx.AmbienceStop);
    }

    public void Resume()
    {
        if (State == GameState.Paused) State = GameState.Playing;
    }

    private void GameOver()
    {
        Ambience(Sfx.AmbienceStop);
        if (State == GameState.Attract)
        {
            ShowHighScores(-1);
            return;
        }
        Play(Sfx.GameOver);
        State = GameState.GameOver;
        StateTime = 0;
    }

    internal void AddScore(int points)
    {
        Score += points;
        if (!_extraLifeGiven && Score >= ExtraLifeAt)
        {
            _extraLifeGiven = true;
            Lives++;
            Play(Sfx.ExtraLife);
        }
    }

    private Sfx _ambience = Sfx.AmbienceStop;

    /// <summary>The background loop: the siren, the frightened warble or the eyes' whoosh.</summary>
    private void Ambience(Sfx loop)
    {
        if (loop == _ambience) return;
        _ambience = loop;
        Play(loop);
    }

    private void Popup(Vec at, int points, double life = 1) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = life, Text = points.ToString() });

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }
}
