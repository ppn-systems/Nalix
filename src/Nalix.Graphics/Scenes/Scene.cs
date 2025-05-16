using Nalix.Graphics.Rendering.Object;
using System.Collections.Generic;

namespace Nalix.Graphics.Scenes;

/// <summary>
/// Represents a base class for a scene in the game.
/// This class provides methods to manage initial scene objects and ensures that derived scenes implement object loading.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Scene"/> class with a specified name.
/// </remarks>
/// <param name="name">The name of the scene.</param>
public abstract class Scene(string name)
{
    /// <summary>
    /// Gets the name of the scene.
    /// </summary>
    public readonly string Name = name;

    private readonly List<SceneObject> _objects = [];

    /// <summary>
    /// Retrieves the list of initial objects in the scene.
    /// </summary>
    /// <returns>ScreenSize list of <see cref="SceneObject"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public List<SceneObject> GetObjects() => _objects;

    /// <summary>
    /// Adds an object to the list of initial objects in the scene.
    /// </summary>
    /// <param name="o">The <see cref="SceneObject"/> to add.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected void AddObject(SceneObject o) => _objects.Add(o);

    /// <summary>
    /// Creates the scene by clearing and loading its objects.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void CreateScene()
    {
        this.ClearObjects();
        this.LoadObjects();
    }

    /// <summary>
    /// An abstract method that must be implemented by derived scenes to load their specific objects.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected abstract void LoadObjects();

    /// <summary>
    /// Clears all objects from the initial objects list.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ClearObjects() => _objects.Clear();
}
