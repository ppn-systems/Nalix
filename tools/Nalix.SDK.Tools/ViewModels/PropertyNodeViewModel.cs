// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Primitives;
using Nalix.Framework.Identifiers;
using Nalix.SDK.Tools.Models;
using Nalix.SDK.Tools.Services;

namespace Nalix.SDK.Tools.ViewModels;

/// <summary>
/// Represents one editable reflected property in the packet editor form.
/// </summary>
public sealed class PropertyNodeViewModel : ViewModelBase
{
    private readonly object _owner;
    private readonly PropertyInfo _property;
    private readonly Action _onValueChanged;
    private string _errorText = string.Empty;

    private PropertyNodeViewModel(
        object owner,
        PropertyInfo property,
        PacketPropertyDefinition definition,
        bool isReadOnly,
        bool editableHeaders,
        Action onValueChanged)
    {
        _owner = owner;
        _property = property;
        _onValueChanged = onValueChanged;

        this.Name = definition.Name;
        this.DisplayName = definition.DisplayName;
        this.PropertyType = property.PropertyType;
        this.EditorKind = definition.EditorKind;
        this.IsHeader = definition.IsHeader;
        this.IsReadOnly = isReadOnly ||
                          property.SetMethod is null ||
                          string.Equals(definition.Name, "MagicNumber", StringComparison.Ordinal) ||
                          (definition.IsHeader && !editableHeaders);
        this.TypeDisplayName = this.GetDisplayTypeName(property.PropertyType);
        this.EnumValues = this.ResolveEnumValues(property.PropertyType);

        if (this.EditorKind == EditorKind.Complex)
        {
            object? childOwner = _property.GetValue(_owner);
            if (childOwner is null && !this.IsReadOnly)
            {
                childOwner = this.TryCreateInstance(this.GetEffectiveType(property.PropertyType));
                if (childOwner is not null)
                {
                    _property.SetValue(_owner, childOwner);
                }
            }

            if (childOwner is not null)
            {
                foreach (PropertyNodeViewModel child in CreateNodes(childOwner, definition.Children, this.IsReadOnly, true, onValueChanged))
                {
                    this.Children.Add(child);
                }
            }
        }
    }

    public string Name { get; }

    public string DisplayName { get; }

    public Type PropertyType { get; }

    public string TypeDisplayName { get; }

    public EditorKind EditorKind { get; }

    public bool IsHeader { get; }

    public bool IsReadOnly { get; }

    public ObservableCollection<PropertyNodeViewModel> Children { get; } = [];

    public IReadOnlyList<object> EnumValues { get; }

    public bool HasChildren => this.Children.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(_errorText);

    public string ErrorText
    {
        get => _errorText;
        private set
        {
            if (this.SetProperty(ref _errorText, value))
            {
                this.OnPropertyChanged(nameof(this.HasError));
            }
        }
    }

    public string TextValue
    {
        get => this.FormatValue(this.GetValue());
        set => this.SetTextValue(value);
    }

    public bool BooleanValue
    {
        get => this.GetValue() is bool booleanValue && booleanValue;
        set => this.SetValue(value);
    }

    public object? SelectedEnumValue
    {
        get => this.GetValue();
        set
        {
            if (value is not null)
            {
                this.SetValue(value);
            }
        }
    }

    public byte[] ByteArrayValue
    {
        get
        {
            object? value = this.GetValue();
            if (value is byte[] bytes)
            {
                return bytes;
            }

            if (value is Bytes32 b32)
            {
                return b32.ToByteArray();
            }

            if (value is Snowflake sf)
            {
                byte[] buf = new byte[7];
                _ = sf.TryWriteBytes(buf);
                return buf;
            }

            return Array.Empty<byte>();
        }
        set
        {
            if (this.PropertyType == typeof(byte[]))
            {
                this.SetValue(value ?? Array.Empty<byte>());
            }
            else if (this.PropertyType == typeof(Bytes32))
            {
                this.SetValue(value is { Length: >= 32 } ? new Bytes32(value) : Bytes32.Zero);
            }
            else if (this.PropertyType == typeof(Snowflake))
            {
                this.SetValue(value is { Length: >= 7 } ? Snowflake.FromBytes(value) : Snowflake.Empty);
            }
        }
    }

    public static ObservableCollection<PropertyNodeViewModel> CreateNodes(
        object owner,
        IReadOnlyList<PacketPropertyDefinition> definitions,
        bool isReadOnly,
        bool editableHeaders,
        Action onValueChanged)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(onValueChanged);

