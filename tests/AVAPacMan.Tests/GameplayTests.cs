using AVAPacMan.Engine;
using AVAPacMan.Models;

namespace AVAPacMan.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pac-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private GameEngine NewGame(int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        return g;
    }

    /// <summary>A game under way with the ghosts shut in the house.</summary>
    private GameEngine Alone()
    {
        var g = NewGame();
        g.Go();
        g.Shut();
        return g;
    }

    private static void Steps(GameEngine g, int n, Dir wanted = Dir.None)
    {
        for (int i = 0; i < n; i++) g.Step(new Controls(wanted));
    }

    private static readonly Dir[] Ways = [Dir.Up, Dir.Left, Dir.Down, Dir.Right];

    /// <summary>Eats every dot but one, then walks the player onto it.</summary>
    internal static void FinishMaze(GameEngine g)
    {
        g.EatAllBut(1);
        var last = Enumerable.Range(0, Maze.Rows).SelectMany(r => Enumerable.Range(0, Maze.Cols).Select(c => new Tile(c, r)))
            .Single(t => g.Dots[t.Col, t.Row] || g.Energizers[t.Col, t.Row]);
        var way = Ways.First(w => Maze.IsOpen(last + w.Reverse()));
        g.PutPlayer(last + way.Reverse(), way);
        for (int i = 0; i < 60 && g.Phase != Phase.LevelClear; i++) g.Step(default);
    }

    [Fact]
    public void The_maze_has_no_dead_ends_one_wide_corridors_and_every_dot_can_be_reached()
    {
        Assert.Equal(Maze.Rows, Maze.Layout.Length);
        Assert.All(Maze.Layout, r => Assert.Equal(Maze.Cols, r.Length));
        var open = new List<Tile>();
        for (int r = 0; r < Maze.Rows; r++)
            for (int c = 0; c < Maze.Cols; c++)
                if (Maze.IsOpen(new Tile(c, r))) open.Add(new Tile(c, r));
        foreach (var t in open)
        {
            Assert.True(Ways.Count(w => Maze.IsOpen(t + w)) >= 2, $"dead end at {t}");
            bool square = Maze.IsOpen(t + Dir.Right) && Maze.IsOpen(t + Dir.Down) && Maze.IsOpen(t + Dir.Right + Dir.Down);
            Assert.False(square, $"open square at {t}");
        }
        var seen = new HashSet<Tile> { Maze.TileOf(Maze.PlayerStart) };
        var queue = new Queue<Tile>(seen);
        while (queue.Count > 0)
            foreach (var w in Ways)
            {
                var n = Maze.Wrap(queue.Peek() + w);
                if (Maze.IsOpen(n) && seen.Add(n)) queue.Enqueue(n);
                if (w == Dir.Right) queue.Dequeue();
            }
        Assert.Equal(open.Count, seen.Count);
        Assert.Equal(4, Maze.Layout.Sum(r => r.Count(c => c == 'o')));
        Assert.True(Maze.IsOpen(Maze.TileOf(Maze.FruitSpot)));
        Assert.False(Maze.IsOpen(new Tile(13, 12)), "the door is shut to the player");
    }

    [Fact]
    public void A_game_starts_ready_with_every_dot_in_place()
    {
        var g = NewGame();
        Assert.Equal(Phase.Ready, g.Phase);
        Assert.Equal(3, g.Lives);
        Assert.Equal(1, g.Level);
        Assert.Equal(Maze.CountDots(), g.DotsLeft);
        Assert.Equal(GhostState.Active, g.Ghosts[Ghost.Red].State);
        Assert.All(g.Ghosts.Skip(1), gh => Assert.Equal(GhostState.InHouse, gh.State));
        double x = g.Player.Pos.X;
        Steps(g, 60);
        Assert.Equal(x, g.Player.Pos.X);
        Steps(g, (int)(60 * GameEngine.FirstReadySeconds));
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.True(g.Player.Pos.X < x);
    }

    [Fact]
    public void The_player_runs_until_a_wall_and_takes_a_turn_asked_for_early()
    {
        var g = Alone();
        // From the start, left along row 23: the first way up is column 12.
        Steps(g, 3, Dir.Up);
        Steps(g, 60 * 2);
        Assert.Equal(12, g.Player.Tile.Col);
        Assert.True(g.Player.Tile.Row < 23);
        Assert.Equal(Dir.Up, g.Player.Dir);
        // Up column 12 to row 20, where the wall stops it if it doesn't turn.
        Steps(g, 60 * 3);
        Assert.True(g.Player.Stopped);
        Assert.Equal(new Tile(12, 20), g.Player.Tile);
        Assert.Equal(Maze.Centre(new Tile(12, 20)), g.Player.Pos);
    }

    [Fact]
    public void The_player_can_turn_round_at_any_moment()
    {
        var g = Alone();
        Steps(g, 10);
        double x = g.Player.Pos.X;
        g.Step(new Controls(Dir.Right));
        Assert.Equal(Dir.Right, g.Player.Dir);
        Steps(g, 5);
        Assert.True(g.Player.Pos.X > x);
    }

    [Fact]
    public void Dots_score_ten_and_energizers_fifty()
    {
        var g = Alone();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        int left = g.DotsLeft;
        Steps(g, 30);
        Assert.True(g.Score > 0 && g.Score % 10 == 0);
        Assert.Equal(left - g.Score / 10, g.DotsLeft);
        Assert.Contains(Sfx.Waka, heard);

        g.PutPlayer(new Tile(1, 22), Dir.Down);
        int before = g.Score;
        Steps(g, 12);
        Assert.False(g.Energizers[1, 23]);
        Assert.Equal(before + 10 + 50, g.Score); // the dot it was put on, then the energizer
    }

    [Fact]
    public void Each_ghost_chases_in_its_own_way()
    {
        var g = Alone();
        g.SetMode(1);
        g.PutPlayer(new Tile(6, 20), Dir.Left);
        foreach (var gh in g.Ghosts) gh.State = GhostState.Active;
        g.PutGhost(Ghost.Red, new Tile(21, 5), Dir.Left);
        g.PutGhost(Ghost.Orange, new Tile(21, 29), Dir.Left);
        Assert.Equal(new Tile(6, 20), g.TargetOf(g.Ghosts[Ghost.Red]));
        Assert.Equal(new Tile(2, 20), g.TargetOf(g.Ghosts[Ghost.Pink]));
        // Two ahead of the player is (4,20); cyan aims as far past that again as red is from it.
        Assert.Equal(new Tile(2 * 4 - 21, 2 * 20 - 5), g.TargetOf(g.Ghosts[Ghost.Cyan]));
        Assert.Equal(new Tile(6, 20), g.TargetOf(g.Ghosts[Ghost.Orange]));
        g.PutGhost(Ghost.Orange, new Tile(6, 23), Dir.Left);
        Assert.Equal(g.Ghosts[Ghost.Orange].Corner, g.TargetOf(g.Ghosts[Ghost.Orange]));
        // Facing up, pink's aim slips four to the left as well, as the arcade's did.
        g.PutPlayer(new Tile(6, 20), Dir.Up);
        Assert.Equal(new Tile(2, 16), g.TargetOf(g.Ghosts[Ghost.Pink]));
        // Scattering, each heads for its own corner.
        g.SetMode(0);
        Assert.All(g.Ghosts, gh => Assert.Equal(gh.Corner, g.TargetOf(gh)));
    }

    [Fact]
    public void Ghosts_never_turn_back_on_their_own_and_reverse_when_the_mode_changes()
    {
        var g = NewGame();
        g.Go();
        var red = g.Ghosts[Ghost.Red];
        Dir last = red.Dir;
        for (int i = 0; i < 60 * 6; i++)
        {
            g.Step(default);
            if (g.Phase != Phase.Playing) break;
            Assert.NotEqual(last.Reverse(), red.Dir);
            last = red.Dir;
        }
        Assert.Equal(0, g.ModeIndex);
        Steps(g, 60 * 2);
        Assert.Equal(1, g.ModeIndex);
        Assert.True(g.Chasing);
        Assert.Equal(new[] { 7.0, 20, 7, 20, 5, 20, 5 }, Levels.Schedule(1));
    }

    [Fact]
    public void Ghosts_leave_the_house_on_a_count_of_dots_or_when_the_player_stops_eating()
    {
        var g = NewGame();
        g.Go();
        Steps(g, 2);
        Assert.Equal(GhostState.Leaving, g.Ghosts[Ghost.Pink].State);
        Assert.Equal(GhostState.InHouse, g.Ghosts[Ghost.Cyan].State);
        Assert.Equal(30, Levels.DotLimit(Ghost.Cyan, 1));
        Assert.Equal(60, Levels.DotLimit(Ghost.Orange, 1));
        Assert.Equal(0, Levels.DotLimit(Ghost.Orange, 3));
        // Stand still against a wall: after four seconds without a dot, cyan is let out anyway.
        g.PutPlayer(new Tile(12, 20), Dir.Up);
        Steps(g, 60 * 4 + 10);
        Assert.NotEqual(GhostState.InHouse, g.Ghosts[Ghost.Cyan].State);
        Assert.Equal(GhostState.InHouse, g.Ghosts[Ghost.Orange].State);
        Steps(g, 60 * 5);
        Assert.Equal(GhostState.Active, g.Ghosts[Ghost.Pink].State);
    }

    [Fact]
    public void An_energizer_turns_the_ghosts_blue_and_each_one_eaten_is_worth_double()
    {
        var g = Alone();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        foreach (var gh in g.Ghosts) gh.State = GhostState.Active;
        g.PutPlayer(new Tile(1, 5), Dir.Up);
        g.PutGhost(Ghost.Red, new Tile(1, 8), Dir.Up);
        g.PutGhost(Ghost.Pink, new Tile(6, 29), Dir.Left);
        g.PutGhost(Ghost.Cyan, new Tile(21, 29), Dir.Left);
        g.PutGhost(Ghost.Orange, new Tile(26, 26), Dir.Left);
        Steps(g, 20);
        Assert.False(g.Energizers[1, 3]);
        Assert.True(g.FrightLeft > 5);
        Assert.All(g.Ghosts, gh => Assert.True(gh.Frightened));
        Assert.Equal(Dir.Down, g.Ghosts[Ghost.Red].Dir);
        // Chase it down.
        var pilot = new AttractPilot(1);
        for (int i = 0; i < 60 * 5 && g.Phase != Phase.GhostEaten; i++) g.Step(pilot.Decide(g));
        Assert.Equal(Phase.GhostEaten, g.Phase);
        Assert.Contains(g.Effects, e => e.Text == "200");
        Assert.Contains(Sfx.EatGhost, heard);
        var eaten = g.Ghosts.Single(x => x.State == GhostState.Eaten);
        Assert.False(eaten.Frightened);
        Assert.True(g.JustEaten(eaten));
        Steps(g, 70);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Equal(Sfx.Eyes, heard.Last(s => s != Sfx.Waka));

        // The eyes go home, and come back out as a ghost again.
        g.PutPlayer(new Tile(26, 29), Dir.Left); // out of the way
        for (int i = 0; i < 60 * 15 && eaten.State != GhostState.Leaving; i++) g.Step(default);
        Assert.Equal(GhostState.Leaving, eaten.State);
        Assert.False(eaten.Frightened);
    }

    [Fact]
    public void Running_into_a_ghost_costs_a_life_and_everyone_starts_again()
    {
        var g = Alone();
        g.PutGhost(Ghost.Red, new Tile(9, 23), Dir.Right);
        Steps(g, 60);
        Assert.Equal(Phase.Dying, g.Phase);
        Steps(g, (int)(60 * GameEngine.DyingSeconds) + 2);
        Assert.Equal(2, g.Lives);
        Assert.Equal(Phase.Ready, g.Phase);
        Assert.Equal(Maze.PlayerStart, g.Player.Pos);
        Assert.True(g.DotsLeft < g.TotalDots, "the dots eaten stay eaten");
    }

    [Fact]
    public void The_tunnel_wraps_round_and_slows_the_ghosts()
    {
        var g = Alone();
        g.PutPlayer(new Tile(2, Maze.TunnelRow), Dir.Left);
        Steps(g, 60);
        Assert.True(g.Player.Tile.Col > 20, $"came out at {g.Player.Tile}");
        Assert.Equal(Maze.TunnelRow, g.Player.Tile.Row);
        Assert.True(Levels.TunnelSpeed(1) < Levels.GhostSpeed(1));
    }

    [Fact]
    public void Fruit_appears_after_seventy_dots_and_is_worth_more_on_later_levels()
    {
        var g = Alone();
        g.EatAllBut(g.DotsLeft - 69);
        Assert.Null(g.FruitShowing);
        Steps(g, 20);
        Assert.Equal(Fruit.Cherry, g.FruitShowing);
        g.PutPlayer(new Tile(10, 17), Dir.Right);
        int before = g.Score;
        Steps(g, 40);
        Assert.Null(g.FruitShowing);
        Assert.Equal(before + 100, g.Score);
        Assert.Equal(5000, Levels.FruitPoints(Levels.FruitFor(13)));
        Assert.Equal(Fruit.Melon, Levels.FruitFor(7));
    }

    [Fact]
    public void Clearing_the_maze_flashes_it_and_after_level_two_comes_a_break()
    {
        var g = Alone();
        FinishMaze(g);
        Assert.Equal(Phase.LevelClear, g.Phase);
        Steps(g, (int)(60 * GameEngine.LevelClearSeconds) + 2);
        Assert.Equal(2, g.Level);
        Assert.Equal(g.TotalDots, g.DotsLeft);
        Assert.Equal(Phase.Ready, g.Phase);

        g.Go();
        g.Shut();
        FinishMaze(g);
        Steps(g, (int)(60 * GameEngine.LevelClearSeconds) + 2);
        Assert.Equal(Phase.Intermission, g.Phase);
        Steps(g, (int)(60 * GameEngine.IntermissionSeconds) + 2);
        Assert.Equal(3, g.Level);
        Assert.True(Levels.IntermissionAfter(5) && Levels.IntermissionAfter(9) && Levels.IntermissionAfter(13) && !Levels.IntermissionAfter(3));
    }

    [Fact]
    public void Later_levels_are_faster_and_the_fright_shorter_until_it_only_turns_them_round()
    {
        Assert.True(Levels.PlayerSpeed(5, false) > Levels.PlayerSpeed(1, false));
        Assert.True(Levels.GhostSpeed(5) > Levels.GhostSpeed(1));
        Assert.Equal(6, Levels.FrightTime(1));
        Assert.Equal(1, Levels.FrightTime(9));
        Assert.Equal(0, Levels.FrightTime(19));
        var g = Alone();
        g.JumpToLevel(19);
        g.Shut();
        g.Ghosts[Ghost.Red].State = GhostState.Active;
        g.PutPlayer(new Tile(1, 22), Dir.Down);
        Steps(g, 8);
        Assert.False(g.Energizers[1, 23]);
        Assert.Equal(0, g.FrightLeft);
        Assert.False(g.Ghosts[Ghost.Red].Frightened);
    }

    [Fact]
    public void The_red_ghost_speeds_up_as_the_dots_run_out()
    {
        var g = Alone();
        Assert.Equal(0, g.Elroy);
        g.EatAllBut(20);
        Assert.Equal(1, g.Elroy);
        g.EatAllBut(10);
        Assert.Equal(2, g.Elroy);
        // And then it chases even when the others scatter.
        g.Ghosts[Ghost.Red].State = GhostState.Active;
        Assert.Equal(g.Player.Tile, g.TargetOf(g.Ghosts[Ghost.Red]));
    }

    [Fact]
    public void A_bonus_life_at_ten_thousand()
    {
        var g = Alone();
        g.AddScore(9990);
        Assert.Equal(3, g.Lives);
        g.AddScore(10);
        Assert.Equal(4, g.Lives);
        g.AddScore(20000);
        Assert.Equal(4, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_clears_the_first_maze_and_the_game_ends_in_the_table()
    {
        var g = NewGame(seed: 3);
        var pilot = new AttractPilot(3);
        for (int i = 0; i < 60 * 60 * 4 && g.State == GameState.Playing && g.Level < 2; i++) g.Step(pilot.Decide(g));
        Assert.True(g.Level >= 2, $"level {g.Level}, {g.DotsLeft} dots left; lost: {string.Join("; ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = NewGame(seed: 21);
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Level} {g.Score} {g.Lives} {g.DotsLeft} {g.Player.Pos} {string.Join(",", g.Ghosts.Select(x => x.Pos))}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 8)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Siren1, heard);
        Assert.Contains(Sfx.Waka, heard);
        g.Idle((int)(60 * GameEngine.AttractSeconds));
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.AmbienceStop, heard.Last(s => s is Sfx.Siren1 or Sfx.Siren2 or Sfx.Siren3 or Sfx.Siren4 or Sfx.Fright or Sfx.Eyes or Sfx.AmbienceStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_stops_the_siren()
    {
        var g = Alone();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        Steps(g, 10);
        Assert.Equal(Sfx.Siren1, heard.Last(s => s != Sfx.Waka));
        g.Pause();
        Assert.Equal(Sfx.AmbienceStop, heard.Last());
        var pos = g.Player.Pos;
        Steps(g, 30);
        Assert.Equal(pos, g.Player.Pos);
        g.Resume();
        Steps(g, 2);
        Assert.Equal(Sfx.Siren1, heard.Last(s => s != Sfx.Waka));
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var files = Enum.GetValues<Sfx>().Where(s => s is not (Sfx.AmbienceStop or Sfx.Waka)).Select(s => s.ToString()).Concat(["WakaA", "WakaB"]);
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        Assert.All(Sprites.GhostBody, s => Assert.Equal((14, 14), (s.Width, s.Height)));
        Assert.Equal((14, 14), (Sprites.FrightFaceArt.Width, Sprites.FrightFaceArt.Height));
        Assert.All(Enum.GetValues<Fruit>(), f => Assert.Equal(12, Sprites.FruitArt(f).Width));
        Assert.Equal(4, Sprites.GhostColours.Length);
    }
}
