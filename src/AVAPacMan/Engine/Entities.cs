using AVAPacMan.Models;

namespace AVAPacMan.Engine;

/// <summary>Anything that moves through the maze from tile centre to tile centre.</summary>
public abstract class Mover
{
    public Vec Pos { get; set; }
    public Dir Dir { get; set; } = Dir.Left;
    public Tile Tile => Maze.Wrap(Maze.TileOf(Pos));
}

public sealed class Player : Mover
{
    /// <summary>The way the player last asked to go; taken at the next tile where it's open.</summary>
    public Dir Wanted { get; set; } = Dir.None;
    /// <summary>Up against a wall.</summary>
    public bool Stopped { get; set; }
    /// <summary>Distance travelled, for the mouth.</summary>
    public double Travelled { get; set; }
}

public enum GhostState
{
    /// <summary>Bobbing in the house, waiting to be let out.</summary>
    InHouse,
    /// <summary>On the way out through the door.</summary>
    Leaving,
    /// <summary>In the maze, scattering or chasing.</summary>
    Active,
    /// <summary>Eaten: just eyes, hurrying back to the house.</summary>
    Eaten,
    /// <summary>The eyes going down through the door to come back to life.</summary>
    Entering,
}

/// <summary>
/// A ghost. Each one chases in its own way: red goes straight for the player, pink aims ahead of
/// them, cyan works with red to cut them off, and orange chases only from far away.
/// </summary>
public sealed class Ghost : Mover
{
    public const int Red = 0, Pink = 1, Cyan = 2, Orange = 3;
    public static readonly string[] Names = ["RED", "PINK", "CYAN", "ORANGE"];
    public static readonly string[] Nicknames = ["THE CHASER", "THE AMBUSHER", "THE FLANKER", "THE WANDERER"];

    public required int Id { get; init; }
    public GhostState State { get; set; }
    public bool Frightened { get; set; }
    public bool ReversePending { get; set; }
    /// <summary>Dots counted towards leaving the house.</summary>
    public int DotCount { get; set; }
    /// <summary>Its home corner in scatter mode (outside the maze, so it circles the nearest block).</summary>
    public Tile Corner => Id switch
    {
        Red => new Tile(25, -4),
        Pink => new Tile(2, -4),
        Cyan => new Tile(27, 31),
        _ => new Tile(0, 31),
    };
    /// <summary>Where it last decided to head, for the tests.</summary>
    public Tile Target { get; set; }
    /// <summary>For bobbing in the house: +1 down, −1 up.</summary>
    public int Bob { get; set; } = 1;
}

public enum EffectKind { Score }

/// <summary>Points shown where something was eaten.</summary>
public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: the way the player wants to go (or none).</summary>
public record struct Controls(Dir Wanted);
