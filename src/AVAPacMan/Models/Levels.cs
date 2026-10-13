namespace AVAPacMan.Models;

public enum Fruit { Cherry, Strawberry, Orange, Apple, Melon, Rocket, Bell, Key }

/// <summary>
/// How each level plays: speeds (as fractions of the top speed), how long the ghosts stay frightened,
/// the scatter-and-chase timetable, which fruit appears, and when the ghosts leave the house. The early
/// levels are gentle; from level 5 the player and the ghosts are at full pace.
/// </summary>
public static class Levels
{
    /// <summary>Full speed, in pixels a second.</summary>
    public const double TopSpeed = 75.75;

    public static double PlayerSpeed(int level, bool fright) => level switch
    {
        1 => fright ? 0.90 : 0.80,
        <= 4 => fright ? 0.95 : 0.90,
        <= 20 => 1.00,
        _ => 0.90,
    };

    public static double GhostSpeed(int level) => level switch { 1 => 0.75, <= 4 => 0.85, _ => 0.95 };
    public static double FrightGhostSpeed(int level) => level switch { 1 => 0.50, <= 4 => 0.55, _ => 0.60 };
    public static double TunnelSpeed(int level) => level switch { 1 => 0.40, <= 4 => 0.45, _ => 0.50 };

    private static readonly double[] FrightSeconds = [6, 5, 4, 3, 2, 5, 2, 2, 1, 5, 2, 1, 1, 3, 1, 1, 0, 1];

    /// <summary>How long an energizer frightens the ghosts; from level 19 it only turns them round.</summary>
    public static double FrightTime(int level) => level <= FrightSeconds.Length ? FrightSeconds[level - 1] : 0;

    /// <summary>How many times the ghosts flash white before they recover.</summary>
    public static int Flashes(int level) => FrightTime(level) <= 1 ? 3 : 5;

    /// <summary>Scatter, chase, scatter, chase... in seconds; the last chase lasts for good.</summary>
    public static double[] Schedule(int level) => level switch
    {
        1 => [7, 20, 7, 20, 5, 20, 5],
        <= 4 => [7, 20, 7, 20, 5, 1033, 1.0 / 60],
        _ => [5, 20, 5, 20, 5, 1037, 1.0 / 60],
    };

    public static Fruit FruitFor(int level) => level switch
    {
        1 => Fruit.Cherry,
        2 => Fruit.Strawberry,
        <= 4 => Fruit.Orange,
        <= 6 => Fruit.Apple,
        <= 8 => Fruit.Melon,
        <= 10 => Fruit.Rocket,
        <= 12 => Fruit.Bell,
        _ => Fruit.Key,
    };

    public static int FruitPoints(Fruit f) => f switch
    {
        Fruit.Cherry => 100,
        Fruit.Strawberry => 300,
        Fruit.Orange => 500,
        Fruit.Apple => 700,
        Fruit.Melon => 1000,
        Fruit.Rocket => 2000,
        Fruit.Bell => 3000,
        _ => 5000,
    };

    /// <summary>Dots the player must eat before a ghost in the house may leave (pink, cyan, orange).</summary>
    public static int DotLimit(int ghost, int level) => ghost switch
    {
        2 => level == 1 ? 30 : 0,
        3 => level == 1 ? 60 : level == 2 ? 50 : 0,
        _ => 0,
    };

    /// <summary>With this few dots left the red ghost speeds up; with half as many, more again.</summary>
    public static int ElroyDots(int level) => level switch
    {
        1 => 20,
        2 => 30,
        <= 5 => 40,
        <= 8 => 50,
        <= 11 => 60,
        <= 14 => 80,
        <= 18 => 100,
        _ => 120,
    };

    /// <summary>The short scenes between levels: after 2, 5 and 9, then every four levels.</summary>
    public static bool IntermissionAfter(int level) => level is 2 or 5 or 9 || (level > 9 && (level - 9) % 4 == 0);
}
