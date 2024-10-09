using SFML.Audio;
using System;
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
    private static readonly Dictionary<String, Music> _musicCache = [];

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a value indicating whether music is currently playing.
    /// </summary>
    public static Boolean IsPlaying => _current?.Status == SoundStatus.Playing;

    /// <summary>
    /// Gets a value indicating whether music is currently paused.
    /// </summary>
    public static Boolean IsPaused => _current?.Status == SoundStatus.Paused;

    #endregion Properties

    #region Methods

    /// <summary>
    /// Loads a music file into cache if not already loaded.
    /// </summary>
    /// <param name="filename">The path to the music file.</param>
    /// <exception cref="FileNotFoundException"></exception>
    public static void Load(String filename)
    {
        if (!_musicCache.ContainsKey(filename))
        {
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException($"Music file not found: {filename}");
            }

            var music = new Music(filename);
            _musicCache[filename] = music;
        }
    }

    /// <summary>
    /// Plays a loaded music file. Must be loaded before.
    /// </summary>
    /// <param name="filename">The path to the music file.</param>
    /// <param name="loop">Determines whether the music should loop.</param>
    /// <exception cref="InvalidOperationException">If music not loaded yet.</exception>
    public static void Play(String filename, Boolean loop = true)
    {
        Stop(); // Stop current before playing new

        if (!_musicCache.TryGetValue(filename, out Music music))
        {
            throw new InvalidOperationException($"Music file not loaded: {filename}");
        }

        _current = music;
        _current.Loop = loop;
        _current.Play();
    }

    /// <summary>
    /// Pauses the currently playing music.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Pause() => _current?.Pause();

    /// <summary>
    /// Resumes playback if the music is paused.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Resume()
    {
        if (_current?.Status == SoundStatus.Paused)
        {
            _current.Play();
        }
    }

    /// <summary>
    /// Stops the currently playing music and clears the reference.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Stop()
    {
        _current?.Stop();
        _current = null;
    }

    /// <summary>
    /// Sets the volume of the currently playing music.
    /// </summary>
    /// <param name="volume">The volume level to set, ranging from 0.0 (silent) to 100.0 (full volume).</param>
    public static void SetVolume(Single volume)
    {
        if (_current != null)
        {
            _current.Volume = volume;
        }
    }

    /// <summary>
    /// Clears the music cache by disposing of all cached music instances and removing them from the cache.
    /// </summary>
    public static void ClearCache()
    {
        foreach (Music music in _musicCache.Values)
        {
            music.Dispose();
        }

        _musicCache.Clear();
    }

    /// <summary>
    /// Disposes of the music manager by clearing the music cache and stopping any currently playing music.
    /// </summary>
    public static void Dispose()
    {
        Stop();
        ClearCache();
    }

    #endregion Methods
}
