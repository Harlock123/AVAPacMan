using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAPacMan.Engine;
using AVAPacMan.Models;
using AVAPacMan.Views;
using Controls = AVAPacMan.Engine.Controls;

namespace AVAPacMan.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set PACMAN_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pac-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("PACMAN_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAPacMan-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_the_first_maze()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 5);
        Shot(w, c, "title");
        g.StartGame();
        g.Idle(60);
        Shot(w, c, "ready");
        g.Go();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 25 && g.Phase != Phase.Dying; i++)
        {
            g.Step(pilot.Decide(g));
            if (i > 60 * 14 && g.Phase == Phase.Playing && g.FruitShowing is not null && g.Ghosts.All(x => x.State == GhostState.Active)) break;
        }
        Shot(w, c, "play");
        w.Close();
    }

    [AvaloniaFact]
    public void Frightened_ghosts_and_one_eaten()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.Go();
        var pilot = new AttractPilot(4);
        for (int i = 0; i < 60 * 3; i++) g.Step(pilot.Decide(g));
        foreach (var x in g.Ghosts) x.State = GhostState.Active;
        g.PutPlayer(new Tile(1, 7), Dir.Up);
        g.PutGhost(Ghost.Red, new Tile(6, 5), Dir.Left);
        g.PutGhost(Ghost.Pink, new Tile(4, 8), Dir.Left);
        g.PutGhost(Ghost.Cyan, new Tile(9, 1), Dir.Left);
        g.PutGhost(Ghost.Orange, new Tile(12, 6), Dir.Up);
        for (int i = 0; i < 40; i++) g.Step(new Controls(Dir.Up));
        Assert.True(g.FrightLeft > 4, $"fright {g.FrightLeft}");
        g.Step(new Controls(Dir.Right));
        Shot(w, c, "frightened");
        for (int i = 0; i < 60 * 4 && g.Phase != Phase.GhostEaten; i++) g.Step(pilot.Decide(g));
        Assert.Equal(Phase.GhostEaten, g.Phase);
        g.Idle(6);
        Shot(w, c, "ghost-eaten");
        w.Close();
    }

    [AvaloniaFact]
    public void The_break_after_level_two()
    {
        var (w, c, g) = Open(seed: 6);
        g.StartGame();
        g.JumpToLevel(2);
        g.Shut();
        GameplayTests.FinishMaze(g);
        for (int i = 0; i < 60 * 30 && g.Phase != Phase.Intermission; i++) g.Step(default);
        Assert.Equal(Phase.Intermission, g.Phase);
        g.Idle(60 * 5);
        Shot(w, c, "intermission");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_initials_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 * 5 && g.Score < 6000 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('A');
        g.Initials.Type('V');
        Shot(w, c, "initials");
        g.Initials.Type('A');
        g.Idle(5);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
