namespace Nalix.Graphics.Map;

/// <summary>
/// Defines different types of tiles used in the game map.
/// </summary>
public enum TileType
{
    /// <summary>
    /// An empty tile, typically non-collidable and invisible.
    /// </summary>
    EMPTY = 0,

    /// <summary>
    /// A solid wall tile that blocks all movement.
    /// </summary>
    WALL = 1,

    /// <summary>
    /// A one-way platform that allows jumping through from below,
    /// but acts as a solid surface from above.
    /// </summary>
    ONEWAY_PLATFORM = 2,

    /// <summary>
    /// A water tile that may affect movement (e.g., swimming or slowing down).
    /// </summary>
    WATER = 3,

    /// <summary>
    /// A spike tile that damages or kills the player on contact.
    /// </summary>
    SPIKE = 4
}
