using Nalix.Graphics.Scenes;
using System.Collections.Generic;

namespace Nalix.Graphics.Rendering.Object;

/// <summary>
/// Represents a base class for all scene objects in the game.
/// This class provides lifecycle management, tagging, and utility methods for objects within a scene.
/// </summary>
public abstract class SceneObject
{
    private readonly HashSet<string> _tags = [];

    /// <summary>
    /// Determines whether the object persists on a scene change. Default is false.
    /// </summary>
    public bool PersistOnSceneChange { get; protected set; } = false;

    /// <summary>
    /// Indicates whether the object has been initialized.
    /// </summary>
    public bool Initialized { get; private set; }

    /// <summary>
    /// Indicates whether the object is paused.
    /// </summary>
    public bool Paused { get; private set; }

    /// <summary>
    /// Indicates whether the object is enabled and active.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Called during the initialization phase for additional setup logic.
    /// Override this in derived classes to add custom initialization logic.
    /// </summary>
    protected virtual void Initialize()
    { }

    /// <summary>
    /// Initializes the scene object. This is called internally by the scene manager.
    /// </summary>
    internal void InitializeSceneObject()
    {
        Initialize();
        Initialized = true;
        Enabled = true;
    }

    /// <summary>
    /// Invoked just before the object is destroyed. Override this to add custom cleanup logic.
    /// </summary>
    public virtual void BeforeDestroy()
    { }

    /// <summary>
    /// Updates the state of the object. Override this method to add custom update logic.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since the last update in seconds.</param>
    public virtual void Update(float deltaTime)
    { }

    /// <summary>
    /// Adds a tag to the object.
    /// </summary>
    /// <param name="tag">The tag to add.</param>
    public void AddTag(string tag) => _tags.Add(tag);

    /// <summary>
    /// Checks if the object has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>True if the object has the tag; otherwise, false.</returns>
    public bool HasTag(string tag) => _tags.Contains(tag);

    /// <summary>
    /// Pauses the object, preventing it from updating.
    /// </summary>
    public void Pause() => Paused = true;

    /// <summary>
    /// Unpauses the object, allowing it to update again.
    /// </summary>
    public void Unpause() => Paused = false;

    /// <summary>
    /// Enables the object, activating its behavior.
    /// </summary>
    public void Enable() => Enabled = true;

    /// <summary>
    /// Disables the object, deactivating its behavior.
    /// </summary>
    public void Disable() => Enabled = false;

    /// <summary>
    /// Checks if the object is queued to be destroyed.
    /// </summary>
    /// <returns>True if the object is queued for destruction; otherwise, false.</returns>
    public bool ToBeDestroyed() => this.InDestroyQueue();

    /// <summary>
    /// Checks if the object is queued to be spawned.
    /// </summary>
    /// <returns>True if the object is queued for spawning; otherwise, false.</returns>
    public bool ToBeSpawned() => this.InSpawnQueue();

    /// <summary>
    /// Queues the object to be spawned in the scene.
    /// </summary>
    public void Spawn() => SceneManager.QueueSpawn(this);

    /// <summary>
    /// Queues the object to be destroyed in the scene.
    /// </summary>
    public void Destroy() => SceneManager.QueueDestroy(this);
}
