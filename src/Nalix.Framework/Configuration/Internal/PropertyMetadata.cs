// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Tests.")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Configuration.Internal;

/// <summary>
/// Stores metadata about a configuration property.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("{Naming,nq} ({PropertyType.Naming})")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal class PropertyMetadata
{
    #region Properties

    /// <summary>
    /// Gets or sets the type code of the property.
    /// </summary>
    public System.TypeCode TypeCode { get; init; }

    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.NotNull]
    public System.String Name { get; init; } = null!;

    /// <summary>
    /// Gets or sets the type of the property.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.NotNull]
    public System.Type PropertyType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the property information.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.NotNull]
    public System.Reflection.PropertyInfo PropertyInfo { get; init; } = null!;

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Sets the value of this property on the specified target object.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="value">The value to set.</param>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void SetValue(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Object target,
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.Object? value)
    {
        // Only set if the types are compatible
        if (value == null || PropertyType.IsInstanceOfType(value))
        {
            PropertyInfo.SetValue(target, value);
        }
        else
        {
            throw new System.InvalidOperationException(
                $"Type mismatch for property {Name}: " +
                $"Expected {PropertyType}, but got {value.GetType()}");
        }
    }

    #endregion Public Methods
}
