using AVAPacMan.Models;

namespace AVAPacMan.Engine;

public sealed partial class GameEngine
{
    public const double ReadySeconds = 2;
    public const double FirstReadySeconds = 4.2;
    public const double GhostEatenSeconds = 1;
    public const double DyingSeconds = 3;
    public const double LevelClearSeconds = 3;
    public const double IntermissionSeconds = 7;
    public const double FruitSeconds = 9.5;
    public const double EyesSpeed = 150;
    public const double HouseSpeed = 38;

    public Player Player { get; } = new();
    public Ghost[] Ghosts { get; }
    public bool[,] Dots { get; }
    public bool[,] Energizers { get; }
    public int DotsLeft { get; private set; }
    public int DotsEaten { get; private set; }
    public int TotalDots { get; private set; }

    /// <summary>The fruit below the house, while it's there.</summary>
    public Fruit? FruitShowing { get; private set; }
    private double _fruitTime;

    /// <summary>Seconds of fright left (0: the ghosts are their usual selves).</summary>
    public double FrightLeft { get; private set; }
    private int _ghostsEatenThisFright;

    /// <summary>Scatter (even) or chase (odd), counting through the level's timetable.</summary>
    public int ModeIndex { get; private set; }
    private double _modeTime;
    public bool Chasing => ModeIndex % 2 == 1;

    private int _pause;
    private double _idleTime;
    private bool _globalCounting;
    private int _globalDots;

    // ----- levels and lives ---------------------------------------------------------------------

    private void StartLevel(int level, bool firstOfGame = false)
    {
        Level = level;
        FruitHistory.Add(Levels.FruitFor(level));
        if (FruitHistory.Count > 7) FruitHistory.RemoveAt(0);
        FillMaze();
        foreach (var g in Ghosts) g.DotCount = 0;
        _globalCounting = false;
        PlaceEveryone();
        SetPhase(Phase.Ready);
        _readyFor = firstOfGame ? FirstReadySeconds : ReadySeconds;
        if (firstOfGame) Play(Sfx.StartTune);
    }

    private double _readyFor = ReadySeconds;

    private void FillMaze()
    {
        Array.Clear(Dots);
        Array.Clear(Energizers);
        DotsLeft = 0;
        for (int r = 0; r < Maze.Rows; r++)
            for (int c = 0; c < Maze.Cols; c++)
            {
                char ch = Maze.Layout[r][c];
                if (ch == '.') Dots[c, r] = true;
                else if (ch == 'o') Energizers[c, r] = true;
                else continue;
                DotsLeft++;
            }
        TotalDots = DotsLeft;
        DotsEaten = 0;
        FruitShowing = null;
    }

