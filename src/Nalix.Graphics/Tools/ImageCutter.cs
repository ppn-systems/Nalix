using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nalix.Graphics.Tools;

/// <summary>
/// ScreenSize class responsible for cutting a large texture into smaller icon-sized sprites.
/// It supports cutting icons in a grid layout, flipping, rotating, and custom region extraction.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ImageCutter"/> class.
/// </remarks>
/// <param name="texture">The texture containing multiple icons.</param>
/// <param name="iconWidth">The width of each icon.</param>
/// <param name="iconHeight">The height of each icon.</param>
/// <exception cref="ArgumentNullException">Thrown if texture is null.</exception>
public class ImageCutter(Texture texture, Int32 iconWidth, Int32 iconHeight)
{
    private readonly Int32 _iconWidth = iconWidth;    // Width of each icon
    private readonly Int32 _iconHeight = iconHeight;   // Height of each icon

    /// <summary>
    /// Gets the texture used for cutting icons.
    /// </summary>
    public Texture Texture { get; } = texture
        ?? throw new ArgumentNullException(nameof(texture), "Texture cannot be null.");

    /// <summary>
    /// Cuts all icons from the large texture and returns them as an array of sprites.
    /// </summary>
    /// <param name="iconsPerRow">The number of icons per row.</param>
    /// <param name="iconsPerColumn">The number of icons per column.</param>
    /// <returns>ScreenSize task representing the asynchronous operation, with a result of an array of sprites.</returns>
    /// <remarks>
    /// This method uses asynchronous tasks to cut icons in parallel for better performance on large textures.
    /// </remarks>
    public async Task<Sprite[]> CutAllIconsAsync(Int32 iconsPerRow, Int32 iconsPerColumn)
    {
        var totalIcons = iconsPerRow * iconsPerColumn;
        var icons = new Sprite[totalIcons];

        var tasks = new List<Task>();

        // Asynchronously cut all icons using parallelization for performance
        for (Int32 row = 0; row < iconsPerColumn; row++)
        {
            for (Int32 col = 0; col < iconsPerRow; col++)
            {
                Int32 x = col * _iconWidth;
                Int32 y = row * _iconHeight;

                Int32 index = (row * iconsPerRow) + col;
                tasks.Add(Task.Run(() =>
                {
                    icons[index] = CreateIcon(x, y);
                }));
            }
        }

        // Await the completion of all tasks
        await Task.WhenAll(tasks);

        return icons;
    }

    /// <summary>
    /// Cuts a specific icon from the texture at the given index and number of icons per row.
    /// </summary>
    /// <param name="index">The index of the icon in the grid.</param>
    /// <param name="iconsPerRow">The number of icons per row.</param>
    /// <returns>ScreenSize Sprite representing the icon at the specified index.</returns>
    public Sprite CutIconAt(Int32 index, Int32 iconsPerRow)
    {
        Int32 row = index / iconsPerRow;
        Int32 col = index % iconsPerRow;
        return CreateIcon(col * _iconWidth, row * _iconHeight);
    }

    /// <summary>
    /// Gets the rectangle representing the icon's area in the texture at the specified column and row.
    /// </summary>
    /// <param name="column">The column of the icon.</param>
    /// <param name="row">The row of the icon.</param>
    /// <returns>An <see cref="IntRect"/> representing the icon's rectangle.</returns>
    public IntRect GetRectAt(Int32 column, Int32 row)
        => new(column * _iconWidth, row * _iconHeight, _iconWidth, _iconHeight);

    /// <summary>
    /// Creates a Sprite from a specific section of the texture based on x and y coordinates.
    /// </summary>
    /// <param name="x">The x-coordinate of the top-left corner of the icon.</param>
    /// <param name="y">The y-coordinate of the top-left corner of the icon.</param>
    /// <returns>ScreenSize Sprite representing the icon at the given coordinates.</returns>
    private Sprite CreateIcon(Int32 x, Int32 y)
    {
        IntRect iconRect = new(x, y, _iconWidth, _iconHeight);
        return new Sprite(Texture, iconRect);
    }

    /// <summary>
    /// Rotates the given Sprite by a specified angle.
    /// </summary>
    /// <param name="icon">The Sprite to be rotated.</param>
    /// <param name="angle">The angle of rotation in degrees.</param>
    /// <returns>The rotated Sprite.</returns>
    public static Sprite RotateIcon(Sprite icon, Single angle)
    {
        icon.Rotation = angle;
        return icon;
    }

    /// <summary>
    /// Cuts a custom region from the texture based on a specific rectangular area.
    /// </summary>
    /// <param name="region">The <see cref="IntRect"/> defining the region to be cut from the texture.</param>
    /// <returns>ScreenSize Sprite representing the custom region.</returns>
    /// <exception cref="ArgumentException">Thrown if the region exceeds the bounds of the texture.</exception>
    public Sprite CutCustomRegion(IntRect region)
    {
        return region.Left < 0 || region.Top < 0 || region.Left + region.Width > Texture.Size.X || region.Top + region.Height > Texture.Size.Y
            ? throw new ArgumentException("The region is out of bounds of the texture.")
            : new Sprite(Texture, region);
    }
}
