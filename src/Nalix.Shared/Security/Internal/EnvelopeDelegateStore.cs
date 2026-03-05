// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Shared.Security.Internal;

/// <summary>
/// Central store for all cached delegates used by <see cref="EnvelopeEncryptor"/>.
/// <list type="bullet">
///   <item>Serialization/deserialization delegates keyed by member value type.</item>
///   <item>Nested Encrypt/Decrypt delegates keyed by nested object type.</item>
///   <item>Boxed default(T) values keyed by value type — avoids <c>Activator.CreateInstance</c>.</item>
/// </list>
/// All dictionaries are written only on first access per type; subsequent reads are lock-free.
/// </summary>
internal static class EnvelopeDelegateStore
{
    // ── Serialization delegates ───────────────────────────────────────────────

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, EncryptionDelegates> _serializationDelegates = new();

    // ── Nested encryptor delegates ────────────────────────────────────────────

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, NestedEncryptorDelegates> _nestedDelegates = new();

    // ── Cached boxed default(T) values ────────────────────────────────────────

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Object> _defaultValues = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns (or creates) cached serialization/deserialization delegates for <paramref name="memberType"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static EncryptionDelegates GetSerializationDelegates(System.Type memberType) => _serializationDelegates.GetOrAdd(memberType, BuildSerializationDelegates);

    /// <summary>
    /// Returns (or creates) cached nested encryptor/decryptor delegates for <paramref name="objectType"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static NestedEncryptorDelegates GetNestedDelegates(System.Type objectType) => _nestedDelegates.GetOrAdd(objectType, BuildNestedDelegates);

    /// <summary>
    /// Returns a cached boxed <c>default(T)</c> for <paramref name="type"/>.
    /// Replaces <c>Activator.CreateInstance(memberType)</c> on the hot encrypt path.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.Object GetDefaultValue(System.Type type) => _defaultValues.GetOrAdd(type, BuildDefaultValue);

    // ── Builders ───────────��──────────────────────────────────────────────────

    private static EncryptionDelegates BuildSerializationDelegates(System.Type t)
    {
        System.Reflection.MethodInfo serializeMethod =
            typeof(EnvelopeValueCodec)
                .GetMethod(
                    nameof(EnvelopeValueCodec.Serialize),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t);

        System.Reflection.MethodInfo deserializeMethod =
            typeof(EnvelopeValueCodec)
                .GetMethod(
                    nameof(EnvelopeValueCodec.Deserialize),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t);

        return new EncryptionDelegates
        {
            SerializeFunc =
                (System.Func<System.Object, System.Byte[], CipherSuiteType, System.Byte[], System.String>)
                System.Delegate.CreateDelegate(
                    typeof(System.Func<System.Object, System.Byte[], CipherSuiteType, System.Byte[], System.String>),
                    serializeMethod),

            DeserializeFunc =
                (System.Func<System.String, System.Byte[], System.Byte[], System.Object>)
                System.Delegate.CreateDelegate(
                    typeof(System.Func<System.String, System.Byte[], System.Byte[], System.Object>),
                    deserializeMethod)
        };
    }

    private static NestedEncryptorDelegates BuildNestedDelegates(System.Type t)
    {
        System.Reflection.MethodInfo encryptMethod =
            typeof(EnvelopeEncryptor)
                .GetMethod(
                    nameof(EnvelopeEncryptor.Encrypt),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t);

        System.Reflection.MethodInfo decryptMethod =
            typeof(EnvelopeEncryptor)
                .GetMethod(
                    nameof(EnvelopeEncryptor.Decrypt),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t);

        void EncryptAction(System.Object o, System.Byte[] k, CipherSuiteType alg, System.Byte[] a) => encryptMethod.Invoke(null, [o, k, alg, a]);

        void DecryptAction(System.Object o, System.Byte[] k) => decryptMethod.Invoke(null, [o, k, null]);

        return new NestedEncryptorDelegates
        {
            EncryptAction = EncryptAction,
            DecryptAction = DecryptAction
        };
    }

    private static System.Object BuildDefaultValue(System.Type t)
    {
        // Build and compile lambda: () => (object)default(T)
        // Executed once per type; result is cached indefinitely.
        var body = System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.Default(t), typeof(System.Object));
        var lambda = System.Linq.Expressions.Expression.Lambda<System.Func<System.Object>>(body).Compile();
        return lambda();
    }
}