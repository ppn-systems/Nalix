using Notio.Serialization.Internal.Extensions;
using Notio.Serialization.Internal.Reflection;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Notio.Serialization;

/// <summary>
/// A very simple, light-weight JSON library written by Mario
/// to teach Geo how things are done
///
/// This is an useful helper for small tasks but it doesn't represent a full-featured
/// serializer such as the beloved Serialization.NET.
/// </summary>
public static partial class Json
{
    private class Converter
    {
        // Caches to avoid repeated reflection lookups.
        private static readonly ConcurrentDictionary<MemberInfo, string> MemberInfoNameCache = new();

        private static readonly ConcurrentDictionary<Type, Type?> ListAddMethodCache = new();

        private readonly object? _target;
        private readonly Type _targetType;
        private readonly bool _includeNonPublic;
        private readonly JsonSerializerCase _jsonSerializerCase;

        private Converter(object? source, Type targetType, ref object? targetInstance, bool includeNonPublic, JsonSerializerCase jsonSerializerCase)
        {
            _targetType = targetInstance != null ? targetInstance.GetType() : targetType;
            _includeNonPublic = includeNonPublic;
            _jsonSerializerCase = jsonSerializerCase;

            if (source is null)
            {
                return;
            }

            var sourceType = source.GetType();
            if (_targetType == typeof(object))
                _targetType = sourceType;

            if (sourceType == _targetType)
            {
                _target = source;
                return;
            }

            if (!TrySetInstance(targetInstance, source, ref _target))
                return;

            ResolveObject(source, ref _target);
        }

        internal static object? FromJsonResult(object? source, JsonSerializerCase jsonSerializerCase, Type? targetType = null, bool includeNonPublic = false)
        {
            object? nullRef = null;
            return new Converter(source, targetType ?? typeof(object), ref nullRef, includeNonPublic, jsonSerializerCase).GetResult();
        }

        private static object? FromJsonResult(object source, Type targetType, ref object? targetInstance, bool includeNonPublic)
            => new Converter(source, targetType, ref targetInstance, includeNonPublic, JsonSerializerCase.None).GetResult();

        private static Type? GetAddMethodParameterType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type targetType)
        {
            // Cache the parameter type of the Add method.
            return ListAddMethodCache.GetOrAdd(targetType, type =>
            {
                var addMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                     .FirstOrDefault(m => m.Name == AddMethodName && m.GetParameters().Length == 1);
                return addMethod?.GetParameters()[0].ParameterType;
            });
        }

        private static void GetByteArray(string sourceString, ref object? target)
        {
            try
            {
                target = Convert.FromBase64String(sourceString);
            }
            catch (FormatException)
            {
                target = Encoding.UTF8.GetBytes(sourceString);
            }
        }

        private object? GetSourcePropertyValue(IDictionary<string, object> sourceProperties, MemberInfo targetMember)
        {
            // Cache the target name (either from a JsonPropertyAttribute or the member name)
            var targetName = MemberInfoNameCache.GetOrAdd(targetMember,
                x => AttributeCache.DefaultCache.Value.RetrieveOne<JsonPropertyAttribute>(x)?.PropertyName
                     ?? x.Name.GetNameWithCase(_jsonSerializerCase));
            return sourceProperties.TryGetValue(targetName, out var val) ? val : null;
        }

