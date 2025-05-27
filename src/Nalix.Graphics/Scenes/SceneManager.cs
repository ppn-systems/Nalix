using Nalix.Graphics.Rendering.Object;
using Nalix.Logging.Extensions;
using System.Linq;

namespace Nalix.Graphics.Scenes;

/// <summary>
/// The SceneManager class is responsible for managing scenes and objects within those scenes.
/// It handles scene transitions, object spawning, and object destruction.
/// </summary>
public static class SceneManager
{
    /// <summary>
    /// This event is invoked at the beginning of the next frame after all non-persisting objects have been queued to be destroyed
    /// and after the new objects have been queued to spawn, but before they are initialized.
    /// </summary>
    public static event System.Action<System.String, System.String> SceneChanged;

    private static Scene _currentScene;
    private static System.String _nextScene = "";

    private static readonly System.Collections.Generic.List<Scene> _scenes = [];
    private static readonly System.Collections.Generic.HashSet<SceneObject> _sceneObjects = [];
    private static readonly System.Collections.Generic.HashSet<SceneObject> _spawnQueue = [];
    private static readonly System.Collections.Generic.HashSet<SceneObject> _destroyQueue = [];

    /// <summary>
    /// Retrieves all objects in the scene of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of objects to retrieve.</typeparam>
    /// <returns>ScreenSize HashSet of all objects of the specified type.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Collections.Generic.HashSet<T> AllObjects<T>()
        where T : SceneObject
        => [.. _sceneObjects.OfType<T>()];

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.Boolean InDestroyQueue(this SceneObject o)
        => _destroyQueue.Contains(o);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.Boolean InSpawnQueue(this SceneObject o)
        => _spawnQueue.Contains(o);

    /// <summary>
    /// Creates instances of all classes inheriting from Scenes in the specified namespace.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' " +
        "require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. " +
        "The return value of the source method does not have matching annotations.", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void Instantiate()
    {
        // Get the types from the entry assembly that match the scene namespace
        System.Collections.Generic.IEnumerable<System.Type> sceneTypes = System.Reflection.Assembly
            .GetEntryAssembly()!
            .GetTypes()
            .Where(t => t.Namespace != null &&
                        t.Namespace.Contains(GameEngine.GraphicsConfig.ScenesNamespace));

        // HashSet to check for duplicate scene names efficiently
        System.Collections.Generic.HashSet<string> sceneNames = [];

        foreach (System.Type type in sceneTypes)
        {
            // Skip compiler-generated types (like anonymous types or internal generic types)
            if (type.Name.Contains('<')) continue;

            // Check if the class has the IgnoredLoadAttribute
            if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<IgnoredLoadAttribute>(type) != null)
            {
                NLogixFx.Warn(
                    source: type.Name,
                    message: $"Skipping load of scene {type.Name} because it is marked as not loadable."
                );
                continue;
            }

            // Attempt to find a constructor with no parameters
            System.Reflection.ConstructorInfo constructor = type.GetConstructor(System.Type.EmptyTypes);
            if (constructor == null) continue;

            // Instantiate the scene using the parameterless constructor
            Scene scene;
            try
            {
                scene = (Scene)constructor.Invoke(null);
            }
            catch (System.Exception ex)
            {
                // Handle any exceptions that occur during instantiation
                NLogixFx.Error(
                    ex,
                    source: type.Name,
                    message: $"Error instantiating scene {type.Name}: {ex.Message}"
                );
                continue;
            }

            // Check for duplicate scene names
            if (sceneNames.Contains(scene.Name))
            {
                throw new System.Exception($"Scenes with name {scene.Name} already exists.");
            }

            // Add the scene name to the HashSet for future checks
            sceneNames.Add(scene.Name);

            // Add the scene to the list
            _scenes.Add(scene);
        }

