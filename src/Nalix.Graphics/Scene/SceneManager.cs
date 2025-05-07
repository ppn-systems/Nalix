using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Nalix.Graphics.Scene;

public static class SceneManager
{
    /// <summary>
    /// Is invoked at the beginning of the next frame, after all non-persisting objects have been queued to be destroyed
    /// and after the new objects have been queued to spawn, but before they are initialized.
    /// </summary>
    public static event Action<string, string>? SceneChanged;

    private static string _nextScene = "";
    private static Scene? _currentScene;
    private static readonly List<Scene> _scenes = new();

    private static readonly HashSet<SceneObject> _sceneObjects = new();
    private static readonly HashSet<SceneObject> _spawnQueue = new();
    private static readonly HashSet<SceneObject> _destroyQueue = new();

    public static HashSet<T> AllObjects<T>() where T : SceneObject
    {
        return _sceneObjects.OfType<T>().ToHashSet();
    }

    internal static bool InDestroyQueue(this SceneObject o)
    {
        return _destroyQueue.Contains(o);
    }

    internal static bool InSpawnQueue(this SceneObject o)
    {
        return _spawnQueue.Contains(o);
    }

    // create one instance of all classes inheriting Scene in namespace ScenesNamespace
    internal static void Instantiate()
    {
        // get all types in assembly
        IEnumerable<Type> sceneTypes = Assembly.GetEntryAssembly()!.GetTypes()
                                               .Where(t => t.Namespace != null &&
                                                           t.Namespace.Contains(Game.ProjectSettings.ScenesNamespace));
        // dynamically call each constructor
        foreach (Type type in sceneTypes)
        {
            // <>c appears when lambda expressions exist in the class definition
            // it crashes if we try to call it's constructor so we ignore it
            if (type.Name.Contains('<')) continue;

            // get parameterless constructor and invoke it
            Scene? scene = (Scene?)type.GetConstructor(Type.EmptyTypes)?.Invoke([]);
            if (scene == null) continue;

            bool sceneNameExists = _scenes.Select(s => s.Name).Contains(scene.Name);
            if (sceneNameExists)
            { // scenes may not have dulicate names
                throw new Exception($"Scene with name {scene.Name} already exists.");
            }
            _scenes.Add(scene);
        }
        ChangeScene(Game.ProjectSettings.MainScene);
    }

    public static void ChangeScene(string name)
    {
        _nextScene = name;
    }

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

        AssetManager.UnloadAssets();
    }

    private static void LoadScene(string name)
    {
        _currentScene = _scenes.First(scene => scene.Name == name);
        _currentScene.CreateScene();
        QueueSpawn(_currentScene.GetInitialObjects());
    }

    public static void QueueSpawn(SceneObject o)
    {
        if (o.Initialized)
        {
            throw new Exception($"Instance of SceneObject {nameof(o)} already exists in Scene");
        }
        if (!_spawnQueue.Add(o))
        {
            Debug.Warning($"Instance of SceneObject {nameof(o)} is already queued to be spawned.");
        }
    }

    public static void QueueSpawn(IEnumerable<SceneObject> sceneObjects)
    {
        foreach (SceneObject o in sceneObjects)
        {
            QueueSpawn(o);
        }
    }

    public static void QueueDestroy(SceneObject o)
    {
        if (!_sceneObjects.Contains(o) && !_spawnQueue.Contains(o))
        { // destroyed before it's ever been spawned
            throw new Exception("Instance of SceneObject does not exist in the scene.");
        }
        if (_spawnQueue.Remove(o)) { } // false if it is not queued to spawn
        else if (!_destroyQueue.Add(o))
        { // try to add to destroy queue
            Debug.Warning("Instance of SceneObject is already queued to be destroyed.");
        }
    }

    public static void QueueDestroy(IEnumerable<SceneObject> sceneObjects)
    {
        foreach (SceneObject o in sceneObjects)
        {
            QueueDestroy(o);
        }
    }

    internal static void ProcessLoadScene()
    {
        if (_nextScene == "") return;
        ClearScene();
        string lastScene = _currentScene?.Name ?? "";
        LoadScene(_nextScene);
        SceneChanged?.Invoke(lastScene, _nextScene);
        _nextScene = "";
    }

    internal static void ProcessDestroyQueue()
    {
        foreach (SceneObject o in _destroyQueue)
        {
            if (!_sceneObjects.Remove(o))
            {
                Debug.Warning("Instance of SceneObject to be destroyed does not exist in scene");
                continue;
            }
            o.BeforeDestroy();
        }
        _destroyQueue.Clear();
    }

    internal static void ProcessSpawnQueue()
    {
        foreach (SceneObject q in _spawnQueue)
        {
            if (!_sceneObjects.Add(q))
            {
                throw new Exception("Instance of queued SceneObject already exists in scene.");
            }
        }

        _spawnQueue.Clear();

        foreach (SceneObject o in _sceneObjects)
        {
            if (!o.Initialized) o.InitializeSceneObject();
        }
    }

    internal static void UpdateSceneObjects(float deltaTime)
    {
        foreach (SceneObject o in _sceneObjects)
        {
            if (o.Enabled) o.Update(deltaTime);
        }
    }

    // Searching for SceneObjects
    public static T? FindByType<T>() where T : SceneObject
    {
        HashSet<T> objects = AllObjects<T>();
        if (objects.Count != 0)
        {
            return objects.First();
        }
        return null;
    }
}
