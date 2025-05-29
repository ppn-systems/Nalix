namespace Nalix.Common.Cryptography.Hashing;

/// <summary>
/// Defines a common interface for SHA-based cryptographic hash functions.
/// </summary>
/// <remarks>
/// Implementations of this interface provide incremental hashing, allowing data
/// to be processed in chunks rather than all at once.
/// </remarks>
public interface ISHA
{
    /// <summary>
    /// Initializes or resets the hashing state.
    /// </summary>
    /// <remarks>
    /// This method should be called before starting a new hash computation.
    /// If an instance is reused, it resets all internal states.
    /// </remarks>
    void Initialize();

    /// <summary>
    /// Computes the hash of the given data in a single operation.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The computed hash as a byte array.</returns>
    /// <remarks>
    /// This is a convenience method that initializes, updates, and finalizes the hash in one call.
    /// </remarks>
    System.Byte[] ComputeHash(System.ReadOnlySpan<System.Byte> data);

    /// <summary>
    /// Processes more data into the hash computation.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <remarks>
    /// This method can be called multiple times with different chunks of data.
    /// </remarks>
    void Update(System.ReadOnlySpan<System.Byte> data);

    /// <summary>
    /// Finalizes the hash computation and returns the result.
    /// </summary>
    /// <returns>The computed hash as a byte array.</returns>
    /// <remarks>
    /// Once this method is called, no further updates are allowed until `Initialize()` is called again.
    /// </remarks>
    System.Byte[] FinalizeHash();
}
