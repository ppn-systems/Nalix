// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using Nalix.Common.Exceptions;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Shared.Extensions;
using Nalix.Shared.Memory.Buffers;
using Nalix.Shared.Serialization.Formatters;

namespace Nalix.Shared.Security;

/// <summary>
/// Provides automatic encryption and decryption of sensitive data
/// for object graphs using attribute-based metadata and serialization formatters.
/// </summary>
/// <remarks>
/// <para>
/// This class leverages the existing serialization infrastructure to handle
/// encryption of complex types uniformly, eliminating type-specific logic.
/// </para>
/// <para>
/// Encryption is governed by <see cref="DataSensitivityLevel"/>:
/// members tagged with <c>Public</c> or <c>Internal</c> are skipped;
/// only <c>Confidential</c>, <c>High</c>, and <c>Critical</c> are encrypted.
/// </para>
/// <para>
/// Thread-safe for concurrent use across multiple object instances.
/// </para>
/// </remarks>
public static class EnvelopeEncryptor
{
    #region Fields

    /// <summary>
    /// Minimum sensitivity level that triggers encryption.
    /// Members below this threshold are skipped silently.
    /// </summary>
    private const DataSensitivityLevel EncryptionThreshold = DataSensitivityLevel.Confidential;

    // Cache serialization/deserialization delegates keyed by member value type
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, EncryptionDelegates> _delegateCache = new();

    // Cache sensitive member metadata per type (properties + fields + their sensitivity levels)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, SensitiveMemberCache> _sensitiveMetadataCache = new();

    // Cache nested Encrypt<T>/Decrypt<T> delegates to avoid GetMethod+MakeGenericMethod per call
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, NestedEncryptorDelegates> _nestedDelegateCache = new();

    // External storage for encrypted value types (keyed by object instance + member name)
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        System.Object,
        System.Collections.Generic.Dictionary<System.String, EncryptedValueStorage>> _encryptedValueStorage = [];

    // OPT-1: Shared empty byte array — eliminates Array.Empty<byte>() call overhead at every nested boundary
    private static readonly System.Byte[] _emptyAad = [];

    // OPT-2: Cache default(T) boxed values per value type — eliminates Activator.CreateInstance on hot path
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Object> _defaultValueCache = new();

    #endregion Fields

    #region Nested Types

    /// <summary>
    /// Cached metadata for sensitive members, including per-member sensitivity level.
    /// </summary>
    private readonly struct SensitiveMemberCache(
        SensitiveMemberInfo[] properties,
        SensitiveMemberInfo[] fields,
        System.Int32 encryptableCount)
    {
        public readonly SensitiveMemberInfo[] Properties = properties;
        public readonly SensitiveMemberInfo[] Fields = fields;
        public readonly System.Boolean HasAnyMembers = properties.Length > 0 || fields.Length > 0;

        // OPT-3: Pre-computed count of members that actually need encrypting (Level >= threshold)
        // Avoids recomputing this in Decrypt's error message every time
        public readonly System.Int32 EncryptableCount = encryptableCount;
    }

    /// <summary>
    /// Pairs a reflected member with its sensitivity level and a pre-compiled accessor delegate.
    /// Storing the accessor here eliminates secondary GetProperty/GetField calls at decrypt time.
    /// </summary>
    private sealed class SensitiveMemberInfo
    {
        public required System.String Name { get; init; }
        public required System.Type MemberType { get; init; }
        public required DataSensitivityLevel Level { get; init; }

        // Pre-compiled getter/setter delegates — avoids reflection on every Encrypt/Decrypt call
        public required System.Func<System.Object, System.Object?> Getter { get; init; }
        public required System.Action<System.Object, System.Object?> Setter { get; init; }
    }

