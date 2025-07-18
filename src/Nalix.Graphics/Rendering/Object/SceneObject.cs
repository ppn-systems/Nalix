using Nalix.Graphics.Scenes;

namespace Nalix.Graphics.Rendering.Object;

/// <summary>
/// Represents a base class for all scene objects in the game.
/// This class provides lifecycle management, tagging, and utility methods for objects within a scene.
/// </summary>
public abstract class SceneObject
{
    private readonly System.Collections.Generic.HashSet<System.String> _tags = [];

    /// <summary>
    /// Indicates whether the object is paused.
    /// </summary>
    public System.Boolean Paused { get; private set; }

    /// <summary>
    /// Indicates whether the object is enabled and active.
    /// </summary>
    public System.Boolean Enabled { get; private set; }

    /// <summary>
    /// Indicates whether the object has been initialized.
    /// </summary>
    public System.Boolean Initialized { get; private set; }

    /// <summary>
    /// Determines whether the object persists on a scene change. Default is false.
    /// </summary>
    public System.Boolean PersistOnSceneChange { get; protected set; } = false;

    /// <summary>
    /// Called during the initialization phase for additional setup logic.
    /// Override this in derived classes to add custom initialization logic.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    protected virtual void Initialize()
    { }

    /// <summary>
    /// Initializes the scene object. This is called internally by the scene manager.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void InitializeSceneObject()
    {
        Initialize();
        Initialized = true;
        Enabled = true;
    }

    /// <summary>
    /// Invoked just before the object is destroyed. Override this to add custom cleanup logic.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual void BeforeDestroy()
    { }

    /// <summary>
    /// Updates the state of the object. Override this method to add custom update logic.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since the last update in seconds.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public virtual void Update(System.Single deltaTime)
    { }

    /// <summary>
    /// Adds a tag to the object.
    /// </summary>
    /// <param name="tag">The tag to add.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void AddTag(System.String tag) => _tags.Add(tag);

    /// <summary>
    /// Checks if the object has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>True if the object has the tag; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean HasTag(System.String tag) => _tags.Contains(tag);

    /// <summary>
    /// Pauses the object, preventing it from updating.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Pause() => Paused = true;

    /// <summary>
    /// Unpauses the object, allowing it to update again.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Unpause() => Paused = false;

    /// <summary>
    /// Enables the object, activating its behavior.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Enable() => Enabled = true;

    /// <summary>
    /// Disables the object, deactivating its behavior.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Disable() => Enabled = false;

    /// <summary>
    /// Checks if the object is queued to be destroyed.
    /// </summary>
    /// <returns>True if the object is queued for destruction; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean ToBeDestroyed() => this.InDestroyQueue();

    /// <summary>
    /// Checks if the object is queued to be spawned.
    /// </summary>
    /// <returns>True if the object is queued for spawning; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean ToBeSpawned() => this.InSpawnQueue();

    /// <summary>
    /// Queues the object to be spawned in the scene.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Spawn() => SceneManager.QueueSpawn(this);

    /// <summary>
    /// Queues the object to be destroyed in the scene.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Destroy() => SceneManager.QueueDestroy(this);
}