    /// <summary>Everyone back to their starting places, the mode timetable from the top.</summary>
    private void PlaceEveryone()
    {
        Player.Pos = Maze.PlayerStart;
        Player.Dir = Dir.Left;
        Player.Wanted = Dir.None;
        Player.Stopped = false;
        Player.Travelled = 0;
        Vec[] starts = [Maze.DoorOutside, Maze.HouseCentre, Maze.HouseLeft, Maze.HouseRight];
        foreach (var g in Ghosts)
        {
            g.Pos = starts[g.Id];
            g.State = g.Id == Ghost.Red ? GhostState.Active : GhostState.InHouse;
            g.Dir = g.Id switch { Ghost.Red => Dir.Left, Ghost.Pink => Dir.Down, _ => Dir.Up };
            g.Frightened = false;
            g.ReversePending = false;
            g.Bob = g.Id == Ghost.Pink ? 1 : -1;
        }
        FrightLeft = 0;
        ModeIndex = 0;
        _modeTime = 0;
        _pause = 0;
        _idleTime = 0;
        _fruitTime = 0;
        FruitShowing = null;
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        if (c.Wanted != Dir.None) Player.Wanted = c.Wanted;
        switch (Phase)
        {
            case Phase.Ready:
                Ambience(Sfx.AmbienceStop);
                if (PhaseTime >= _readyFor) SetPhase(Phase.Playing);
                return;
            case Phase.GhostEaten:
                // Only the eyes already heading home keep going.
                foreach (var g in Ghosts)
                    if (g.State is GhostState.Eaten or GhostState.Entering && !_justEaten.Contains(g)) MoveGhost(g, dt);
                if (PhaseTime >= GhostEatenSeconds)
                {
                    _justEaten.Clear();
                    SetPhase(Phase.Playing);
                }
                return;
            case Phase.Dying:
                if (PhaseTime >= 1 && PhaseTime - dt < 1) Play(Sfx.Death);
                Ambience(Sfx.AmbienceStop);
                if (PhaseTime >= DyingSeconds) LoseLife();
                return;
            case Phase.LevelClear:
                Ambience(Sfx.AmbienceStop);
                if (PhaseTime < LevelClearSeconds) return;
                if (Levels.IntermissionAfter(Level))
                {
                    SetPhase(Phase.Intermission);
                    Play(Sfx.Intermission);
                }
                else StartLevel(Level + 1);
                return;
            case Phase.Intermission:
                if (PhaseTime >= IntermissionSeconds) StartLevel(Level + 1);
                return;
        }

        UpdateModes(dt);
        MovePlayer(dt);
        if (Phase != Phase.Playing) return;
        Collide();
        if (Phase != Phase.Playing) return;
        foreach (var g in Ghosts) MoveGhost(g, dt);
        Collide();
        if (Phase != Phase.Playing) return;
        Release(dt);
        UpdateFruit(dt);
        Ambience(Ghosts.Any(g => g.State is GhostState.Eaten or GhostState.Entering) ? Sfx.Eyes
            : FrightLeft > 0 ? Sfx.Fright
            : (DotsEaten * 4 / Math.Max(1, TotalDots)) switch { 0 => Sfx.Siren1, 1 => Sfx.Siren2, 2 => Sfx.Siren3, _ => Sfx.Siren4 });
    }

    private void LoseLife()
    {
        Lives--;
        if (Lives <= 0)
        {
            GameOver();
            return;
        }
        PlaceEveryone();
        // After a life is lost the ghosts come out on a shared count of dots.
        _globalCounting = true;
        _globalDots = 0;
        SetPhase(Phase.Ready);
        _readyFor = ReadySeconds;
    }

    // ----- scatter and chase, and fright --------------------------------------------------------

    private void UpdateModes(double dt)
    {
        if (FrightLeft > 0)
        {
            FrightLeft -= dt;
            if (FrightLeft <= 0)
            {
                FrightLeft = 0;
                foreach (var g in Ghosts) g.Frightened = false;
            }
            return; // the timetable waits while the ghosts are frightened
        }
        var schedule = Levels.Schedule(Level);
        if (ModeIndex >= schedule.Length) return;
        _modeTime += dt;
        if (_modeTime < schedule[ModeIndex]) return;
        _modeTime = 0;
        ModeIndex++;
        foreach (var g in Ghosts)
            if (g.State is GhostState.Active) g.ReversePending = true;
    }

    private void Frighten()
    {
        double time = Levels.FrightTime(Level);
        foreach (var g in Ghosts)
        {
            if (g.State is GhostState.Eaten or GhostState.Entering) continue;
            if (g.State == GhostState.Active) g.ReversePending = true;
            if (time > 0) g.Frightened = true;
        }
        if (time <= 0) return;
        FrightLeft = time;
        _ghostsEatenThisFright = 0;
    }

    // ----- the player ---------------------------------------------------------------------------

    private void MovePlayer(double dt)
    {
        var p = Player;
        if (_pause > 0) { _pause--; return; }
        if (p.Wanted == p.Dir.Reverse() && p.Wanted != Dir.None)
        {
            p.Dir = p.Wanted;
            p.Stopped = false;
        }
        if (p.Stopped)
        {
            if (p.Wanted != Dir.None && Maze.IsOpen(p.Tile + p.Wanted)) { p.Dir = p.Wanted; p.Stopped = false; }
            else if (Maze.IsOpen(p.Tile + p.Dir)) p.Stopped = false;
            else return;
        }
        double dist = Levels.PlayerSpeed(Level, FrightLeft > 0) * Levels.TopSpeed * dt;
        double moved = Advance(p, dist, AtCentre);
        p.Travelled += moved;
        Eat();

        bool AtCentre(Mover m)
        {
            if (p.Wanted != Dir.None && Maze.IsOpen(p.Tile + p.Wanted)) p.Dir = p.Wanted;
            if (Maze.IsOpen(p.Tile + p.Dir)) return true;
            p.Stopped = true;
            return false;
        }
    }