    /// <summary>
    /// Cached delegates for type-safe serialization/deserialization operations.
    /// </summary>
    private sealed class EncryptionDelegates
    {
        public required System.Func<System.Object, System.Byte[], CipherSuiteType, System.Byte[], System.String> SerializeFunc { get; init; }
        public required System.Func<System.String, System.Byte[], System.Byte[], System.Object> DeserializeFunc { get; init; }
    }

    /// <summary>
    /// Cached delegates for nested Encrypt[T] / Decrypt[T] calls,
    /// replacing GetMethod + MakeGenericMethod on every nested object encounter.
    /// </summary>
    private sealed class NestedEncryptorDelegates
    {
        public required System.Action<System.Object, System.Byte[], CipherSuiteType, System.Byte[]> EncryptAction { get; init; }
        public required System.Action<System.Object, System.Byte[]> DecryptAction { get; init; }
    }

    /// <summary>
    /// Storage for encrypted value-type data (int, bool, struct, enum, etc.).
    /// </summary>
    private sealed class EncryptedValueStorage
    {
        public required System.String EncryptedBase64 { get; init; }
        public required System.Type OriginalType { get; init; }
    }

    #endregion Nested Types

    #region Public API

    /// <summary>
    /// Encrypts all properties and fields marked with
    /// <see cref="SensitiveDataAttribute"/> whose <see cref="DataSensitivityLevel"/>
    /// is at or above <see cref="EncryptionThreshold"/> on the specified object.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="obj"/> or <paramref name="key"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="key"/> length is invalid for <paramref name="algorithm"/>.</exception>
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

        ValidateKeyLength(key, algorithm);

        // OPT-4: Reuse caller's aad array directly — avoids ToArray() materialisation on the top-level call.
        // Internal helpers receive byte[] instead of ReadOnlySpan to prevent repeated ToArray() at every
        // nested boundary. The penalty is one null-check instead of repeated heap allocs.
        System.Byte[] aadArray = aad ?? _emptyAad;

        var cache = GET_SENSITIVE_MEMBERS(typeof(T));
        if (!cache.HasAnyMembers)
        {
            return obj;
        }

        var storage = _encryptedValueStorage.GetOrCreateValue(obj);

        System.ReadOnlySpan<SensitiveMemberInfo> properties = cache.Properties;
        for (System.Int32 i = 0; i < properties.Length; i++)
        {
            var member = properties[i];

            if (member.Level < EncryptionThreshold)
            {
                continue;
            }

            var value = member.Getter(obj);
            if (value is null)
            {
                continue;
            }

            var encrypted = ENCRYPT_VALUE(value, member.MemberType, member.Name, storage, key, algorithm, aadArray);
            if (encrypted is not null)
            {
                member.Setter(obj, encrypted);
            }
        }

        System.ReadOnlySpan<SensitiveMemberInfo> fields = cache.Fields;
        for (System.Int32 i = 0; i < fields.Length; i++)
        {
            var member = fields[i];

            if (member.Level < EncryptionThreshold)
            {
                continue;
            }

            var value = member.Getter(obj);
            if (value is null)
            {
                continue;
            }

            var encrypted = ENCRYPT_VALUE(value, member.MemberType, member.Name, storage, key, algorithm, aadArray);
            if (encrypted is not null)
            {
                member.Setter(obj, encrypted);
            }
        }

        if (obj is IPacket packet)
        {
            packet.Flags = packet.Flags.AddFlag(PacketFlags.ENCRYPTED);
        }

