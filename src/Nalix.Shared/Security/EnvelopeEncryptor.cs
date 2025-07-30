// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Security.Enums;
using Nalix.Shared.Extensions;
using Nalix.Shared.Security.Internal;

namespace Nalix.Shared.Security;

/// <summary>
/// Provides automatic attribute-driven encryption and decryption of sensitive members
/// on arbitrary object graphs.
/// </summary>
/// <remarks>
/// <para>
/// Encryption is governed by <see cref="DataSensitivityLevel"/>:
/// members tagged with <c>Public</c> or <c>Internal</c> are skipped silently;
/// only <c>Confidential</c>, <c>High</c>, and <c>Critical</c> levels are encrypted.
/// </para>
/// <para>
/// All per-type metadata and delegates are built once on first access and cached
/// indefinitely — subsequent calls are O(1) with zero reflection overhead.
/// </para>
/// <para>
/// Thread-safe for concurrent use across multiple object instances.
/// </para>
/// </remarks>
public static class EnvelopeEncryptor
{
    #region Fields

    // Shared empty AAD array — eliminates Array.Empty<byte>() call overhead at every call site.
    private static readonly System.Byte[] _emptyAad = [];

    // External storage for encrypted value types (keyed by object instance + member name).
    // ConditionalWeakTable ensures entries are collected when the owning object is GC'd.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Object, System.Collections.Generic.Dictionary<System.String, EncryptedValueStorage>> _encryptedValueStorage = [];

    #endregion Fields

    #region Public API

