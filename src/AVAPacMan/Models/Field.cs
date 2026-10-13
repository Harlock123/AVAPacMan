namespace AVAPacMan.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>A tile of the maze.</summary>
public readonly record struct Tile(int Col, int Row)
{
    public static Tile operator +(Tile t, Dir d) => new(t.Col + d.Dx(), t.Row + d.Dy());
    public Tile Ahead(Dir d, int n) => new(Col + d.Dx() * n, Row + d.Dy() * n);
    public double DistanceSq(Tile o) => (double)(Col - o.Col) * (Col - o.Col) + (double)(Row - o.Row) * (Row - o.Row);
    public override string ToString() => $"[{Col},{Row}]";
}

public enum Dir { None, Up, Left, Down, Right }

public static class Dirs
{
    /// <summary>The order ghosts prefer when two ways are equally good.</summary>
    public static readonly Dir[] Preference = [Dir.Up, Dir.Left, Dir.Down, Dir.Right];

    public static int Dx(this Dir d) => d switch { Dir.Left => -1, Dir.Right => 1, _ => 0 };
    public static int Dy(this Dir d) => d switch { Dir.Up => -1, Dir.Down => 1, _ => 0 };

    public static Dir Reverse(this Dir d) => d switch
    {
        Dir.Up => Dir.Down,
        Dir.Down => Dir.Up,
        Dir.Left => Dir.Right,
        Dir.Right => Dir.Left,
        _ => Dir.None,
    };
}

/// <summary>
/// The maze: 28 by 31 tiles of 8 pixels, this game's own layout. Corridors are one tile wide; the
/// tunnel on row 14 wraps from one side to the other; the ghosts' house sits in the middle with its
/// door on top. Only the left half is written out: the right half mirrors it.
/// </summary>
public static class Maze
{
    public const int TileSize = 8;
    public const int Cols = 28;
    public const int Rows = 31;
    public const int Width = Cols * TileSize;
    public const int Height = Rows * TileSize;
    public const double Step = 1.0 / 60;

    private static readonly string[] Left =
    [
        "##############",
        "#............#",
        "#.####.#####.#",
        "#o####.#####.#",
        "#.####.#####.#",
        "#.............",
        "#.####.##.####",
        "#.####.##.####",
        "#......##....#",
        "######.##### #",
        "######.##### #",
        "######.##     ",
        "######.## ###-",
        "######.## #   ",
        "      .   #   ",
        "######.## #   ",
        "######.## ####",
        "######.##     ",
        "######.## ####",
        "######.## ####",
        "#............#",
        "#.####.#####.#",
        "#.####.#####.#",
        "#o..##........",
        "###.##.##.####",
        "###.##.##.####",
        "#......##....#",
        "#.##########.#",
        "#.##########.#",
        "#.............",
        "##############",
    ];

    /// <summary>The whole maze, row by row: '#' wall, '.' dot, 'o' energizer, '-' the house door, ' ' open.</summary>
    public static readonly string[] Layout = Left.Select(r => r + new string(r.Reverse().ToArray())).ToArray();

    public const int TunnelRow = 14;
    /// <summary>Where the player starts: on row 23, between columns 13 and 14.</summary>
    public static readonly Vec PlayerStart = new(14 * TileSize, 23 * TileSize + 4);
    /// <summary>Just above the house door, where ghosts come out (and the red one starts).</summary>
    public static readonly Vec DoorOutside = new(14 * TileSize, 11 * TileSize + 4);
    public static readonly Vec HouseCentre = new(14 * TileSize, 14 * TileSize + 4);
    public static readonly Vec HouseLeft = new(12 * TileSize, 14 * TileSize + 4);
    public static readonly Vec HouseRight = new(16 * TileSize, 14 * TileSize + 4);
    /// <summary>Where the fruit appears, below the house.</summary>
    public static readonly Vec FruitSpot = new(14 * TileSize, 17 * TileSize + 4);

    public static char At(int col, int row)
    {
        if (row is < 0 or >= Rows) return '#';
        if (col is < 0 or >= Cols) return row == TunnelRow ? ' ' : '#';
        return Layout[row][col];
    }

    public static char At(Tile t) => At(t.Col, t.Row);

    /// <summary>Open to the player and to ghosts outside the house (the door is shut to them).</summary>
    public static bool IsOpen(Tile t)
    {
        char c = At(Wrap(t));
        return c is '.' or 'o' or ' ' && !InHouse(t);
    }

    public static bool InHouse(Tile t) => t.Row is >= 13 and <= 15 && t.Col is >= 11 and <= 16;

    public static bool InTunnel(Tile t) => t.Row == TunnelRow && (t.Col <= 5 || t.Col >= Cols - 6);

    /// <summary>The tile with its column brought back into the maze (the tunnel wraps).</summary>
    public static Tile Wrap(Tile t) => t with { Col = ((t.Col % Cols) + Cols) % Cols };

    public static Tile TileOf(Vec p) => new((int)Math.Floor(p.X / TileSize), (int)Math.Floor(p.Y / TileSize));

    public static Vec Centre(Tile t) => new(t.Col * TileSize + TileSize / 2.0, t.Row * TileSize + TileSize / 2.0);

    public static int CountDots() => Layout.Sum(r => r.Count(c => c is '.' or 'o'));
}