        // Switch to the main scene defined in the config
        ChangeScene(GameEngine.GraphicsConfig.MainScene);
    }

    /// <summary>
    /// Queues a scene to be loaded on the next frame.
    /// </summary>
    /// <param name="name">The name of the scene to be loaded.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void ChangeScene(System.String name) => _nextScene = name;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ClearScene()
    {
        foreach (SceneObject sceneObject in _sceneObjects)
        {
            if (!sceneObject.PersistOnSceneChange) sceneObject.BeforeDestroy();
        }
        _sceneObjects.RemoveWhere(o => !o.PersistOnSceneChange);

        foreach (SceneObject queued in _spawnQueue)
        {
            if (!queued.PersistOnSceneChange) queued.BeforeDestroy();
        }
        _spawnQueue.RemoveWhere(o => !o.PersistOnSceneChange);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void LoadScene(System.String name)
    {
        _currentScene = _scenes.First(scene => scene.Name == name);
        _currentScene.CreateScene();
        QueueSpawn(_currentScene.GetObjects());
    }

    /// <summary>
    /// Queues a single object to be spawned in the scene.
    /// </summary>
    /// <param name="o">The object to be spawned.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void QueueSpawn(SceneObject o)
    {
        if (o.Initialized)
        {
            throw new System.Exception($"Instance of SceneObject {nameof(o)} already exists in Scenes");
        }
        if (!_spawnQueue.Add(o))
        {
            NLogixFx.Warn($"Instance of SceneObject {nameof(o)} is already queued to be spawned.");
        }
    }

    /// <summary>
    /// Queues a collection of objects to be spawned in the scene.
    /// </summary>
    /// <param name="sceneObjects">The collection of objects to be spawned.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void QueueSpawn(
        System.Collections.Generic.IEnumerable<SceneObject> sceneObjects)
    {
        foreach (SceneObject o in sceneObjects)
        {
            QueueSpawn(o);
        }
    }

    /// <summary>
    /// Queues an object to be destroyed in the scene.
    /// </summary>
    /// <param name="o">The object to be destroyed.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void QueueDestroy(SceneObject o)
    {
        if (!_sceneObjects.Contains(o) && !_spawnQueue.Contains(o))
        {
            throw new System.Exception("Instance of SceneObject does not exist in the scene.");
        }
        if (_spawnQueue.Remove(o)) { }
        else if (!_destroyQueue.Add(o))
        {
            NLogixFx.Warn("Instance of SceneObject is already queued to be destroyed.");
        }
    }

    /// <summary>
    /// Queues a collection of objects to be destroyed in the scene.
    /// </summary>
    /// <param name="sceneObjects">The collection of objects to be destroyed.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void QueueDestroy(
        System.Collections.Generic.IEnumerable<SceneObject> sceneObjects)
    {
        foreach (SceneObject o in sceneObjects)
        {
            QueueDestroy(o);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void ProcessLoadScene()
    {
        if (_nextScene == "") return;
        ClearScene();
        string lastScene = _currentScene?.Name ?? "";
        LoadScene(_nextScene);
        SceneChanged?.Invoke(lastScene, _nextScene);
        _nextScene = "";
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void ProcessDestroyQueue()
    {
        foreach (SceneObject o in _destroyQueue)
        {
            if (!_sceneObjects.Remove(o))
            {
                NLogixFx.Warn("Instance of SceneObject to be destroyed does not exist in scene");
                continue;
            }
            o.BeforeDestroy();
        }
        _destroyQueue.Clear();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void ProcessSpawnQueue()
    {
        foreach (SceneObject q in _spawnQueue)
        {
            if (!_sceneObjects.Add(q))
            {
                throw new System.Exception("Instance of queued SceneObject already exists in scene.");
            }
        }

        _spawnQueue.Clear();

        foreach (SceneObject o in _sceneObjects)
        {
            if (!o.Initialized) o.InitializeSceneObject();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void UpdateSceneObjects(float deltaTime)
    {
        foreach (SceneObject o in _sceneObjects)
        {
            if (o.Enabled) o.Update(deltaTime);
        }
    }

    /// <summary>
    /// Finds the first object of a specific type in the scene.
    /// </summary>
    /// <typeparam name="T">The type of object to find.</typeparam>
    /// <returns>The first object of the specified type, or null if none exist.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static T FindByType<T>() where T : SceneObject
    {
        System.Collections.Generic.HashSet<T> objects = AllObjects<T>();
        if (objects.Count != 0)
        {
            return objects.First();
        }

        return null;
    }
}