    /// <summary>
    /// Encrypts all properties and fields marked with
    /// <see cref="Common.Security.Attributes.SensitiveDataAttribute"/> whose
    /// <see cref="DataSensitivityLevel"/> is at or above <c>Confidential</c>.
    /// </summary>
    /// <typeparam name="T">The object type to process. Must be a reference type.</typeparam>
    /// <param name="obj">The object whose sensitive members will be encrypted in place.</param>
    /// <param name="key">Encryption key. Length must match <paramref name="algorithm"/> requirements.</param>
    /// <param name="algorithm">The cipher suite to use.</param>
    /// <param name="aad">
    /// Optional Additional Authenticated Data (AEAD suites only).
    /// Must be supplied identically when calling <see cref="Decrypt{T}"/>.
    /// </param>
    /// <returns>The same <paramref name="obj"/> instance with sensitive members encrypted.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="obj"/> or <paramref name="key"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="key"/> length is invalid for <paramref name="algorithm"/>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static T Encrypt<T>(
        T obj,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[]? aad = null) where T : class
    {
        System.ArgumentNullException.ThrowIfNull(obj);
        System.ArgumentNullException.ThrowIfNull(key);

        System.Byte[] aadArray = aad ?? _emptyAad;
        SensitiveMemberCache cache = EnvelopeMemberResolver.GetMembers(typeof(T));

        if (!cache.HasAnyMembers)
        {
            return obj;
        }

        var storage = _encryptedValueStorage.GetOrCreateValue(obj);

        EncryptMembers(obj, cache.Properties, storage, key, algorithm, aadArray);
        EncryptMembers(obj, cache.Fields, storage, key, algorithm, aadArray);

        if (obj is IPacket packet)
        {
            packet.Flags = packet.Flags.AddFlag(PacketFlags.ENCRYPTED);
        }

        return obj;
    }

    /// <summary>
    /// Decrypts all properties and fields marked with
    /// <see cref="Common.Security.Attributes.SensitiveDataAttribute"/> on the specified object.
    /// </summary>
    /// <typeparam name="T">The object type to process. Must be a reference type.</typeparam>
    /// <param name="obj">The object whose sensitive members will be decrypted in place.</param>
    /// <param name="key">Decryption key. Must match the key used during <see cref="Encrypt{T}"/>.</param>
    /// <param name="aad">
    /// Optional Additional Authenticated Data. Must match the value supplied at encrypt time.
    /// </param>
    /// <returns>The same <paramref name="obj"/> instance with sensitive members decrypted.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="obj"/> or <paramref name="key"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="CryptoException">
    /// Thrown when authentication fails for any member.
    /// <b>The object may be partially decrypted</b>; callers requiring atomicity should clone first.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static T Decrypt<T>(
        T obj,
        System.Byte[] key,
        System.Byte[]? aad = null) where T : class
    {
        System.ArgumentNullException.ThrowIfNull(obj);
        System.ArgumentNullException.ThrowIfNull(key);

        System.Byte[] aadArray = aad ?? _emptyAad;
        SensitiveMemberCache cache = EnvelopeMemberResolver.GetMembers(typeof(T));

        if (!cache.HasAnyMembers)
        {
            return obj;
        }

        if (!_encryptedValueStorage.TryGetValue(obj, out var storage))
        {
            storage = [];
        }

        System.Int32 successCount = 0;
        System.Int32 totalCount = cache.EncryptableCount;

        DecryptMembers(obj, cache.Properties, storage, key, aadArray,
            ref successCount, totalCount, isMemberProperty: true, typeName: typeof(T).Name);

        DecryptMembers(obj, cache.Fields, storage, key, aadArray,
            ref successCount, totalCount, isMemberProperty: false, typeName: typeof(T).Name);

        storage.Clear();

        if (obj is IPacket packet)
        {
            packet.Flags = packet.Flags.RemoveFlag(PacketFlags.ENCRYPTED);
        }

        return obj;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <typeparamref name="T"/> contains at least one
    /// member annotated with <see cref="Common.Security.Attributes.SensitiveDataAttribute"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean HasSensitiveData<T>() => EnvelopeMemberResolver.GetMembers(typeof(T)).HasAnyMembers;

    /// <summary>
    /// Returns the names and sensitivity levels of all members on <typeparamref name="T"/>
    /// that carry <see cref="Common.Security.Attributes.SensitiveDataAttribute"/>.
    /// </summary>
    /// <returns>
    /// An array of strings in the format <c>"MemberName [Level]"</c>,
    /// or an empty array when none are found.
    /// </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0301:Simplify collection initialization", Justification = "Explicit for clarity")]
    public static System.String[] GetSensitiveDataMembers<T>()
    {
        SensitiveMemberCache cache = EnvelopeMemberResolver.GetMembers(typeof(T));

        System.Int32 total = cache.Properties.Length + cache.Fields.Length;
        if (total is 0)
        {
            return System.Array.Empty<System.String>();
        }

        System.String[] names = new System.String[total];
        System.Int32 index = 0;

        foreach (var prop in cache.Properties)
        {
            names[index++] = $"{prop.Name} [{prop.Level}]";
        }
        foreach (var field in cache.Fields)
        {
            names[index++] = $"{field.Name} [{field.Level}]";
        }

        return names;
    }

    #endregion Public API

    #region Private Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EncryptMembers(
        System.Object obj,
        SensitiveMemberInfo[] members,
        System.Collections.Generic.Dictionary<System.String, EncryptedValueStorage> storage,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[] aad)
    {
        System.ReadOnlySpan<SensitiveMemberInfo> span = members;
        for (System.Int32 i = 0; i < span.Length; i++)
        {
            SensitiveMemberInfo member = span[i];
            if (member.Level < EnvelopeMemberResolver.EncryptionThreshold)
            {
                continue;
            }

            System.Object? value = member.Getter(obj);
            if (value is null)
            {
                continue;
            }

            System.Object? encrypted = EncryptValue(
                value, member.MemberType, member.Name, storage, key, algorithm, aad);

            if (encrypted is not null)
            {
                member.Setter(obj, encrypted);
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void DecryptMembers(
        System.Object obj,
        SensitiveMemberInfo[] members,
        System.Collections.Generic.Dictionary<System.String, EncryptedValueStorage> storage,
        System.Byte[] key,
        System.Byte[] aad,
        ref System.Int32 successCount,
        System.Int32 totalCount,
        System.Boolean isMemberProperty,
        System.String typeName)
    {
        System.ReadOnlySpan<SensitiveMemberInfo> span = members;
        for (System.Int32 i = 0; i < span.Length; i++)
        {
            SensitiveMemberInfo member = span[i];
            if (member.Level < EnvelopeMemberResolver.EncryptionThreshold)
            {
                continue;
            }

            try
            {
                System.Object? decrypted = DecryptValue(
                    member.Name, member.MemberType, member.Getter(obj), storage, key, aad);

                if (decrypted is not null)
                {
                    member.Setter(obj, decrypted);
                }

                successCount++;
            }
            catch (System.Exception ex)
            {
                System.String memberKind = isMemberProperty ? "property" : "field";
                throw new CryptoException(
                    $"Decryption failed on {memberKind} '{member.Name}' of '{typeName}'. " +
                    $"Object is partially decrypted ({successCount}/{totalCount} members done). " +
                    $"Inner: {ex.Message}", ex);
            }
        }
    }

    // ── Value-level encrypt/decrypt ───────────────────────────────────────────

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object? EncryptValue(
        System.Object value,
        System.Type memberType,
        System.String memberName,
        System.Collections.Generic.Dictionary<System.String, EncryptedValueStorage> storage,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[] aad)
    {
        // ── String (most common path) ──────────────────────────────────────────
        if (value is System.String str)
        {
            return System.String.IsNullOrEmpty(str)
                ? str
                : str.EncryptToBase64(key, algorithm, aad);
        }

        // ── List<T> ────────────────────────────────────────────────────────────
        if (value is System.Collections.IList list &&
            memberType.IsGenericType &&
            memberType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            System.Type elementType = memberType.GetGenericArguments()[0];
            if (elementType.IsClass && elementType != typeof(System.String))
            {
                System.Int32 count = list.Count;
                for (System.Int32 i = 0; i < count; i++)
                {
                    System.Object? item = list[i];
                    if (item is not null)
                    {
                        EncryptNestedObject(item, key, algorithm, aad);
                    }
                }
            }
            return value;
        }

        // ── Array T[] ─────────────────────────────────────────────────────────
        if (value is System.Array array && memberType.IsArray)
        {
            System.Type elementType = memberType.GetElementType()!;
            if (elementType.IsClass && elementType != typeof(System.String))
            {
                System.Int32 length = array.Length;
                for (System.Int32 i = 0; i < length; i++)
                {
                    System.Object? item = array.GetValue(i);
                    if (item is not null)
                    {
                        EncryptNestedObject(item, key, algorithm, aad);
                    }
                }
            }
            return value;
        }

        // ── Nested reference type ──────────────────────────────────────────────
        if (memberType.IsClass)
        {
            EncryptNestedObject(value, key, algorithm, aad);
            return value;
        }

        // ── Value type (int, bool, enum, struct) ───────────────────────────────
        System.String encryptedBase64 = EnvelopeDelegateStore
            .GetSerializationDelegates(memberType)
            .SerializeFunc(value, key, algorithm, aad);

        storage[memberName] = new EncryptedValueStorage
        {
            EncryptedBase64 = encryptedBase64,
            OriginalType = memberType
        };

        // Return cached boxed default(T) — avoids Activator.CreateInstance on hot path.
        return EnvelopeDelegateStore.GetDefaultValue(memberType);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object? DecryptValue(
        System.String memberName,
        System.Type memberType,
        System.Object? currentValue,
        System.Collections.Generic.Dictionary<System.String, EncryptedValueStorage> storage,
        System.Byte[] key,
        System.Byte[] aad)
    {
        // Value type stored in external storage during Encrypt.
        if (storage.TryGetValue(memberName, out EncryptedValueStorage? encryptedStorage))
        {
            return EnvelopeDelegateStore
                .GetSerializationDelegates(encryptedStorage.OriginalType)
                .DeserializeFunc(encryptedStorage.EncryptedBase64, key, aad);
        }

        if (memberType.IsValueType)
        {
            return null;
        }

        if (currentValue is null)
        {
            return null;
        }

        // ── String ────────────────────────────────────────────────────────────
        if (currentValue is System.String str)
        {
            return System.String.IsNullOrEmpty(str)
                ? str
                : str.DecryptFromBase64(key, aad);
        }

        // ── List<T> ───────────────────────────────────────────────────────────
        if (currentValue is System.Collections.IList list &&
            memberType.IsGenericType &&
            memberType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            System.Type elementType = memberType.GetGenericArguments()[0];
            if (elementType.IsClass && elementType != typeof(System.String))
            {
                System.Int32 count = list.Count;
                for (System.Int32 i = 0; i < count; i++)
                {
                    System.Object? item = list[i];
                    if (item is not null)
                    {
                        DecryptNestedObject(item, key, aad);
                    }
                }
            }
            return currentValue;
        }

        // ── Array T[] ─────────────────────────────────────────────────────────
        if (currentValue is System.Array arr && memberType.IsArray)
        {
            System.Type elementType = memberType.GetElementType()!;
            if (elementType.IsClass && elementType != typeof(System.String))
            {
                System.Int32 length = arr.Length;
                for (System.Int32 i = 0; i < length; i++)
                {
                    System.Object? item = arr.GetValue(i);
                    if (item is not null)
                    {
                        DecryptNestedObject(item, key, aad);
                    }
                }
            }
            return currentValue;
        }

        // ── Nested reference object ────────────────────────────────────────────
        if (memberType.IsClass)
        {
            DecryptNestedObject(currentValue, key, aad);
            return currentValue;
        }

        return null;
    }

    // ── Nested object dispatchers ─────────────────────────────────────────────

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EncryptNestedObject(System.Object obj, System.Byte[] key, CipherSuiteType algorithm, System.Byte[] aad)
    {
        NestedEncryptorDelegates delegates = EnvelopeDelegateStore.GetNestedDelegates(obj.GetType());
        delegates.EncryptAction(obj, key, algorithm, aad);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void DecryptNestedObject(System.Object obj, System.Byte[] key, System.Byte[] aad)
    {
        NestedEncryptorDelegates delegates = EnvelopeDelegateStore.GetNestedDelegates(obj.GetType());
        delegates.DecryptAction(obj, key, aad);
    }

    #endregion Private Helpers
}
