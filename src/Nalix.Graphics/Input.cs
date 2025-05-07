using SFML.Window;
using System;
using System.Collections.Generic;

namespace Nalix.Graphics;

/// <summary>
/// The Input class provides functionality for handling keyboard and mouse input using SFML.Window.
/// </summary>
public class Input
{
    /// <summary>
    /// A dictionary that stores the current state of each keyboard key.
    /// </summary>
    private static readonly Dictionary<Keyboard.Key, bool> KeyState = [];

    /// <summary>
    /// A dictionary that stores the previous state of each keyboard key.
    /// </summary>
    private static readonly Dictionary<Keyboard.Key, bool> PreviousKeyState = [];

    /// <summary>
    /// A tuple that stores the current position of the mouse (X, Y).
    /// </summary>
    private static (float X, float Y) _mousePosition;

    /// <summary>
    /// Updates the state of all keys and the mouse position. Should be called once per frame.
    /// </summary>
    public static void Update()
    {
        // Update the state of each key
        foreach (Keyboard.Key key in Enum.GetValues<Keyboard.Key>())
        {
            PreviousKeyState[key] = KeyState.TryGetValue(key, out bool value) && value;
            KeyState[key] = Keyboard.IsKeyPressed(key);
        }

        // Update mouse position
        _mousePosition = (Mouse.GetPosition().X, Mouse.GetPosition().Y);
    }

    /// <summary>
    /// Checks if a key is currently being pressed.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is currently down; otherwise, false.</returns>
    public static bool IsKeyDown(Keyboard.Key key) => KeyState.ContainsKey(key) && KeyState[key];

    /// <summary>
    /// Checks if a key is currently not being pressed.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is currently up; otherwise, false.</returns>
    public static bool IsKeyUp(Keyboard.Key key) => !KeyState.ContainsKey(key) || !KeyState[key];

    /// <summary>
    /// Checks if a key was pressed for the first time this frame.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was pressed this frame; otherwise, false.</returns>
    public static bool IsKeyPressed(Keyboard.Key key) => IsKeyDown(key) && !PreviousKeyState.ContainsKey(key);

    /// <summary>
    /// Checks if a key was released for the first time this frame.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was released this frame; otherwise, false.</returns>
    public static bool IsKeyReleased(Keyboard.Key key) => !IsKeyDown(key) && PreviousKeyState.ContainsKey(key);

    /// <summary>
    /// Gets the current position of the mouse.
    /// </summary>
    /// <returns>A tuple containing the X and Y position of the mouse.</returns>
    public static (float X, float Y) GetMousePosition() => _mousePosition;
}
