using Nalix.Graphics.Rendering.Object;

namespace Nalix.Graphics.Scenes;

/// <summary>
/// Represents a sealed class that stores information about a scene change and persists across scene transitions.
/// </summary>
/// <typeparam name="T">The type of information stored in the scene change info.</typeparam>
public sealed class SceneChangeInfo<T> : SceneObject
{
    private System.Boolean _sceneChanged;
    private readonly T _info;

    /// <summary>
    /// Gets the name associated with this scene change info.
    /// </summary>
    public System.String Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SceneChangeInfo{T}"/> class with the specified information and name.
    /// </summary>
    /// <param name="info">The information to store.</param>
    /// <param name="name">The name associated with this scene change info.</param>
    public SceneChangeInfo(T info, System.String name)
    {
        _info = info;
        Name = name;
        PersistOnSceneChange = true;
    }

    /// <summary>
    /// Extracts the stored information.
    /// </summary>
    /// <returns>The information of type <typeparamref name="T"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T Extract() => _info;

    /// <summary>
    /// Initializes the scene change info and subscribes to the SceneChanged event.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected override void Initialize() => SceneManager.SceneChanged += OnSceneChange;

    /// <summary>
    /// Cleans up before the object is destroyed and unsubscribes from the SceneChanged event.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override void BeforeDestroy() => SceneManager.SceneChanged -= OnSceneChange;

    /// <summary>
    /// Handles the scene change event by setting the <see cref="_sceneChanged"/> flag.
    /// </summary>
    /// <param name="lastScene">The name of the last scene.</param>
    /// <param name="nextScene">The name of the next scene.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnSceneChange(System.String lastScene, System.String nextScene) => _sceneChanged = true;

    /// <summary>
    /// Updates the state of the SceneChangeInfo object and destroys it after a scene change.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since the last update in seconds.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override void Update(System.Single deltaTime)
    {
        // Destroy this instance on the first frame after a new scene has been loaded
        if (_sceneChanged)
        {
            Destroy();
        }
    }

    /// <summary>
    /// Retrieves a SceneChangeInfo object by name, or returns a default value if not found.
    /// </summary>
    /// <param name="name">The name of the scene change info to look for.</param>
    /// <param name="defaultValue">The default value to return if no matching object is found.</param>
    /// <returns>The stored information of type <typeparamref name="T"/> or the default value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static T Catch(System.String name, T defaultValue)
    {
        SceneChangeInfo<T> info = SceneManager.FindByType<SceneChangeInfo<T>>();
        if (info == null)
        {
            return defaultValue;
        }

        return info.Name != name ? defaultValue : info.Extract();
    }
}
