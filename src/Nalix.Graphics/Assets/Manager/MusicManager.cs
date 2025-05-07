using SFML.Audio;
using System.Collections.Generic;
using System.IO;

namespace Nalix.Graphics.Assets.Manager;

/// <summary>
/// Manages music playback, caching, and control.
/// </summary>
public static class MusicManager
{
    #region Fields

    private static Music _current;
    private static readonly Dictionary<string, Music> _musicCache = [];

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a value indicating whether music is currently playing.
    /// </summary>
    public static bool IsPlaying => _current?.Status == SoundStatus.Playing;

    /// <summary>
    /// Gets a value indicating whether music is currently paused.
    /// </summary>
    public static bool IsPaused => _current?.Status == SoundStatus.Paused;

    #endregion Properties

    #region Methods

    /// <summary>
    /// Plays music from a file, with optional looping.
    /// </summary>
    /// <param name="filename">The path to the music file.</param>
    /// <param name="loop">Determines whether the music should loop.</param>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    public static void Play(string filename, bool loop = true)
    {
        Stop(); // Stop current before playing new

        if (!_musicCache.TryGetValue(filename, out var music))
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException($"Music file not found: {filename}");

            music = new Music(filename);
            _musicCache[filename] = music;
        }

        _current = music;
        _current.Loop = loop;
        _current.Play();
    }

    /// <summary>
    /// Pauses the currently playing music.
    /// </summary>
    public static void Pause()
    {
        _current?.Pause();
    }

    /// <summary>
    /// Resumes playback if the music is paused.
    /// </summary>
    public static void Resume()
    {
        if (_current?.Status == SoundStatus.Paused)
            _current.Play();
    }

    /// <summary>
    /// Stops the currently playing music and clears the reference.
    /// </summary>
    public static void Stop()
    {
        _current?.Stop();
        _current = null;
    }

    #endregion Methods
}
