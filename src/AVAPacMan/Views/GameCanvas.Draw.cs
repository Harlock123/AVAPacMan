using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAPacMan.Engine;
using AVAPacMan.Models;

namespace AVAPacMan.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAPacMan/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(Sprites.PlayerColour);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Cyan = Brush(0xFF2AF0F0);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);
    private const uint WallColour = 0xFF2A3CFF;
    private const uint DotColour = 0xFFFFC0A8;

    /// <summary>Maze pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        switch (g.State)
        {
            case GameState.Title:
                DrawTitle(dc);
                break;
            case GameState.HighScores:
                DrawHighScores(dc);
                break;
            default:
                if (g.Phase == Phase.Intermission) DrawIntermission(dc, g.PhaseTime);
                else DrawPlay(dc);
                break;
        }
        DrawPanels(dc);
    }

    // ----- the maze -----------------------------------------------------------------------------

    private static readonly (Point A, Point B)[] WallLines = BuildWalls();

    private static bool IsWall(int c, int r)
    {
        if (r is < 0 or >= Maze.Rows) return true;
        if (c is < 0 or >= Maze.Cols) return r != Maze.TunnelRow;
        return Maze.Layout[r][c] == '#';
    }

    /// <summary>An outline just inside each wall, along every edge it shares with a corridor.</summary>
    private static (Point, Point)[] BuildWalls()
    {
        const double inset = 2, t = Maze.TileSize;
        var lines = new List<(Point, Point)>();
        for (int r = 0; r < Maze.Rows; r++)
            for (int c = 0; c < Maze.Cols; c++)
            {
                if (!IsWall(c, r)) continue;
                double x = c * t, y = r * t;
                bool up = !IsWall(c, r - 1), down = !IsWall(c, r + 1), left = !IsWall(c - 1, r), right = !IsWall(c + 1, r);
                double x0 = left ? x + inset : x, x1 = right ? x + t - inset : x + t;
                double y0 = up ? y + inset : y, y1 = down ? y + t - inset : y + t;
                if (up) lines.Add((P(x0, y + inset), P(x1, y + inset)));
                if (down) lines.Add((P(x0, y + t - inset), P(x1, y + t - inset)));
                if (left) lines.Add((P(x + inset, y0), P(x + inset, y1)));
                if (right) lines.Add((P(x + t - inset, y0), P(x + t - inset, y1)));
                // Inside corners: a corridor only diagonally next to this wall.
                if (!up && !left && !IsWall(c - 1, r - 1)) { lines.Add((P(x, y + inset), P(x + inset, y + inset))); lines.Add((P(x + inset, y), P(x + inset, y + inset))); }
                if (!up && !right && !IsWall(c + 1, r - 1)) { lines.Add((P(x + t - inset, y + inset), P(x + t, y + inset))); lines.Add((P(x + t - inset, y), P(x + t - inset, y + inset))); }
                if (!down && !left && !IsWall(c - 1, r + 1)) { lines.Add((P(x, y + t - inset), P(x + inset, y + t - inset))); lines.Add((P(x + inset, y + t - inset), P(x + inset, y + t))); }
                if (!down && !right && !IsWall(c + 1, r + 1)) { lines.Add((P(x + t - inset, y + t - inset), P(x + t, y + t - inset))); lines.Add((P(x + t - inset, y + t - inset), P(x + t - inset, y + t))); }
            }
        return lines.ToArray();
    }

    private void DrawMaze(DrawingContext dc)
    {
        var g = Engine;
        // At the end of a level the walls flash.
        bool flash = g.Phase == Phase.LevelClear && g.PhaseTime > 1 && (int)((g.PhaseTime - 1) / 0.22) % 2 == 0;
        var pen = new Pen(Brush(flash ? 0xFFF2F2F2 : WallColour), 2.2, lineCap: PenLineCap.Square);
        foreach (var (a, b) in WallLines) dc.DrawLine(pen, a, b);
        // The house door.
        dc.FillRectangle(Brush(0xFFFFB0E8), new Rect(P(13 * 8, 12 * 8 + 3), P(15 * 8, 12 * 8 + 5)));

        bool blinkOn = g.Phase != Phase.Playing || _clock % 0.4 < 0.22;
        for (int r = 0; r < Maze.Rows; r++)
            for (int c = 0; c < Maze.Cols; c++)
            {
                if (g.Dots[c, r])
                    dc.FillRectangle(Brush(DotColour), new Rect(P(c * 8 + 3, r * 8 + 3), P(c * 8 + 5, r * 8 + 5)));
                else if (g.Energizers[c, r] && blinkOn)
                    dc.DrawEllipse(Brush(DotColour), null, P(c * 8 + 4, r * 8 + 4), 3.6 * Scale, 3.6 * Scale);
            }
    }

    // ----- the play -----------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Maze.Width, Maze.Height))))
        {
            DrawMaze(dc);
            if (g.FruitShowing is { } fruit) DrawSprite(dc, Sprites.FruitArt(fruit), Maze.FruitSpot.X - 6, Maze.FruitSpot.Y - 6, null);

            bool ghostsShown = !(g.Phase == Phase.Dying && g.PhaseTime >= 1) && !(g.Phase == Phase.LevelClear && g.PhaseTime > 1);
            if (ghostsShown)
                foreach (var gh in g.Ghosts)
                {
                    if (g.JustEaten(gh)) continue;
                    DrawGhost(dc, gh, gh.Pos);
                    // Through the tunnel: show the half that's come out of the other side.
                    if (gh.Pos.X < 8) DrawGhost(dc, gh, gh.Pos with { X = gh.Pos.X + Maze.Width });
                    else if (gh.Pos.X > Maze.Width - 8) DrawGhost(dc, gh, gh.Pos with { X = gh.Pos.X - Maze.Width });
                }

            if (g.Phase != Phase.GhostEaten)
            {
                if (g.Phase == Phase.Dying)
                {
                    if (g.PhaseTime < 1) DrawPlayer(dc, g.Player.Pos, g.Player.Dir, 30);
                    else if (g.PhaseTime < 2.6) DrawPlayer(dc, g.Player.Pos, Dir.Up, 30 + (g.PhaseTime - 1) / 1.6 * 150);
                }
                else
                {
                    double mouth = g.Player.Stopped ? 30 : 4 + 40 * Math.Abs(Math.Sin(g.Player.Travelled * Math.PI / 8));
                    if (g.Phase == Phase.Ready && g.Player.Travelled == 0) mouth = 0;
                    DrawPlayer(dc, g.Player.Pos, g.Player.Dir, mouth);
                }
            }
        }

        foreach (var fx in g.Effects)
            Text(dc, fx.Text, P(fx.Pos), 26, Cyan, center: true);

        var notice = P(Maze.Width / 2.0, 17 * 8 + 4);
        switch (g.State)
        {
            case GameState.Paused:
                Text(dc, "PAUSED", notice, 40, White, center: true);
                Text(dc, "ESC TO QUIT, ANY OTHER KEY TO RESUME", P(Maze.Width / 2.0, 20 * 8 + 4), 24, Dim, center: true);
                break;
            case GameState.GameOver:
                Text(dc, "GAME  OVER", notice, 40, Red, center: true);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
            default:
                if (g.Phase == Phase.Ready) Text(dc, "READY!", notice, 40, Yellow, center: true);
                else if (g.State == GameState.Attract) Text(dc, "DEMONSTRATION", notice, 34, Red, center: true);
                break;
        }
    }

    /// <summary>A ghost: its colour (or frightened blue, flashing white near the end), and eyes that look where it's going.</summary>
    private void DrawGhost(DrawingContext dc, Ghost gh, Vec at)
    {
        var g = Engine;
        double x = at.X - 7, y = at.Y - 7;
        int frame = (int)(_clock * 8) % 2;
        if (gh.State is not (GhostState.Eaten or GhostState.Entering))
        {
            if (gh.Frightened)
            {
                bool flashing = g.FrightLeft < Levels.Flashes(g.Level) * 0.4 && (int)(g.FrightLeft / 0.2) % 2 == 0;
                var pal = new Sprites.Palette(flashing ? Sprites.FlashWhite : Sprites.FrightBlue, flashing ? Sprites.FlashFace : Sprites.FrightFace);
                DrawSprite(dc, Sprites.GhostBody[frame], x, y, pal);
                DrawSprite(dc, Sprites.FrightFaceArt, x, y, pal);
                return;
            }
            DrawSprite(dc, Sprites.GhostBody[frame], x, y, new Sprites.Palette(Sprites.GhostColours[gh.Id], 0));
        }
        DrawEyes(dc, x, y, gh.Dir);
    }

    private static void DrawEyes(DrawingContext dc, double x, double y, Dir dir)
    {
        int dx = dir.Dx(), dy = dir.Dy();
        foreach (double ex in new[] { x + 2 + dx, x + 8 + dx })
        {
            double ey = y + 3 + dy;
            DrawSprite(dc, Sprites.Eye, ex, ey, null);
            dc.FillRectangle(Brush(0xFF2A48E0), new Rect(P(ex + 1 + dx, ey + 1.5 + dy * 1.5), P(ex + 3 + dx, ey + 3.5 + dy * 1.5)));
        }
    }

    /// <summary>The player: a yellow disc with a mouth opening <paramref name="mouth"/> degrees each side of the way it faces.</summary>
    private static void DrawPlayer(DrawingContext dc, Vec at, Dir dir, double mouth, double radius = 6.6)
    {
        double facing = dir switch { Dir.Up => 270, Dir.Left => 180, Dir.Down => 90, _ => 0 };
        if (mouth >= 179.5) return;
        var c = P(at);
        double r = radius * Scale;
        if (mouth <= 0.5)
        {
            dc.DrawEllipse(Yellow, null, c, r, r);
            return;
        }
        Point At(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(c, true);
            ctx.LineTo(At(facing + mouth));
            ctx.ArcTo(At(facing - mouth + 360), new Size(r, r), 0, 360 - 2 * mouth > 180, SweepDirection.Clockwise);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(Yellow, null, geo);
    }

    /// <summary>Between levels: chased one way, then the chase turned round.</summary>
    private void DrawIntermission(DrawingContext dc, double t)
    {
        const double y = 15 * 8;
        if (t < 3.5)
        {
            double px = 250 - t * 80;
            DrawPlayer(dc, new Vec(px, y), Dir.Left, 4 + 40 * Math.Abs(Math.Sin(t * 12)));
            var chaser = new Ghost { Id = Ghost.Red, Dir = Dir.Left, State = GhostState.Active };
            DrawGhost(dc, chaser, new Vec(px + 30, y));
        }
        else
        {
            double s = t - 3.5;
            double gx = -30 + s * 70;
            using (dc.PushClip(new Rect(P(0, 0), P(Maze.Width, Maze.Height))))
            {
                DrawSprite(dc, Sprites.GhostBody[(int)(_clock * 8) % 2], gx - 7, y - 7, new Sprites.Palette(Sprites.FrightBlue, Sprites.FrightFace));
                DrawSprite(dc, Sprites.FrightFaceArt, gx - 7, y - 7, new Sprites.Palette(Sprites.FrightBlue, Sprites.FrightFace));
                DrawPlayer(dc, new Vec(gx - 40, y - 8), Dir.Right, 4 + 40 * Math.Abs(Math.Sin(s * 10)), 20);
            }
        }
        Text(dc, "COFFEE BREAK", P(Maze.Width / 2.0, 8 * 8), 40, Yellow, center: true);
    }

    /// <summary>Draws a sprite with its top-left at a maze point; A and B take the palette's colours.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, Sprites.Palette? pal, double px = 1)
    {
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[c];
                if (ch == '.') continue;
                uint argb = ch switch
                {
                    'A' => pal?.A ?? 0xFFFF00FF,
                    'B' => pal?.B ?? 0xFFFF00FF,
                    _ => Sprites.Fixed.GetValueOrDefault(ch, 0xFFFF00FF),
                };
                var a = P(x + c * px, y + r * px);
                dc.FillRectangle(Brush(argb), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
