using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace Nalix.Graphics;

/// <summary>
/// The InputState class provides functionality for handling keyboard and mouse input using SFML.Window.
/// </summary>
public class InputState
{
    /// <summary>
    /// ScreenSize dictionary that stores the current state of each keyboard key.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<Keyboard.Key, System.Boolean> KeyState;

    /// <summary>
    /// ScreenSize dictionary that stores the previous state of each keyboard key.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<Keyboard.Key, System.Boolean> PreviousKeyState;

    /// <summary>
    /// ScreenSize dictionary that stores the current state of each mouse button.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<Mouse.Button, System.Boolean> MouseButtonState;

    /// <summary>
    /// ScreenSize dictionary that stores the previous state of each mouse button.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<Mouse.Button, System.Boolean> PreviousMouseButtonState;

    /// <summary>
    /// ScreenSize tuple that stores the current position of the mouse (X, Y).
    /// </summary>
    private static Vector2i _mousePosition;

    static InputState()
    {
        KeyState = [];
        PreviousKeyState = [];
        MouseButtonState = [];
        PreviousMouseButtonState = [];
    }

    /// <summary>
    /// Updates the state of all keys and the mouse position. Should be called once per frame.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Update(RenderWindow window)
    {
        // Update the state of each key
        foreach (Keyboard.Key key in System.Enum.GetValues<Keyboard.Key>())
        {
            PreviousKeyState[key] = KeyState.TryGetValue(key, out System.Boolean value) && value;
            KeyState[key] = Keyboard.IsKeyPressed(key);
        }

        // Update mouse button states
        foreach (Mouse.Button button in System.Enum.GetValues<Mouse.Button>())
        {
            PreviousMouseButtonState[button] = MouseButtonState.TryGetValue(button, out System.Boolean value) && value;
            MouseButtonState[button] = Mouse.IsButtonPressed(button);
        }

        // Update mouse position
        _mousePosition = Mouse.GetPosition(window);
    }

    #region Keyboard

    /// <summary>
    /// Checks if a key is currently being pressed.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is currently down; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsKeyDown(Keyboard.Key key)
        => KeyState.ContainsKey(key) && KeyState[key];

    /// <summary>
    /// Checks if a key is currently not being pressed.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is currently up; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsKeyUp(Keyboard.Key key)
        => !KeyState.ContainsKey(key) || !KeyState[key];

    /// <summary>
    /// Checks if a key was pressed for the first time this frame.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was pressed this frame; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsKeyPressed(Keyboard.Key key)
        => IsKeyDown(key) && !PreviousKeyState.ContainsKey(key);

    /// <summary>
    /// Checks if a key was released for the first time this frame.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was released this frame; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsKeyReleased(Keyboard.Key key)
        => !IsKeyDown(key) && PreviousKeyState.ContainsKey(key);

    #endregion Keyboard

    #region Mouse

    /// <summary>
    /// Gets the current position of the mouse.
    /// </summary>
    /// <returns>ScreenSize tuple containing the X and Y position of the mouse.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetMousePosition() => _mousePosition;

    /// <summary>
    /// Determines whether the specified mouse button is currently being held down.
    /// </summary>
    /// <param name="button">The mouse button to check (e.g., Left, Right).</param>
    /// <returns>True if the mouse button is currently down; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsMouseButtonDown(Mouse.Button button)
        => MouseButtonState.ContainsKey(button) && MouseButtonState[button];

    /// <summary>
    /// Determines whether the specified mouse button was just pressed during this frame.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>
    /// True if the button is down now but was not down in the previous frame;
    /// otherwise, false.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsMouseButtonPressed(Mouse.Button button)
        => IsMouseButtonDown(button) &&
           (!PreviousMouseButtonState.ContainsKey(button) ||
           !PreviousMouseButtonState[button]);

    /// <summary>
    /// Determines whether the specified mouse button was just released during this frame.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>
    /// True if the button was down in the previous frame but is not down now;
    /// otherwise, false.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsMouseButtonReleased(Mouse.Button button)
        => !IsMouseButtonDown(button) &&
           PreviousMouseButtonState.ContainsKey(button) &&
           PreviousMouseButtonState[button];

    /// <summary>
    /// Gets the current position of the mouse.
    /// </summary>
    /// <returns>ScreenSize tuple containing the X and Y position of the mouse.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static (System.Single X, System.Single Y) GetMousePositionTuple() => new(_mousePosition.X, _mousePosition.Y);

    #endregion Mouse
}