    private void Eat()
    {
        var t = Player.Tile;
        if (!Maze.IsOpen(t)) return;
        if (Dots[t.Col, t.Row])
        {
            Dots[t.Col, t.Row] = false;
            AddScore(10);
            _pause = 1;
            AteDot();
        }
        else if (Energizers[t.Col, t.Row])
        {
            Energizers[t.Col, t.Row] = false;
            AddScore(50);
            _pause = 3;
            AteDot();
            Frighten();
        }
        if (FruitShowing is { } fruit && Math.Abs(Player.Pos.X - Maze.FruitSpot.X) < 6 && Math.Abs(Player.Pos.Y - Maze.FruitSpot.Y) < 4)
        {
            int points = Levels.FruitPoints(fruit);
            AddScore(points);
            Popup(Maze.FruitSpot, points, 2);
            FruitShowing = null;
            Play(Sfx.EatFruit);
        }
    }

    private void AteDot()
    {
        DotsLeft--;
        DotsEaten++;
        _idleTime = 0;
        Play(Sfx.Waka);
        if (_globalCounting) _globalDots++;
        else if (Ghosts.FirstOrDefault(g => g.State == GhostState.InHouse) is { } waiting) waiting.DotCount++;
        if (DotsEaten is 70 or 170)
        {
            FruitShowing = Levels.FruitFor(Level);
            _fruitTime = FruitSeconds + _rng.NextDouble();
        }
        if (DotsLeft == 0)
        {
            SetPhase(Phase.LevelClear);
            FrightLeft = 0;
            foreach (var g in Ghosts) g.Frightened = false;
        }
    }

    private void UpdateFruit(double dt)
    {
        if (FruitShowing is null) return;
        _fruitTime -= dt;
        if (_fruitTime <= 0) FruitShowing = null;
    }

    // ----- moving through the maze --------------------------------------------------------------

    /// <summary>
    /// Moves along the current direction, stopping at each tile centre it reaches to ask
    /// <paramref name="atCentre"/> which way next (false: stop there). Returns the distance moved.
    /// </summary>
    private static double Advance(Mover m, double dist, Func<Mover, bool> atCentre)
    {
        double moved = 0;
        for (int guard = 0; dist > 1e-9 && m.Dir != Dir.None && guard < 6; guard++)
        {
            var here = Maze.Centre(Maze.TileOf(m.Pos));
            var d = new Vec(m.Dir.Dx(), m.Dir.Dy());
            double ahead = (here.X - m.Pos.X) * d.X + (here.Y - m.Pos.Y) * d.Y;
            var target = ahead > 1e-9 ? here : here + d * Maze.TileSize;
            if (ahead <= 1e-9) ahead += Maze.TileSize;
            double s = Math.Min(dist, ahead);
            dist -= s;
            moved += s;
            if (s < ahead - 1e-9)
            {
                m.Pos += d * s;
                break;
            }
            m.Pos = target;
            // Out of one end of the tunnel and in at the other.
            if (m.Pos.X < -Maze.TileSize) m.Pos = m.Pos with { X = m.Pos.X + Maze.Width };
            else if (m.Pos.X > Maze.Width + Maze.TileSize) m.Pos = m.Pos with { X = m.Pos.X - Maze.Width };
            if (!atCentre(m)) break;
        }
        return moved;
    }

    // ----- the ghosts ---------------------------------------------------------------------------

    private readonly HashSet<Ghost> _justEaten = [];

    /// <summary>The ghost just eaten, while its points show in its place.</summary>
    public bool JustEaten(Ghost g) => Phase == Phase.GhostEaten && _justEaten.Contains(g);

    private double GhostSpeed(Ghost g)
    {
        if (g.State is GhostState.Eaten or GhostState.Entering) return EyesSpeed;
        if (g.State is GhostState.InHouse or GhostState.Leaving) return HouseSpeed;
        if (Maze.InTunnel(g.Tile)) return Levels.TunnelSpeed(Level) * Levels.TopSpeed;
        if (g.Frightened) return Levels.FrightGhostSpeed(Level) * Levels.TopSpeed;
        double speed = Levels.GhostSpeed(Level);
        if (g.Id == Ghost.Red) speed += Elroy * 0.05;
        return speed * Levels.TopSpeed;
    }