        return obj;
    }

    /// <summary>
    /// Decrypts all properties and fields marked with
    /// <see cref="SensitiveDataAttribute"/> on the specified object.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="obj"/> or <paramref name="key"/> is null.</exception>
    /// <exception cref="System.Security.SecurityException">
    /// Thrown if authentication fails for any member. The object may be in a partially-decrypted
    /// state; callers that need atomicity should work on a clone.
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

        // OPT-4: Same as Encrypt — reuse array directly, no ToArray() copies
        System.Byte[] aadArray = aad ?? _emptyAad;

        var cache = GET_SENSITIVE_MEMBERS(typeof(T));
        if (!cache.HasAnyMembers)
        {
            return obj;
        }

        if (!_encryptedValueStorage.TryGetValue(obj, out var storage))
        {
            storage = [];
        }

        System.Int32 successCount = 0;
        // OPT-3: Use pre-computed EncryptableCount instead of recomputing via LINQ each call
        System.Int32 totalCount = cache.EncryptableCount;

        System.ReadOnlySpan<SensitiveMemberInfo> properties = cache.Properties;
        for (System.Int32 i = 0; i < properties.Length; i++)
        {
            var member = properties[i];
            if (member.Level < EncryptionThreshold)
            {
                continue;
            }

            try
            {
                var decrypted = DECRYPT_VALUE(member.Name, member.MemberType, member.Getter(obj), storage, key, aadArray);
                if (decrypted is not null)
                {
                    member.Setter(obj, decrypted);
                }
                successCount++;
            }
            catch (System.Exception ex)
            {
                throw new System.Security.SecurityException(
                    $"Decryption failed on property '{member.Name}' of '{typeof(T).Name}'. " +
                    $"Object is partially decrypted ({successCount}/{totalCount} members done). " +
                    $"Inner: {ex.Message}", ex);
            }
        }

        System.ReadOnlySpan<SensitiveMemberInfo> fields = cache.Fields;
        for (System.Int32 i = 0; i < fields.Length; i++)
        {
            var member = fields[i];
            if (member.Level < EncryptionThreshold)
            {
                continue;
            }

            try
            {
                var decrypted = DECRYPT_VALUE(member.Name, member.MemberType, member.Getter(obj), storage, key, aadArray);
                if (decrypted is not null)
                {
                    member.Setter(obj, decrypted);
                }
                successCount++;
            }
            catch (System.Exception ex)
            {
                throw new System.Security.SecurityException(
                    $"Decryption failed on field '{member.Name}' of '{typeof(T).Name}'. " +
                    $"Object is partially decrypted ({successCount}/{totalCount} members done). " +
                    $"Inner: {ex.Message}", ex);
            }
        }

        storage.Clear();

        if (obj is IPacket packet)
        {
            packet.Flags = packet.Flags.RemoveFlag(PacketFlags.ENCRYPTED);
        }

        return obj;
    }

    /// <summary>
    /// Determines whether the specified type contains any members
    /// marked with <see cref="SensitiveDataAttribute"/> that would be encrypted.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean HasSensitiveData<T>()
    {
        var cache = GET_SENSITIVE_MEMBERS(typeof(T));
        return cache.HasAnyMembers;
    }

    /// <summary>
    /// Gets the names of all properties and fields marked with
    /// <see cref="SensitiveDataAttribute"/> on the specified type,
    /// along with their sensitivity level.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public static System.String[] GetSensitiveDataMembers<T>()
    {
        var cache = GET_SENSITIVE_MEMBERS(typeof(T));

        System.Int32 totalCount = cache.Properties.Length + cache.Fields.Length;
        if (totalCount is 0)
        {
            return System.Array.Empty<System.String>();
        }

        System.String[] names = new System.String[totalCount];
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

    #region Key Validation

    /// <summary>
    /// Validates key length against the chosen algorithm before entering the encrypt path.
    /// Throws <see cref="System.ArgumentException"/> with a clear, actionable message.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ValidateKeyLength(System.Byte[] key, CipherSuiteType algorithm)
    {
        System.Boolean valid = algorithm switch
        {
            CipherSuiteType.CHACHA20_POLY1305 => key.Length == 32,
            CipherSuiteType.SALSA20_POLY1305 => key.Length is 16 or 32,
            CipherSuiteType.SPECK_POLY1305 => key.Length == 32,
            CipherSuiteType.CHACHA20 => key.Length == 32,
            CipherSuiteType.SALSA20 => key.Length is 16 or 32,
            CipherSuiteType.SPECK => key.Length == 32,
            _ => throw new System.ArgumentException($"Unsupported algorithm: {algorithm}", nameof(algorithm))
        };

        if (!valid)
        {
            throw new System.ArgumentException(
                $"Invalid key length {key.Length} bytes for algorithm {algorithm}. " +
                $"Expected: {GetExpectedKeyLengthDescription(algorithm)}",
                nameof(key));
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String GetExpectedKeyLengthDescription(CipherSuiteType algorithm) => algorithm switch
    {
        CipherSuiteType.CHACHA20_POLY1305 or CipherSuiteType.CHACHA20 or
        CipherSuiteType.SPECK_POLY1305 or CipherSuiteType.SPECK => "32 bytes",
        CipherSuiteType.SALSA20_POLY1305 or CipherSuiteType.SALSA20 => "16 or 32 bytes",
        _ => "unknown"
    };

    #endregion Key Validation

    #region Private Helpers

    /// <summary>
    /// Builds and caches <see cref="SensitiveMemberCache"/> for the given type.
    /// Pre-compiles getter/setter delegates to eliminate per-call reflection.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SensitiveMemberCache GET_SENSITIVE_MEMBERS(System.Type type)
    {
        return _sensitiveMetadataCache.GetOrAdd(type, static t =>
        {
            System.Reflection.PropertyInfo[] allProperties = t.GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            var tempProps = new System.Collections.Generic.List<SensitiveMemberInfo>(allProperties.Length);

            foreach (System.Reflection.PropertyInfo prop in allProperties)
            {
                if (!prop.CanWrite)
                {
                    continue;
                }

                var attr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<SensitiveDataAttribute>(prop);
                if (attr is null)
                {
                    continue;
                }

                var getter = BUILD_PROPERTY_GETTER(prop);
                var setter = BUILD_PROPERTY_SETTER(prop);

                tempProps.Add(new SensitiveMemberInfo
                {
                    Name = prop.Name,
                    MemberType = prop.PropertyType,
                    Level = attr.Level,
                    Getter = getter,
                    Setter = setter
                });
            }

            System.Reflection.FieldInfo[] allFields = t.GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            var tempFields = new System.Collections.Generic.List<SensitiveMemberInfo>(allFields.Length);

            foreach (System.Reflection.FieldInfo field in allFields)
            {
                var attr = System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<SensitiveDataAttribute>(field);
                if (attr is null)
                {
                    continue;
                }

                var getter = BUILD_FIELD_GETTER(field);
                var setter = BUILD_FIELD_SETTER(field);

                tempFields.Add(new SensitiveMemberInfo
                {
                    Name = field.Name,
                    MemberType = field.FieldType,
                    Level = attr.Level,
                    Getter = getter,
                    Setter = setter
                });
            }

            SensitiveMemberInfo[] propsArray = [.. tempProps];
            SensitiveMemberInfo[] fieldsArray = [.. tempFields];

            // OPT-3: Pre-compute EncryptableCount once at cache-build time
            System.Int32 encryptableCount = 0;
            foreach (var m in propsArray)
            {
                if (m.Level >= EncryptionThreshold)
                {
                    encryptableCount++;
                }
            }
            foreach (var m in fieldsArray)
            {
                if (m.Level >= EncryptionThreshold)
                {
                    encryptableCount++;
                }
            }

            return new SensitiveMemberCache(propsArray, fieldsArray, encryptableCount);
        });
    }

    // Pre-compile a property getter as a delegate once, then call it O(1) forever
    private static System.Func<System.Object, System.Object?> BUILD_PROPERTY_GETTER(
        System.Reflection.PropertyInfo prop)
    {
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "obj");
        var cast = System.Linq.Expressions.Expression.Convert(instance, prop.DeclaringType!);
        var access = System.Linq.Expressions.Expression.Property(cast, prop);
        var box = System.Linq.Expressions.Expression.Convert(access, typeof(System.Object));
        return System.Linq.Expressions.Expression.Lambda<System.Func<System.Object, System.Object?>>(box, instance).Compile();
    }

    private static System.Action<System.Object, System.Object?> BUILD_PROPERTY_SETTER(
        System.Reflection.PropertyInfo prop)
    {
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "obj");
        var value = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "val");
        var cast = System.Linq.Expressions.Expression.Convert(instance, prop.DeclaringType!);
        var castVal = System.Linq.Expressions.Expression.Convert(value, prop.PropertyType);
        var assign = System.Linq.Expressions.Expression.Call(cast, prop.SetMethod!, castVal);
        return System.Linq.Expressions.Expression.Lambda<System.Action<System.Object, System.Object?>>(assign, instance, value).Compile();
    }

    private static System.Func<System.Object, System.Object?> BUILD_FIELD_GETTER(
        System.Reflection.FieldInfo field)
    {
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "obj");
        var cast = System.Linq.Expressions.Expression.Convert(instance, field.DeclaringType!);
        var access = System.Linq.Expressions.Expression.Field(cast, field);
        var box = System.Linq.Expressions.Expression.Convert(access, typeof(System.Object));
        return System.Linq.Expressions.Expression.Lambda<System.Func<System.Object, System.Object?>>(box, instance).Compile();
    }

    private static System.Action<System.Object, System.Object?> BUILD_FIELD_SETTER(
        System.Reflection.FieldInfo field)
    {
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "obj");
        var value = System.Linq.Expressions.Expression.Parameter(typeof(System.Object), "val");
        var cast = System.Linq.Expressions.Expression.Convert(instance, field.DeclaringType!);
        var castVal = System.Linq.Expressions.Expression.Convert(value, field.FieldType);
        var assign = System.Linq.Expressions.Expression.Assign(
                           System.Linq.Expressions.Expression.Field(cast, field), castVal);
        return System.Linq.Expressions.Expression.Lambda<System.Action<System.Object, System.Object?>>(assign, instance, value).Compile();
    }

    /// <summary>
    /// Encrypts a value based on its runtime type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object? ENCRYPT_VALUE(
        System.Object value,
        System.Type memberType,
        System.String memberName,
        System.Collections.Generic.Dictionary<System.String, EncryptedValueStorage> storage,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[] aad)          // OPT-4: byte[] instead of ReadOnlySpan — no repeated ToArray() at each nested level
    {
        // ── String (most common path) ──────────────────────────────────────
        if (value is System.String str)
        {
            if (System.String.IsNullOrEmpty(str))
            {
                return str;
            }

            System.Int32 maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(str.Length);
            System.Byte[] rentedBuffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(maxByteCount);

            try
            {
                System.Int32 byteCount = System.Text.Encoding.UTF8.GetBytes(
                    str, 0, str.Length, rentedBuffer, 0);

                System.Byte[] cipherBytes = EnvelopeCipher.Encrypt(
                    key, System.MemoryExtensions.AsSpan(rentedBuffer, 0, byteCount), algorithm, aad);

                return System.Convert.ToBase64String(cipherBytes);
            }
            finally
            {
                System.MemoryExtensions.AsSpan(rentedBuffer, 0, maxByteCount).Clear();
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rentedBuffer);
            }
        }

        // ── List<T> ────────────────────────────────────────────────────────
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
                    var item = list[i];
                    if (item is not null)
                    {
                        ENCRYPT_NESTED_OBJECT(item, key, algorithm, aad);
                    }
                }
            }
            return value;
        }

        // ── Array T[] ─────────────────────────────────────────────────────
        if (value is System.Array array && memberType.IsArray)
        {
            var elementType = memberType.GetElementType()!;
            if (elementType.IsClass && elementType != typeof(System.String))
            {
                System.Int32 length = array.Length;
                for (System.Int32 i = 0; i < length; i++)
                {
                    var item = array.GetValue(i);
                    if (item is not null)
                    {
                        ENCRYPT_NESTED_OBJECT(item, key, algorithm, aad);
                    }
                }
            }
            return value;
        }

        // ── Nested reference type with [SensitiveData] ────────────────────
        if (memberType.IsClass)
        {
            ENCRYPT_NESTED_OBJECT(value, key, algorithm, aad);
            return value;
        }

        // ── Value types (int, bool, enum, struct) ─────────────────────────
        System.String encryptedBase64 = SERIALIZE_WITH_DELEGATES(value, memberType, key, algorithm, aad);
        storage[memberName] = new EncryptedValueStorage
        {
            EncryptedBase64 = encryptedBase64,
            OriginalType = memberType
        };

        // OPT-2: Return cached boxed default(T) instead of calling Activator.CreateInstance each time
        return GET_DEFAULT_VALUE(memberType);
    }

    /// <summary>
    /// Decrypts a value based on its runtime type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object? DECRYPT_VALUE(
        System.String memberName,
        System.Type memberType,
        System.Object? currentValue,
        System.Collections.Generic.Dictionary<System.String, EncryptedValueStorage> storage,
        System.Byte[] key,
        System.Byte[] aad)          // OPT-4: byte[] — no repeated ToArray() copies
    {
        // Value type stored in external storage
        if (storage.TryGetValue(memberName, out var encryptedStorage))
        {
            return DESERIALIZE_WITH_DELEGATES(encryptedStorage.EncryptedBase64, encryptedStorage.OriginalType, key, aad);
        }

        if (memberType.IsValueType)
        {
            return null;
        }

        var value = currentValue;
        if (value is null)
        {
            return null;
        }

        // ── String ────────────────────────────────────────────────────────
        if (value is System.String str)
        {
            if (System.String.IsNullOrEmpty(str))
            {
                return str;
            }

            // OPT-5: Use ArrayPool to decode Base64 into a rented buffer, avoiding a fresh heap allocation
            // for the cipher bytes before passing them to Decrypt.
            System.Int32 maxDecodeLen = (str.Length + 3) / 4 * 3;  // upper bound for Base64 decode
            System.Byte[] rentedCipher = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(maxDecodeLen);
            try
            {
                if (!System.Convert.TryFromBase64String(str, rentedCipher, out System.Int32 cipherLen))
                {
                    throw new System.Security.SecurityException(
                        $"Invalid Base64 data while decrypting string member '{memberName}'.");
                }

                System.ReadOnlySpan<System.Byte> cipherSpan = System.MemoryExtensions.AsSpan(rentedCipher, 0, cipherLen);

                return !EnvelopeCipher.Decrypt(key, cipherSpan, out System.Byte[]? plainBytes, aad)
                    ? throw new System.Security.SecurityException(
                        $"Authentication tag mismatch while decrypting string member '{memberName}'. " +
                        "Key or AAD may be incorrect, or data was tampered.")
                    : System.Text.Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                // Zero rented buffer before returning — sensitive cipher data hygiene
                System.MemoryExtensions.AsSpan(rentedCipher, 0, maxDecodeLen).Clear();
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(rentedCipher);
            }
        }

        // ── List<T> ───────────────────────────────────────────────────────
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
                    var item = list[i];
                    if (item is not null)
                    {
                        DECRYPT_NESTED_OBJECT(item, key, aad);
                    }
                }
            }
            return value;
        }

        // ── Array T[] ─────────────────────────────────────────────────────
        if (value is System.Array arr && memberType.IsArray)
        {
            var elementType = memberType.GetElementType()!;
            if (elementType.IsClass && elementType != typeof(System.String))
            {
                System.Int32 length = arr.Length;
                for (System.Int32 i = 0; i < length; i++)
                {
                    var item = arr.GetValue(i);
                    if (item is not null)
                    {
                        DECRYPT_NESTED_OBJECT(item, key, aad);
                    }
                }
            }
            return value;
        }

        // ── Nested reference object ────────────────────────────────────────
        if (memberType.IsClass)
        {
            DECRYPT_NESTED_OBJECT(value, key, aad);
            return value;
        }

        return null;
    }

    // OPT-6: Extracted shared helper — eliminates the duplicate GetOrAdd block
    // that previously existed separately in ENCRYPT_NESTED_OBJECT and DECRYPT_NESTED_OBJECT.
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static NestedEncryptorDelegates GET_NESTED_DELEGATES(System.Type objectType)
    {
        return _nestedDelegateCache.GetOrAdd(objectType, static t =>
        {
            System.Reflection.MethodInfo encryptMethod = typeof(EnvelopeEncryptor)
                .GetMethod(nameof(Encrypt),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t);

            System.Reflection.MethodInfo decryptMethod = typeof(EnvelopeEncryptor)
                .GetMethod(nameof(Decrypt),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t);

            void encryptAction(System.Object o, System.Byte[] k, CipherSuiteType alg, System.Byte[] a)
                => encryptMethod.Invoke(null, [o, k, alg, a]);

            void decryptAction(System.Object o, System.Byte[] k)
                => decryptMethod.Invoke(null, [o, k, null]);

            return new NestedEncryptorDelegates
            {
                EncryptAction = encryptAction,
                DecryptAction = decryptAction
            };
        });
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ENCRYPT_NESTED_OBJECT(
        System.Object obj,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[] aad)  // OPT-4: Receive byte[] — no ToArray() needed here anymore
    {
        var delegates = GET_NESTED_DELEGATES(obj.GetType());
        delegates.EncryptAction(obj, key, algorithm, aad);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void DECRYPT_NESTED_OBJECT(
        System.Object obj,
        System.Byte[] key,
        System.Byte[] aad)  // OPT-4: Receive byte[] — no ToArray() needed here anymore
    {
        var delegates = GET_NESTED_DELEGATES(obj.GetType());
        delegates.DecryptAction(obj, key);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String SERIALIZE_WITH_DELEGATES(
        System.Object value,
        System.Type memberType,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[] aad)  // OPT-4: byte[] passed through directly — no Span snapshot needed
    {
        var delegates = GET_OR_CREATE_SERIALIZATION_DELEGATES(memberType);
        return delegates.SerializeFunc(value, key, algorithm, aad);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object DESERIALIZE_WITH_DELEGATES(
        System.String encryptedBase64,
        System.Type memberType,
        System.Byte[] key,
        System.Byte[] aad)  // OPT-4: byte[] passed through directly
    {
        var delegates = GET_OR_CREATE_SERIALIZATION_DELEGATES(memberType);
        return delegates.DeserializeFunc(encryptedBase64, key, aad);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static EncryptionDelegates GET_OR_CREATE_SERIALIZATION_DELEGATES(System.Type memberType)
    {
        return _delegateCache.GetOrAdd(memberType, static t =>
        {
            System.Reflection.MethodInfo serializeMethod = typeof(EnvelopeEncryptor)
                .GetMethod(nameof(SERIALIZE_GENERIC),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t);

            System.Reflection.MethodInfo deserializeMethod = typeof(EnvelopeEncryptor)
                .GetMethod(nameof(DESERIALIZE_GENERIC),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(t);

            return new EncryptionDelegates
            {
                SerializeFunc = (System.Func<System.Object, System.Byte[], CipherSuiteType, System.Byte[], System.String>)
                    System.Delegate.CreateDelegate(
                        typeof(System.Func<System.Object, System.Byte[], CipherSuiteType, System.Byte[], System.String>),
                        serializeMethod),

                DeserializeFunc = (System.Func<System.String, System.Byte[], System.Byte[], System.Object>)
                    System.Delegate.CreateDelegate(
                        typeof(System.Func<System.String, System.Byte[], System.Byte[], System.Object>),
                        deserializeMethod)
            };
        });
    }

    private static System.String SERIALIZE_GENERIC<T>(
        System.Object value,
        System.Byte[] key,
        CipherSuiteType algorithm,
        System.Byte[] aad)
    {
        if (value is not T typedValue)
        {
            throw new System.InvalidOperationException(
                $"Value '{value.GetType().Name}' cannot be cast to expected type '{typeof(T).Name}'.");
        }

        IFormatter<T> formatter = FormatterProvider.Get<T>();

        DataWriter writer = new(64);
        try
        {
            formatter.Serialize(ref writer, typedValue);

            // OPT-7: Pass the writer's internal span directly to Encrypt instead of calling
            // writer.ToArray() which allocates a new byte[] copy just to pass as plaintext.
            // EnvelopeCipher.Encrypt accepts ReadOnlySpan<byte> so no copy is needed.
            System.Byte[] cipherBytes = EnvelopeCipher.Encrypt(key, writer.FreeBuffer, algorithm, aad);
            return System.Convert.ToBase64String(cipherBytes);
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static System.Object DESERIALIZE_GENERIC<T>(
        System.String encryptedBase64,
        System.Byte[] key,
        System.Byte[] aad)
    {
        System.ArgumentNullException.ThrowIfNull(encryptedBase64);
        System.ArgumentNullException.ThrowIfNull(key);

        // OPT-8: Decode Base64 directly into a rented buffer instead of allocating a fresh byte[].
        // For a 256-bit key + Poly1305 tag, the cipher payload is typically 32–64 bytes, well under
        // the ArrayPool small-object threshold, so this rental is essentially free.
        System.Int32 maxDecodeLen = (encryptedBase64.Length + 3) / 4 * 3;
        System.Byte[] rentedCipher = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(maxDecodeLen);
        try
        {
            if (!System.Convert.TryFromBase64String(encryptedBase64, rentedCipher, out System.Int32 cipherLen))
            {
                throw new CryptoException(
                    $"Invalid Base64 data while decrypting value type '{typeof(T).Name}'.");
            }

            System.ReadOnlySpan<System.Byte> cipherSpan = System.MemoryExtensions.AsSpan(rentedCipher, 0, cipherLen);

            if (!EnvelopeCipher.Decrypt(key, cipherSpan, out System.Byte[]? plainBytes, aad))
            {
                throw new CryptoException(
                    $"Authentication tag mismatch while decrypting value type '{typeof(T).Name}'. " +
                    "Key or AAD is incorrect, or the ciphertext was tampered with.");
            }

            IFormatter<T> formatter = FormatterProvider.Get<T>();

            DataReader reader = new(plainBytes);
            try
            {
                T result = formatter.Deserialize(ref reader);
                return result!;
            }
            finally
            {
                reader.Dispose();
            }
        }
        finally
        {
            // Zero before returning to pool — decrypted plain data hygiene
            System.MemoryExtensions.AsSpan(rentedCipher, 0, maxDecodeLen).Clear();
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(rentedCipher);
        }
    }

    /// <summary>
    /// OPT-2: Returns a cached boxed default value for the given value type.
    /// Replaces <c>Activator.CreateInstance(memberType)</c> on the hot encrypt path,
    /// eliminating one heap allocation per value-type member per Encrypt call.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Object GET_DEFAULT_VALUE(System.Type type)
    {
        return _defaultValueCache.GetOrAdd(type, static t =>
        {
            // Build and compile a lambda that returns default(T) boxed as object — done once per type
            var body = System.Linq.Expressions.Expression.Convert(
                System.Linq.Expressions.Expression.Default(t),
                typeof(System.Object));
            var lambda = System.Linq.Expressions.Expression
                .Lambda<System.Func<System.Object>>(body)
                .Compile();
            return lambda();    // Invoke once to get the cached boxed default
        });
    }

    #endregion Private Helpers
}
