namespace AVAPacMan.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. A and B take a palette; the other letters are fixed
/// colours. The player is drawn as a shape, not a sprite, so its mouth can open by any amount.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>Colours for A and B.</summary>
    public sealed record Palette(uint A, uint B);

    public static readonly uint[] GhostColours = [0xFFFF2A2A, 0xFFFFB0E8, 0xFF2AF0F0, 0xFFFFB04A];
    public const uint PlayerColour = 0xFFFFE600;
    public const uint FrightBlue = 0xFF2A3CFF;
    public const uint FrightFace = 0xFFFFC8B0;
    public const uint FlashWhite = 0xFFF2F2F2;
    public const uint FlashFace = 0xFFFF2A2A;

    public static readonly IReadOnlyDictionary<char, uint> Fixed = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F4F4, // white
        ['u'] = 0xFF2A48E0, // pupil blue
        ['r'] = 0xFFFF2A2A, // red
        ['R'] = 0xFFB01818, // dark red
        ['g'] = 0xFF3CD04A, // green
        ['G'] = 0xFF1E7A2A, // dark green
        ['o'] = 0xFFFF9A1E, // orange
        ['y'] = 0xFFFFE14A, // yellow
        ['b'] = 0xFF8A5A2A, // brown
        ['k'] = 0xFFB8B8C8, // steel
        ['c'] = 0xFF4FD7FF, // cyan
        ['p'] = 0xFFFFB0E8, // pink
    };

    /// <summary>A ghost's body, two frames for its rippling skirt. Its eyes are drawn on top.</summary>
    public static readonly Sprite[] GhostBody =
    [
        S(".....AAAA.....",
          "...AAAAAAAA...",
          "..AAAAAAAAAA..",
          ".AAAAAAAAAAAA.",
          ".AAAAAAAAAAAA.",
          ".AAAAAAAAAAAA.",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AA.AAA..AAA.AA",
          "A...AA..AA...A"),
        S(".....AAAA.....",
          "...AAAAAAAA...",
          "..AAAAAAAAAA..",
          ".AAAAAAAAAAAA.",
          ".AAAAAAAAAAAA.",
          ".AAAAAAAAAAAA.",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAAAAAAAAAAAA",
          "AAAA.AAAA.AAAA",
          ".AA...AA...AA."),
    ];

    /// <summary>An eye: the white; the pupil is drawn looking the way the ghost goes.</summary>
    public static readonly Sprite Eye = S(".ww.", "wwww", "wwww", "wwww", ".ww.");

    /// <summary>A frightened ghost's face: two small eyes and a wobbly mouth (B).</summary>
    public static readonly Sprite FrightFaceArt = S(
        "..............",
        "..............",
        "..............",
        "..............",
        "....BB..BB....",
        "....BB..BB....",
        "..............",
        "..............",
        ".BB..BB..BB...",
        "B..BB..BB..B..",
        "..............",
        "..............",
        "..............",
        "..............");

    public static readonly Sprite Cherry = S(
        "........GG..",
        ".......G.G..",
        "......G..G..",
        ".....G...G..",
        "....G....G..",
        "..rrr...G...",
        ".rrwrr.G....",
        ".rwrrrrrr...",
        ".rrrrrwrrr..",
        "..rrr.rrrr..",
        "......rrrr..",
        ".......rr...");

    public static readonly Sprite Strawberry = S(
        "....GGGG....",
        "...GGgGGG...",
        "..rrrGGrrr..",
        ".rrwrrrrrwr.",
        ".rrrrrwrrrr.",
        ".rwrrrrrrwr.",
        ".rrrrwrrrrr.",
        "..rrrrrrwr..",
        "..rwrrrrrr..",
        "...rrrwrr...",
        "....rrrr....",
        ".....rr.....");

    public static readonly Sprite Orange = S(
        ".....bG.....",
        "....GGgG....",
        "...oooooo...",
        "..oooooyoo..",
        ".oooooooyoo.",
        ".oooooooooo.",
        ".oooooooooo.",
        ".oooooooooo.",
        "..oooooooo..",
        "...oooooo...",
        "....oooo....",
        "............");

    public static readonly Sprite Apple = S(
        ".....b......",
        ".....b.GG...",
        "..rrrbrrG...",
        ".rrrrrrrrr..",
        "rrwwrrrrrrr.",
        "rrwrrrrrrrr.",
        "rrrrrrrrrrr.",
        "rrrrrrrrrrr.",
        ".rrrrrrrrr..",
        ".rrrrrrrrr..",
        "..rrr.rrr...",
        "............");

    public static readonly Sprite Melon = S(
        "....bb......",
        "...GgGgG....",
        "..GgGgGgG...",
        ".GgGgGgGgG..",
        ".gGgGgGgGg..",
        ".GgGgGgGgG..",
        ".gGgGgGgGg..",
        ".GgGgGgGgG..",
        "..GgGgGgG...",
        "...GgGgG....",
        "....GgG.....",
        "............");

    public static readonly Sprite Rocket = S(
        ".....ww.....",
        "....wwww....",
        "....wccw....",
        "....wwww....",
        "....wrrw....",
        "....wwww....",
        "...wwwwww...",
        "..rwwwwwwr..",
        ".rr.wwww.rr.",
        "....oyyo....",
        ".....yy.....",
        ".....o......");

    public static readonly Sprite Bell = S(
        ".....yy.....",
        "....yyyy....",
        "...yywyyy...",
        "...ywyyyy...",
        "..yywyyyyy..",
        "..yyyyyyyy..",
        "..yyyyyyyy..",
        ".yyyyyyyyyy.",
        "yyyyyyyyyyyy",
        "cccccccccccc",
        ".....ww.....",
        "............");

    public static readonly Sprite Key = S(
        "....cccc....",
        "...c....c...",
        "...c....c...",
        "....cccc....",
        ".....kk.....",
        ".....kk.....",
        ".....kkk....",
        ".....kk.....",
        ".....kkk....",
        ".....kk.....",
        ".....kkk....",
        "............");

    public static Sprite FruitArt(Fruit f) => f switch
    {
        Fruit.Cherry => Cherry,
        Fruit.Strawberry => Strawberry,
        Fruit.Orange => Orange,
        Fruit.Apple => Apple,
        Fruit.Melon => Melon,
        Fruit.Rocket => Rocket,
        Fruit.Bell => Bell,
        _ => Key,
    };
}
