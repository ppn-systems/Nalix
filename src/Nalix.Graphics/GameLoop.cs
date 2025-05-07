using Nalix.Graphics.Render;
using Nalix.Graphics.Scene;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System.Collections.Generic;

namespace Nalix.Graphics;

/// <summary>
/// The Game class serves as the entry point for managing the game window, rendering, and scene updates.
/// </summary>
public static class GameLoop
{
    // Private fields
    private static readonly RenderWindow _window;

    /// <summary>
    /// Indicates whether debugging mode is enabled.
    /// </summary>
    public static bool Debugging { get; private set; }

    /// <summary>
    /// Provides access to the assembly configuration.
    /// </summary>
    public static AssemblyConfig AssemblyConfig { get; private set; }

    /// <summary>
    /// Gets the dimensions (width and height) of the screen or viewport, used to set the screen size for rendering purposes.
    /// </summary>
    public static Vector2u ScreenSize { get; private set; }

    /// <summary>
    /// Static constructor to initialize the game configuration and window.
    /// </summary>
    static GameLoop()
    {
        AssemblyConfig = new AssemblyConfig();
        ScreenSize = new Vector2u(AssemblyConfig.ScreenWidth, AssemblyConfig.ScreenHeight);

        _window = new RenderWindow(
            new VideoMode(AssemblyConfig.ScreenWidth, AssemblyConfig.ScreenHeight),
            AssemblyConfig.Title, Styles.Titlebar | Styles.Close
        );
        _window.Closed += (_, _) => _window.Close();
        _window.SetFramerateLimit(AssemblyConfig.FrameLimit);
    }

    /// <summary>
    /// Enables or disables debug mode.
    /// </summary>
    /// <param name="on">Set to true to enable debug mode, false to disable it.</param>
    public static void SetDebugMode(bool on) => Debugging = on;

    /// <summary>
    /// Opens the game window and starts the main game loop.
    /// </summary>
    public static void OpenWindow()
    {
        Clock clock = new();
        SceneManager.Instantiate();

        while (_window.IsOpen)
        {
            _window.DispatchEvents();
            float deltaTime = clock.Restart().AsSeconds();
            Update(deltaTime);

            _window.Clear();
            Render(_window);

            _window.Display();
        }

        _window.Dispose();
    }

    /// <summary>
    /// Updates all game components, including input, scene management, and scene objects.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since the last update, in seconds.</param>
    private static void Update(float deltaTime)
    {
        Input.Update(_window);
        SceneManager.ProcessLoadScene();
        SceneManager.ProcessDestroyQueue();
        SceneManager.ProcessSpawnQueue();
        SceneManager.UpdateSceneObjects(deltaTime);
    }

    /// <summary>
    /// Renders all objects in the current scene, sorted by their Z-index.
    /// </summary>
    /// <param name="target">The render target.</param>
    private static void Render(RenderTarget target)
    {
        List<RenderObject> renderObjects = [.. SceneManager.AllObjects<RenderObject>()];
        renderObjects.Sort(RenderObject.CompareByZIndex);
        foreach (RenderObject r in renderObjects)
        {
            if (r.Enabled || !r.Visible) r.Render(target);
        }
    }
}