    /// <summary>How hard the red ghost has sped up as the dots run out: 0, 1 or 2.</summary>
    public int Elroy => DotsLeft <= Levels.ElroyDots(Level) / 2 ? 2 : DotsLeft <= Levels.ElroyDots(Level) ? 1 : 0;

    private void MoveGhost(Ghost g, double dt)
    {
        double dist = GhostSpeed(g) * dt;
        switch (g.State)
        {
            case GhostState.InHouse:
                double y = g.Pos.Y + g.Bob * dist;
                if (y > Maze.HouseCentre.Y + 4 || y < Maze.HouseCentre.Y - 4) g.Bob = -g.Bob;
                g.Pos = g.Pos with { Y = Math.Clamp(y, Maze.HouseCentre.Y - 4, Maze.HouseCentre.Y + 4) };
                g.Dir = g.Bob > 0 ? Dir.Down : Dir.Up;
                return;
            case GhostState.Leaving:
                if (Math.Abs(g.Pos.X - Maze.DoorOutside.X) > 0.01)
                {
                    double dx = Math.Clamp(Maze.DoorOutside.X - g.Pos.X, -dist, dist);
                    g.Pos = g.Pos with { X = g.Pos.X + dx };
                    g.Dir = dx < 0 ? Dir.Left : Dir.Right;
                    return;
                }
                g.Dir = Dir.Up;
                g.Pos = g.Pos with { Y = Math.Max(Maze.DoorOutside.Y, g.Pos.Y - dist) };
                if (g.Pos.Y <= Maze.DoorOutside.Y)
                {
                    g.State = GhostState.Active;
                    g.Dir = Dir.Left;
                    g.ReversePending = false;
                }
                return;
            case GhostState.Entering:
                if (Math.Abs(g.Pos.X - Maze.DoorOutside.X) > 0.01)
                {
                    g.Pos = g.Pos with { X = g.Pos.X + Math.Clamp(Maze.DoorOutside.X - g.Pos.X, -dist, dist) };
                    return;
                }
                g.Dir = Dir.Down;
                g.Pos = g.Pos with { Y = Math.Min(Maze.HouseCentre.Y, g.Pos.Y + dist) };
                if (g.Pos.Y >= Maze.HouseCentre.Y)
                {
                    // Back to life, and straight out again.
                    g.State = GhostState.Leaving;
                    g.Frightened = false;
                }
                return;
        }

        if (g.ReversePending && g.State == GhostState.Active)
        {
            g.ReversePending = false;
            g.Dir = g.Dir.Reverse();
        }
        Advance(g, dist, _ => ChooseWay(g));
    }

    /// <summary>At a tile centre: the way to go next. Never back the way it came.</summary>
    private bool ChooseWay(Ghost g)
    {
        var tile = g.Tile;
        if (g.State == GhostState.Eaten && tile.Row == 11 && tile.Col is 13 or 14)
        {
            g.State = GhostState.Entering;
            return false;
        }
        var options = Dirs.Preference.Where(d => d != g.Dir.Reverse() && Maze.IsOpen(tile + d)).ToList();
        if (options.Count == 0)
        {
            g.Dir = g.Dir.Reverse();
            return true;
        }
        if (g.Frightened && g.State == GhostState.Active)
        {
            g.Dir = options[_rng.Next(options.Count)];
            return true;
        }
        var target = g.Target = TargetOf(g);
        var best = options[0];
        double bestDist = double.MaxValue;
        foreach (var d in options)
        {
            double dist = (tile + d).DistanceSq(target);
            if (dist < bestDist) { bestDist = dist; best = d; }
        }
        g.Dir = best;
        return true;
    }

