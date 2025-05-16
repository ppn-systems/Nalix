using Nalix.Logging.Extensions;
using SFML.Audio;
using SFML.System;
using System;
using System.Collections.Generic;

namespace Nalix.Graphics.Assets.Manager;

/// <summary>
/// Simplifies access and management of sound effects
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SfxManager" /> class.
/// </remarks>
/// <param name="loader">Resolution <see cref="SfxLoader" /> instance to load the required sound effects</param>
/// <param name="volume">Resolution function that returns the current volume.</param>
/// <exception cref="ArgumentNullException">loader</exception>
public class SfxManager(SfxLoader loader, Func<int> volume)
{
    private readonly SfxLoader _Loader = loader ?? throw new ArgumentNullException(nameof(loader));

    private readonly Dictionary<String, SoundManager> _SoundLibrary = [];
    private readonly Func<int> _ReadVolume = volume;

    /// <summary>
    /// Gets or sets the global listener position for spatial sounds.
    /// </summary>
    public static Vector2f ListenerPosition
    {
        get => new(Listener.Position.X, Listener.Position.Y);
        set => Listener.Position = new Vector3f(value.X, value.Y, Listener.Position.Z);
    }

    /// <summary>
    /// Gets or sets the initial volume drop-off start distance for spatial sounds.
    /// This defines the maximum distance a sound can still be heard at max volume.
    /// </summary>
    public float VolumeDropoffStartDistance { get; set; } = 500;

    /// <summary>
    /// Gets or sets the initial volume drop-off factor for spatial sounds.
    /// Defines how fast the volume drops beyond the <see cref="VolumeDropoffStartDistance"/>
    /// </summary>
    public float VolumeDropoffFactor { get; set; } = 10;

    /// <summary>
    /// Loads all compatible files from a folder into the sound library.
    /// </summary>
    /// <param name="root">Optional: root folder path when different from the default asset root.</param>
    /// <param name="parallelSounds">The amount of times each sound can be played in parallel.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void LoadFromDirectory(string root = null, int parallelSounds = 2)
    {
        ObjectDisposedException.ThrowIf(_Loader.Disposed, nameof(_Loader));

        var oldRoot = _Loader.RootFolder;
        if (root != null) _Loader.RootFolder = root;
        var sounds = _Loader.LoadAllFilesInDirectory();
        NLogixFx.Debug($"[SfxManager] Loaded {sounds.Length} sound files from '{_Loader.RootFolder}'");
        _Loader.RootFolder = oldRoot;

        LoadFromFileList(sounds, parallelSounds);
    }

    /// <summary>
    /// Loads the specified files into the sound library.
    /// </summary>
    /// <param name="files">The files to load.</param>
    /// <param name="parallelSounds">The amount of times each sound can be played in parallel.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void LoadFromFileList(IEnumerable<String> files, int parallelSounds)
    {
        foreach (var file in files) AddToLibrary(file, parallelSounds);
    }

    /// <summary>
    /// Loads a new entry into the sound library.
    /// </summary>
    /// <param name="name">The name of the sound effect.</param>
    /// <param name="parallelSounds">The amount of times this sound can be played in parallel.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void AddToLibrary(String name, int parallelSounds)
    {
        ObjectDisposedException.ThrowIf(_Loader.Disposed, nameof(_Loader));

        if (_SoundLibrary.TryGetValue(name, out SoundManager sound))
        {
            // Replace SoundManager
            _SoundLibrary.Remove(name);
            sound.Dispose();
            AddToLibrary(name, parallelSounds);
        }
        else
        {
            // Add new SoundManager
            sound = new SoundManager(name, _Loader.Load(name), parallelSounds);
            _SoundLibrary.Add(name, sound);
        }
    }

    /// <summary>
    /// Retrieves a sound effect from the sound library if it is currently available.
    /// </summary>
    /// <param name="name">The name of the sound effect to retrieve.</param>
    /// <param name="spatial">Resolution boolean value that determines if the sound is spatial (3D) or not.
    /// If true, the sound will be spatialized with distance attenuation. If false, the sound is 2D and will play relative to the listener.</param>
    /// <returns>Resolution <see cref="Sound"/> instance if available, otherwise throws an <see cref="ArgumentException"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the loader has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown if no sound is found with the specified name.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Sound GetSound(String name, bool spatial = false)
    {
        ObjectDisposedException.ThrowIf(_Loader.Disposed, nameof(_Loader));

        if (_SoundLibrary.TryGetValue(name, out SoundManager SoundManager))
        {
            var sound = SoundManager.GetSound();
            if (sound != null)
            {
                sound.Volume = _ReadVolume.Invoke();
                sound.RelativeToListener = !spatial;
                if (spatial)
                {
                    sound.MinDistance = VolumeDropoffStartDistance;
                    sound.Attenuation = VolumeDropoffFactor;
                }
            }
            return sound;
        }

        throw new ArgumentException($"There is no sound named '{name}'");
    }

    /// <summary>
    /// Plays a sound effect when currently available.
    /// </summary>
    /// <param name="name">The name of the sound effect.</param>
    /// <param name="position">Optional position of the sound. Only relevant when sound is supposed to be spatial.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Play(String name, Vector2f? position = null)
    {
        ObjectDisposedException.ThrowIf(_Loader.Disposed, nameof(_Loader));

        var sound = GetSound(name, position.HasValue);
        if (sound == null)
            return;

        // If position is specified, set it in 3D space
        if (position.HasValue)
            sound.Position = new Vector3f(position.Value.X, position.Value.Y, 0f); // Assuming z = 0 for 2D position.

        sound.Play();
    }
}
