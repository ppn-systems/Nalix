using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nalix.Graphics.Tools;

/// <summary>
/// A class responsible for cutting a large texture into smaller icon-sized sprites.
/// It supports cutting icons in a grid layout, flipping, rotating, and custom region extraction.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ImageCutter"/> class.
/// </remarks>
/// <param name="texture">The texture containing multiple icons.</param>
/// <param name="iconWidth">The width of each icon.</param>
/// <param name="iconHeight">The height of each icon.</param>
/// <exception cref="ArgumentNullException">Thrown if texture is null.</exception>
public class ImageCutter(Texture texture, int iconWidth, int iconHeight)
{
    // The large texture containing multiple icons
    private readonly Texture _texture = texture
        ?? throw new ArgumentNullException(nameof(texture), "Texture cannot be null.");

    private readonly int _iconWidth = iconWidth;    // Width of each icon
    private readonly int _iconHeight = iconHeight;   // Height of each icon

    /// <summary>
    /// Gets the texture used for cutting icons.
    /// </summary>
    public Texture Texture => _texture;

    /// <summary>
    /// Cuts all icons from the large texture and returns them as an array of sprites.
    /// </summary>
    /// <param name="iconsPerRow">The number of icons per row.</param>
    /// <param name="iconsPerColumn">The number of icons per column.</param>
    /// <returns>A task representing the asynchronous operation, with a result of an array of sprites.</returns>
    /// <remarks>
    /// This method uses asynchronous tasks to cut icons in parallel for better performance on large textures.
    /// </remarks>
    public async Task<Sprite[]> CutAllIconsAsync(int iconsPerRow, int iconsPerColumn)
    {
        var totalIcons = iconsPerRow * iconsPerColumn;
        var icons = new Sprite[totalIcons];

        var tasks = new List<Task>();

        // Asynchronously cut all icons using parallelization for performance
        for (int row = 0; row < iconsPerColumn; row++)
        {
            for (int col = 0; col < iconsPerRow; col++)
            {
                int x = col * _iconWidth;
                int y = row * _iconHeight;

                int index = row * iconsPerRow + col;
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
    /// <returns>A sprite representing the icon at the specified index.</returns>
    public Sprite CutIconAt(int index, int iconsPerRow)
    {
        int row = index / iconsPerRow;
        int col = index % iconsPerRow;
        return CreateIcon(col * _iconWidth, row * _iconHeight);
    }

    /// <summary>
    /// Gets the rectangle representing the icon's area in the texture at the specified column and row.
    /// </summary>
    /// <param name="column">The column of the icon.</param>
    /// <param name="row">The row of the icon.</param>
    /// <returns>An <see cref="IntRect"/> representing the icon's rectangle.</returns>
    public IntRect GetRectAt(int column, int row)
        => new(column * _iconWidth, row * _iconHeight, _iconWidth, _iconHeight);

    /// <summary>
    /// Creates a sprite from a specific section of the texture based on x and y coordinates.
    /// </summary>
    /// <param name="x">The x-coordinate of the top-left corner of the icon.</param>
    /// <param name="y">The y-coordinate of the top-left corner of the icon.</param>
    /// <returns>A sprite representing the icon at the given coordinates.</returns>
    private Sprite CreateIcon(int x, int y)
    {
        IntRect iconRect = new(x, y, _iconWidth, _iconHeight);
        return new Sprite(_texture, iconRect);
    }

    /// <summary>
    /// Rotates the given sprite by a specified angle.
    /// </summary>
    /// <param name="icon">The sprite to be rotated.</param>
    /// <param name="angle">The angle of rotation in degrees.</param>
    /// <returns>The rotated sprite.</returns>
    public static Sprite RotateIcon(Sprite icon, float angle)
    {
        icon.Rotation = angle;
        return icon;
    }

    /// <summary>
    /// Cuts a custom region from the texture based on a specific rectangular area.
    /// </summary>
    /// <param name="region">The <see cref="IntRect"/> defining the region to be cut from the texture.</param>
    /// <returns>A sprite representing the custom region.</returns>
    /// <exception cref="ArgumentException">Thrown if the region exceeds the bounds of the texture.</exception>
    public Sprite CutCustomRegion(IntRect region)
    {
        if (region.Left < 0 || region.Top < 0 || region.Left + region.Width > _texture.Size.X || region.Top + region.Height > _texture.Size.Y)
        {
            throw new ArgumentException("The region is out of bounds of the texture.");
        }

        return new Sprite(_texture, region);
    }
}
