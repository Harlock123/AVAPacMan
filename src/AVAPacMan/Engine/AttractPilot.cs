using AVAPacMan.Models;

namespace AVAPacMan.Engine;

/// <summary>
/// The computer player for attract mode and the tests. Each step it works out how far every tile is
/// from the player and from the nearest dangerous ghost, then heads for the nearest dot it can reach
/// well before any ghost; it hunts frightened ghosts while there's time, takes an energizer when
/// ghosts close in, and otherwise runs for the most open ground.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private static readonly Dir[] Ways = [Dir.Up, Dir.Left, Dir.Down, Dir.Right];

    public Controls Decide(GameEngine g)
    {
        if (g.Phase != Phase.Playing) return default;
        var start = g.Player.Tile;
        var (dist, first, parent) = Search(start);

        // How soon a dangerous ghost could be at each tile.
        var danger = new int[Maze.Cols, Maze.Rows];
        for (int c = 0; c < Maze.Cols; c++)
            for (int r = 0; r < Maze.Rows; r++) danger[c, r] = int.MaxValue;
        bool frightLong = g.FrightLeft > 1.2;
        foreach (var gh in g.Ghosts)
        {
            if (gh.State is GhostState.Eaten or GhostState.Entering) continue;
            if (gh.Frightened && frightLong) continue;
            var from = gh.State == GhostState.Active ? gh.Tile : new Tile(13, 11);
            var (gd, _, _) = Search(from);
            int head = gh.State == GhostState.Active ? 0 : 3;
            for (int c = 0; c < Maze.Cols; c++)
                for (int r = 0; r < Maze.Rows; r++)
                    if (gd[c, r] != int.MaxValue) danger[c, r] = Math.Min(danger[c, r], gd[c, r] + head);
        }

        Dir best = Dir.None;
        // Hunt frightened ghosts that can be reached in time.
        if (g.FrightLeft > 0)
        {
            double tilesPerSecond = Levels.PlayerSpeed(g.Level, true) * Levels.TopSpeed / Maze.TileSize;
            int bestD = int.MaxValue;
            foreach (var gh in g.Ghosts)
            {
                if (!gh.Frightened || gh.State != GhostState.Active) continue;
                int d = dist[gh.Tile.Col, gh.Tile.Row];
                if (d < bestD && d < g.FrightLeft * tilesPerSecond * 0.8) { bestD = d; best = first[gh.Tile.Col, gh.Tile.Row]; }
            }
        }

        if (best == Dir.None)
        {
            // The nearest dot that's safely ours (an energizer counts as safe sooner when ghosts are near).
            int bestD = int.MaxValue;
            for (int c = 0; c < Maze.Cols; c++)
                for (int r = 0; r < Maze.Rows; r++)
                {
                    bool energizer = g.Energizers[c, r];
                    if (!g.Dots[c, r] && !energizer) continue;
                    int d = dist[c, r];
                    if (d == int.MaxValue || d == 0) continue;
                    int margin = energizer ? 1 : 3;
                    if (d + margin >= danger[c, r] || !PathSafe(c, r)) continue;
                    // Save the energizers for when they're needed.
                    int cost = energizer && g.FrightLeft <= 0 && NearestDanger() > 8 ? d + 30 : d;
                    if (cost < bestD) { bestD = cost; best = first[c, r]; }
                }
        }

        if (best == Dir.None)
        {
            // Nowhere safe to eat: head for the tile with the most room between us and the ghosts.
            int bestRoom = int.MinValue;
            for (int c = 0; c < Maze.Cols; c++)
                for (int r = 0; r < Maze.Rows; r++)
                {
                    int d = dist[c, r];
                    if (d == int.MaxValue || d == 0 || d > 12 || !PathSafe(c, r)) continue;
                    int room = Math.Min(danger[c, r], 40) - d + _rng.Next(2);
                    if (room > bestRoom) { bestRoom = room; best = first[c, r]; }
                }
        }
        return new Controls(best);

        int NearestDanger()
        {
            int n = int.MaxValue;
            foreach (var gh in g.Ghosts)
                if (gh.State == GhostState.Active && !gh.Frightened) n = Math.Min(n, dist[gh.Tile.Col, gh.Tile.Row]);
            return n;
        }

        // Every step of the way there is reached before a ghost could get to it.
        bool PathSafe(int c, int r)
        {
            var t = new Tile(c, r);
            for (int guard = 0; guard < 80 && t != start; guard++)
            {
                if (dist[t.Col, t.Row] + 1 >= danger[t.Col, t.Row]) return false;
                t = parent[t.Col, t.Row];
            }
            return true;
        }
    }

    /// <summary>Distances from a tile through the maze, and the first step of the way to each tile.</summary>
    private static (int[,] Dist, Dir[,] First, Tile[,] Parent) Search(Tile from)
    {
        var dist = new int[Maze.Cols, Maze.Rows];
        var first = new Dir[Maze.Cols, Maze.Rows];
        var parent = new Tile[Maze.Cols, Maze.Rows];
        for (int c = 0; c < Maze.Cols; c++)
            for (int r = 0; r < Maze.Rows; r++) dist[c, r] = int.MaxValue;
        from = Maze.Wrap(from);
        if (from.Row is < 0 or >= Maze.Rows) return (dist, first, parent);
        dist[from.Col, from.Row] = 0;
        var queue = new Queue<Tile>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            foreach (var w in Ways)
            {
                var n = Maze.Wrap(t + w);
                if (!Maze.IsOpen(n) || dist[n.Col, n.Row] != int.MaxValue) continue;
                dist[n.Col, n.Row] = dist[t.Col, t.Row] + 1;
                first[n.Col, n.Row] = t == from ? w : first[t.Col, t.Row];
                parent[n.Col, n.Row] = t;
                queue.Enqueue(n);
            }
        }
        return (dist, first, parent);
    }
}
