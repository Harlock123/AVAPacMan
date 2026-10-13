using Avalonia;
using Avalonia.Media;
using AVAPacMan.Engine;
using AVAPacMan.Models;

namespace AVAPacMan.Views;

public sealed partial class GameCanvas
{
    private const double RightPanel = FieldLeft + Maze.Width * Scale;

    /// <summary>The panels either side: the scores and spare lives on the left; the level, its fruit and the keys on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        if (!playing || _clock % 0.6 < 0.4 || g.Phase != Phase.Playing)
            Text(dc, "1UP", new Point(lx, 60), 30, White, center: true);
        Text(dc, (playing ? g.Score : 0).ToString(), new Point(lx, 92), 34, White, center: true);
        Text(dc, "HIGH SCORE", new Point(lx, 160), 30, White, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(lx, 192), 34, White, center: true);

        if (playing)
        {
            // Spare lives: the one in play isn't counted.
            int spare = g.Lives - (g.Phase is Phase.Dying ? 0 : 1);
            for (int i = 0; i < Math.Min(spare, 5); i++)
            {
                var c = new Point(lx - 44 + i * 30, 700);
                DrawPanelPlayer(dc, c);
            }
            Text(dc, "LEVEL", new Point(rx, 60), 30, White, center: true);
            Text(dc, g.Level.ToString(), new Point(rx, 92), 34, Yellow, center: true);
            for (int i = 0; i < g.FruitHistory.Count; i++)
                DrawPanelSprite(dc, Sprites.FruitArt(g.FruitHistory[^(i + 1)]), rx - 18 + (i % 2) * 36 - 18, 140 + (i / 2) * 40);
        }
        Text(dc, "AVAPACMAN", new Point(rx, 560), 24, Dim, center: true);
        Text(dc, "Arrows or WASD", new Point(rx, 590), 20, Dim, center: true);
        Text(dc, "Enter or Space starts", new Point(rx, 610), 20, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 640), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 660), 20, Dim, center: true);
    }

    private static void DrawPanelPlayer(DrawingContext dc, Point c)
    {
        double r = 11;
        Point At(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(c, true);
            ctx.LineTo(At(180 + 35));
            ctx.ArcTo(At(180 - 35 + 360), new Size(r, r), 0, true, SweepDirection.Clockwise);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(Yellow, null, geo);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double left, double top)
    {
        const double px = 3;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                dc.FillRectangle(Brush(Sprites.Fixed.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(left + c * px, top + r * px, px + 0.2, px + 0.2));
            }
    }

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Maze.Width / 2.0;
        Text(dc, "AVA PAC-MAN", P(cx, 18), 76, Yellow, center: true);
        Text(dc, "AFTER THE 1980 ARCADE CLASSIC", P(cx, 32), 26, Dim, center: true);
        Text(dc, "CHARACTER  /  NICKNAME", P(cx, 50), 30, White, center: true);
        // The ghosts come on one by one.
        for (int i = 0; i < 4; i++)
        {
            if (Engine.StateTime < 0.7 + i * 0.8) continue;
            double y = 64 + i * 18;
            var gh = new Ghost { Id = i, Dir = Dir.Right, State = GhostState.Active };
            DrawGhost(dc, gh, new Vec(36, y));
            Text(dc, $"{Ghost.Names[i],-7}  \"{Ghost.Nicknames[i]}\"", P(52, y), 30, Brush(Sprites.GhostColours[i]));
        }
        dc.FillRectangle(Brush(DotColour), new Rect(P(cx - 34, 141), P(cx - 32, 143)));
        Text(dc, "10 PTS", P(cx - 24, 142), 28, White);
        if (_clock % 0.4 < 0.22) dc.DrawEllipse(Brush(DotColour), null, P(cx - 33, 154), 3.6 * Scale, 3.6 * Scale);
        Text(dc, "50 PTS", P(cx - 24, 154), 28, White);
        Text(dc, "BLUE GHOSTS: 200  400  800  1600", P(cx, 170), 28, Brush(0xFF6A8AFF), center: true);
        Text(dc, "FRUIT: 100 TO 5000", P(cx, 182), 28, Brush(0xFFFF6A6A), center: true);
        DrawSprite(dc, Sprites.Cherry, cx + 50, 176, null);

        // A little chase along the bottom.
        double t = _clock % 8;
        double px = 240 - t * 40;
        using (dc.PushClip(new Rect(P(0, 0), P(Maze.Width, Maze.Height))))
        {
            DrawPlayer(dc, new Vec(px, 206), Dir.Left, 4 + 40 * Math.Abs(Math.Sin(_clock * 10)));
            for (int i = 0; i < 4; i++)
                DrawGhost(dc, new Ghost { Id = i, Dir = Dir.Left, State = GhostState.Active }, new Vec(px + 26 + i * 18, 206));
        }

        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 226), 32, White, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 240), 22, Dim, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Maze.Width / 2.0;
        Text(dc, "TOP MUNCHERS", P(cx, 30), 60, Yellow, center: true);
        Text(dc, "RANK    SCORE   NAME  LEVEL", P(cx, 50), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : Brush(Sprites.GhostColours[i % 4]);
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}  {e.Stage,4}", P(cx, 66 + i * 14), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 220), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(16, 92), P(208, 170)));
        double cx = Maze.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 102), 44, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 116), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 12 + i * 12, 138);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 162), 22, Dim, center: true);
    }
}