    /// <summary>Where a ghost is heading: its corner when scattering, its own idea of the player when chasing.</summary>
    public Tile TargetOf(Ghost g)
    {
        if (g.State is GhostState.Eaten) return new Tile(13, 11);
        bool chase = Chasing || (g.Id == Ghost.Red && Elroy > 0);
        if (!chase) return g.Corner;
        var pt = Player.Tile;
        var pd = Player.Dir;
        switch (g.Id)
        {
            case Ghost.Red:
                return pt;
            case Ghost.Pink:
                // Four tiles ahead of the player (and, facing up, four to the left as well, as the arcade did).
                var ahead = pt.Ahead(pd, 4);
                return pd == Dir.Up ? ahead with { Col = ahead.Col - 4 } : ahead;
            case Ghost.Cyan:
                var pivot = pt.Ahead(pd, 2);
                if (pd == Dir.Up) pivot = pivot with { Col = pivot.Col - 2 };
                var red = Ghosts[Ghost.Red].Tile;
                return new Tile(2 * pivot.Col - red.Col, 2 * pivot.Row - red.Row);
            default:
                return pt.DistanceSq(g.Tile) > 64 ? pt : g.Corner;
        }
    }

    /// <summary>Lets ghosts out of the house: on a count of dots, or when the player stops eating for a while.</summary>
    private void Release(double dt)
    {
        var waiting = Ghosts.FirstOrDefault(g => g.State == GhostState.InHouse);
        if (waiting is null)
        {
            _globalCounting = false;
            return;
        }
        _idleTime += dt;
        bool go;
        if (_globalCounting)
            go = _globalDots >= waiting.Id switch { Ghost.Pink => 7, Ghost.Cyan => 17, _ => 32 };
        else
            go = waiting.DotCount >= Levels.DotLimit(waiting.Id, Level);
        if (_idleTime >= (Level < 5 ? 4 : 3))
        {
            go = true;
            _idleTime = 0;
        }
        if (go) waiting.State = GhostState.Leaving;
    }

    private void Collide()
    {
        var pt = Player.Tile;
        foreach (var g in Ghosts)
        {
            if (g.State is not (GhostState.Active or GhostState.Leaving) || g.Tile != pt) continue;
            if (g.State == GhostState.Leaving && g.Pos.Y > Maze.DoorOutside.Y + 4) continue;
            if (g.Frightened)
            {
                int points = 200 << _ghostsEatenThisFright;
                _ghostsEatenThisFright = Math.Min(3, _ghostsEatenThisFright + 1);
                AddScore(points);
                Popup(g.Pos, points);
                g.State = GhostState.Eaten;
                g.Frightened = false;
                _justEaten.Add(g);
                SetPhase(Phase.GhostEaten);
                Play(Sfx.EatGhost);
                return;
            }
            SetPhase(Phase.Dying);
            LossLog.Add($"L{Level}: caught by {Ghost.Names[g.Id]} at {pt}, {DotsLeft} dots left");
            return;
        }
    }

    internal List<string> LossLog { get; } = [];

    // ----- for the tests and screenshots --------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }

    /// <summary>Skips the "READY!" pause.</summary>
    internal void Go()
    {
        if (Phase == Phase.Ready) SetPhase(Phase.Playing);
    }

    internal void JumpToLevel(int level)
    {
        StartLevel(level);
        Go();
    }

    /// <summary>Eats every dot but <paramref name="leave"/> (taken from the end of the maze).</summary>
    internal void EatAllBut(int leave)
    {
        var all = new List<(int C, int R)>();
        for (int r = 0; r < Maze.Rows; r++)
            for (int c = 0; c < Maze.Cols; c++)
                if (Dots[c, r] || Energizers[c, r]) all.Add((c, r));
        foreach (var (c, r) in all.Take(Math.Max(0, all.Count - leave)))
        {
            Dots[c, r] = Energizers[c, r] = false;
            DotsLeft--;
            DotsEaten++;
        }
    }

    internal void PutPlayer(Tile t, Dir d)
    {
        Player.Pos = Maze.Centre(t);
        Player.Dir = d;
        Player.Wanted = Dir.None;
        Player.Stopped = false;
    }

    internal void PutGhost(int id, Tile t, Dir d, GhostState state = GhostState.Active)
    {
        var g = Ghosts[id];
        g.Pos = Maze.Centre(t);
        g.Dir = d;
        g.State = state;
    }

    /// <summary>Puts every ghost back in the house and keeps them there.</summary>
    internal void Shut()
    {
        foreach (var g in Ghosts)
        {
            g.State = GhostState.InHouse;
            g.Pos = Maze.HouseCentre;
        }
        _idleTime = double.MinValue;
        foreach (var g in Ghosts) g.DotCount = int.MinValue / 2;
    }

    internal void SetMode(int index)
    {
        ModeIndex = index;
        _modeTime = 0;
    }
}