        private bool TrySetInstance(object? targetInstance, object source, ref object? target)
        {
            if (targetInstance is null)
            {
                try
                {
                    source.CreateTarget(_targetType, _includeNonPublic, ref target);
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                target = targetInstance;
            }
            return true;
        }

        private object? GetResult() => _target ?? _targetType.GetDefault();

        private void ResolveObject(object source, ref object? target)
        {
            switch (source)
            {
                case string s when _targetType == typeof(byte[]):
                    GetByteArray(s, ref target);
                    break;

                case Dictionary<string, object> dict when target is IDictionary targetDict:
                    PopulateDictionary(dict, targetDict);
                    break;

                case Dictionary<string, object> dict:
                    PopulateObject(dict);
                    break;

                case List<object> list when target is Array targetArray:
                    PopulateArray(list, targetArray);
                    break;

                case List<object> list when target is IList targetList:
                    PopulateIList(list, targetList);
                    break;

                default:
                    {
                        var sourceStringValue = source.ToStringInvariant();
                        if (!_targetType.TryParseBasicType(sourceStringValue, out target))
                        {
                            GetEnumValue(sourceStringValue, ref target);
                        }
                    }
                    break;
            }
        }

        private void PopulateIList(IEnumerable<object> objects, IList list)
        {
            var parameterType = GetAddMethodParameterType(_targetType);
            if (parameterType is null) return;

            foreach (var item in objects)
            {
                try
                {
                    list.Add(FromJsonResult(item, _jsonSerializerCase, parameterType, _includeNonPublic));
                }
                catch
                {
                    // Ignored
                }
            }
        }

        private void PopulateArray(List<object> objects, Array array)
        {
            var elementType = _targetType.GetElementType();
            for (var i = 0; i < objects.Count; i++)
            {
                try
                {
                    var targetItem = FromJsonResult(objects[i], _jsonSerializerCase, elementType, _includeNonPublic);
                    array.SetValue(targetItem, i);
                }
                catch
                {
                    // Ignored
                }
            }
        }

        private void GetEnumValue(string sourceStringValue, ref object? target)
        {
            var enumType = Nullable.GetUnderlyingType(_targetType) ?? (_targetType.IsEnum ? _targetType : null);
            if (enumType is null) return;

            try
            {
                target = Enum.Parse(enumType, sourceStringValue);
            }
            catch
            {
                // Ignored
            }
        }

        private void PopulateDictionary(IDictionary<string, object> sourceProperties, IDictionary targetDictionary)
        {
            // Locate an Add method that accepts (string, T) parameters.
            var addMethod = _targetType.GetMethods().FirstOrDefault(m =>
                m is { Name: AddMethodName, IsPublic: true } &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType == typeof(string));

            if (addMethod is null) return;

            var targetEntryType = addMethod.GetParameters()[1].ParameterType;
            foreach (var kvp in sourceProperties)
            {
                try
                {
                    var targetEntryValue = FromJsonResult(kvp.Value, _jsonSerializerCase, targetEntryType, _includeNonPublic);
                    targetDictionary.Add(kvp.Key, targetEntryValue);
                }
                catch
                {
                    // Ignored
                }
            }
        }

        private void PopulateObject(IDictionary<string, object>? sourceProperties)
        {
            if (sourceProperties is null) return;
            if (_targetType.IsValueType)
                PopulateFields(sourceProperties);

            PopulateProperties(sourceProperties);
        }

        private void PopulateProperties(IDictionary<string, object> sourceProperties)
        {
            // Retrieve writable properties from the cache.
            var properties = PropertyTypeCache.DefaultCache.Value
                .RetrieveFilteredProperties(_targetType, false, p => p.CanWrite);

            foreach (var property in properties)
            {
                var sourceValue = GetSourcePropertyValue(sourceProperties, property);
                if (sourceValue is null) continue;

                try
                {
                    // For arrays, no need to retrieve the current value.
                    object? currentValue = property.PropertyType.IsArray ? null : _target.ReadProperty(property.Name);
                    var targetValue = FromJsonResult(sourceValue, property.PropertyType, ref currentValue, _includeNonPublic);
                    if (targetValue != null)
                        _target.WriteProperty(property.Name, targetValue);
                }
                catch
                {
                    // Ignored
                }
            }
        }

        private void PopulateFields(IDictionary<string, object> sourceProperties)
        {
            foreach (var field in FieldTypeCache.DefaultCache.Value.RetrieveAllFields(_targetType))
            {
                var sourceValue = GetSourcePropertyValue(sourceProperties, field);
                if (sourceValue is null) continue;

                try
                {
                    var targetValue = FromJsonResult(sourceValue, _jsonSerializerCase, field.FieldType, _includeNonPublic);
                    field.SetValue(_target, targetValue);
                }
                catch
                {
                    // Ignored
                }
            }
        }
    }
}