        ObservableCollection<PropertyNodeViewModel> nodes = [];

        foreach (PacketPropertyDefinition definition in definitions)
        {
            PropertyInfo? property = owner.GetType().GetProperty(definition.Name, BindingFlags.Public | BindingFlags.Instance);
            if (property?.GetMethod is null)
            {
                continue;
            }

            nodes.Add(new PropertyNodeViewModel(owner, property, definition, isReadOnly, editableHeaders, onValueChanged));
        }

        return nodes;
    }

    private object? GetValue() => _property.GetValue(_owner);

    private void SetValue(object? value)
    {
        if (this.IsReadOnly || _property.SetMethod is null)
        {
            return;
        }

        _property.SetValue(_owner, value);
        this.ErrorText = string.Empty;
        this.RefreshBindings();
        _onValueChanged();
    }

    private void SetTextValue(string? value)
    {
        Type effectiveType = this.GetEffectiveType(this.PropertyType);

        if (effectiveType == typeof(string))
        {
            this.SetValue(value ?? string.Empty);
            return;
        }

        if (this.TryConvertFromText(value, out object? convertedValue, out string errorText))
        {
            this.SetValue(convertedValue);
            this.ErrorText = string.Empty;
            return;
        }

        this.ErrorText = errorText;
        this.OnPropertyChanged(nameof(this.TextValue));
    }

    private bool TryConvertFromText(string? text, out object? value, out string errorText)
    {
        Type effectiveType = this.GetEffectiveType(this.PropertyType);
        bool isNullable = Nullable.GetUnderlyingType(this.PropertyType) is not null;
        string normalized = text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(normalized))
        {
            if (isNullable)
            {
                value = null;
                errorText = string.Empty;
                return true;
            }

            value = null;
            errorText = string.Format(CultureInfo.CurrentCulture, ToolResourceHelper.GetTexts().PropertyValueCannotBeEmptyFormat, this.DisplayName);
            return false;
        }

        if (effectiveType == typeof(byte) && byte.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte byteValue))
        {
            value = byteValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(sbyte) && sbyte.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte signedByteValue))
        {
            value = signedByteValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(short) && short.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out short shortValue))
        {
            value = shortValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(ushort) && ushort.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort ushortValue))
        {
            value = ushortValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(int) && int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
        {
            value = intValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(uint) && uint.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint uintValue))
        {
            value = uintValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(long) && long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
        {
            value = longValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(ulong) && ulong.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong ulongValue))
        {
            value = ulongValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(float) && float.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float floatValue))
        {
            value = floatValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(double) && double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue))
        {
            value = doubleValue;
            errorText = string.Empty;
            return true;
        }

        if (effectiveType == typeof(decimal) && decimal.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimal decimalValue))
        {
            value = decimalValue;
            errorText = string.Empty;
            return true;
        }

        TypeConverter converter = TypeDescriptor.GetConverter(effectiveType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                value = converter.ConvertFromInvariantString(normalized);
                errorText = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                value = null;
                errorText = exception.Message;
                return false;
            }
        }

        value = null;
        errorText = string.Format(CultureInfo.CurrentCulture, ToolResourceHelper.GetTexts().PropertyTypeNotSupportedFormat, effectiveType.Name);
        return false;
    }

    private void RefreshBindings()
    {
        this.OnPropertyChanged(nameof(this.TextValue));
        this.OnPropertyChanged(nameof(this.BooleanValue));
        this.OnPropertyChanged(nameof(this.SelectedEnumValue));
        this.OnPropertyChanged(nameof(this.ByteArrayValue));
    }

    private string FormatValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (string.Equals(this.Name, "MagicNumber", StringComparison.Ordinal) && value is uint magicNumber)
        {
            return $"0x{magicNumber:X8}";
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private IReadOnlyList<object> ResolveEnumValues(Type type)
    {
        Type effectiveType = this.GetEffectiveType(type);
        return effectiveType.IsEnum
            ? [.. Enum.GetValues(effectiveType).Cast<object>()]
            : Array.Empty<object>();
    }

    private Type GetEffectiveType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private object? TryCreateInstance(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    private string GetDisplayTypeName(Type type)
    {
        Type effectiveType = this.GetEffectiveType(type);
        if (effectiveType == typeof(byte[]))
        {
            return ToolResourceHelper.GetTexts().ByteArrayTypeName;
        }

        return effectiveType.Name;
    }
}
