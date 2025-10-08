using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Nalix.Network.Internal.Compilation;

/// <summary>
/// Provides helper methods to construct open delegate types (Func/Action)
/// that match a target <see cref="MethodInfo"/> signature.
/// </summary>
internal static class MethodInfoDelegateTypeExtensions
{
    /// <summary>
    /// Builds a delegate <see cref="System.Type"/> for a static method, matching its parameter list
    /// and return type. Uses <see cref="Expression.GetActionType(System.Type[])"/> for <c>void</c> and
    /// <see cref="Expression.GetDelegateType(System.Type[])"/> otherwise.
    /// </summary>
    /// <param name="mi">The static method metadata.</param>
    /// <returns>A <see cref="System.Type"/> representing an <c>Action&lt;...&gt;</c> or <c>Func&lt;...&gt;</c> shape.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="mi"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when the method is not static.</exception>
    /// <exception cref="System.NotSupportedException">Thrown for methods with ref/out parameters or too many parameters.</exception>
    internal static System.Type CreateDelegateTypeForStatic(this MethodInfo mi)
    {
        System.ArgumentNullException.ThrowIfNull(mi);

        if (!mi.IsStatic)
        {
            throw new System.ArgumentException("MethodInfo must be static.", nameof(mi));
        }

        var paramTypes = mi.GetParameters();

        // Disallow ref/out for this generic delegate approach.
        if (paramTypes.Any(p => p.ParameterType.IsByRef))
        {
            throw new System.NotSupportedException("Ref/out parameters are not supported for generated delegate type.");
        }

        // Build the type list: [p1, p2, ..., (return?)]
        var types = paramTypes.Select(p => p.ParameterType).ToList();

        // Expression.GetActionType/GetDelegateType limit: up to 16 parameters (Action) / 17 including return (Func).
        // This is more than enough for handlers here.
        if (mi.ReturnType == typeof(void))
        {
            EnsureParameterLimit(types.Count, isVoid: true);
            return Expression.GetActionType([.. types]);
        }
        else
        {
            EnsureParameterLimit(types.Count + 1, isVoid: false); // +1 for return type
            types.Add(mi.ReturnType);
            return Expression.GetDelegateType([.. types]);
        }
    }

    /// <summary>
    /// Builds a delegate <see cref="System.Type"/> for an instance method as an open-instance delegate,
    /// i.e., the first parameter is the declaring instance (<c>this</c>), followed by the method parameters.
    /// Uses <see cref="Expression.GetActionType(System.Type[])"/> for <c>void</c> and
    /// <see cref="Expression.GetDelegateType(System.Type[])"/> otherwise.
    /// </summary>
    /// <param name="mi">The instance method metadata.</param>
    /// <returns>A <see cref="System.Type"/> representing an open-instance <c>Action&lt;TDeclaring,...&gt;</c> or <c>Func&lt;TDeclaring,...,TReturn&gt;</c>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="mi"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when the method is static or has no declaring type.</exception>
    /// <exception cref="System.NotSupportedException">Thrown for methods with ref/out parameters or too many parameters.</exception>
    internal static System.Type CreateDelegateTypeForInstance(this MethodInfo mi)
    {
        System.ArgumentNullException.ThrowIfNull(mi);

        if (mi.IsStatic)
        {
            throw new System.ArgumentException("MethodInfo must be an instance method.", nameof(mi));
        }

        if (mi.DeclaringType is null)
        {
            throw new System.ArgumentException("MethodInfo must have a declaring type.", nameof(mi));
        }

        var paramTypes = mi.GetParameters();

        if (paramTypes.Any(p => p.ParameterType.IsByRef))
        {
            throw new System.NotSupportedException("Ref/out parameters are not supported for generated delegate type.");
        }

        // Open instance: prepend 'this' (declaring type)
        var types = new System.Collections.Generic.List<System.Type>(1 + paramTypes.Length)
        {
            mi.DeclaringType
        };
        types.AddRange(paramTypes.Select(p => p.ParameterType));

        if (mi.ReturnType == typeof(void))
        {
            EnsureParameterLimit(types.Count, isVoid: true);
            return Expression.GetActionType([.. types]);
        }
        else
        {
            EnsureParameterLimit(types.Count + 1, isVoid: false);
            types.Add(mi.ReturnType);
            return Expression.GetDelegateType([.. types]);
        }
    }

    /// <summary>
    /// Validates parameter count limits of <see cref="Expression.GetActionType(System.Type[])"/> and
    /// <see cref="Expression.GetDelegateType(System.Type[])"/>.
    /// </summary>
    /// <param name="arity">
    /// For <c>Action</c>: number of parameters.
    /// For <c>Func</c>: number of parameters + 1 (the return type).
    /// </param>
    /// <param name="isVoid">True if building an Action-type; false for Func-type.</param>
    private static void EnsureParameterLimit(System.Int32 arity, System.Boolean isVoid)
    {
        // As of .NET, Action supports up to 16 parameters, Func supports up to 16 parameters + return (i.e., 17 types).
        // Expression.GetActionType/GetDelegateType map to those generic definitions.
        const System.Int32 actionMax = 16;
        const System.Int32 funcMaxTypes = 17; // N params + return = up to 17 types

        if (isVoid)
        {
            if (arity > actionMax)
            {
                throw new System.NotSupportedException($"Too many parameters for Action<>: {arity} > {actionMax}.");
            }
        }
        else
        {
            if (arity > funcMaxTypes)
            {
                throw new System.NotSupportedException($"Too many parameters for Func<> (including return): {arity} > {funcMaxTypes}.");
            }
        }
    }
}
