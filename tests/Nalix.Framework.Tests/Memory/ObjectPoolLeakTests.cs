
using System;
using System.Collections.Generic;
using System.Reflection;
using Nalix.Abstractions;
using Nalix.Framework.Memory.Pools;
using Nalix.Framework.Memory.Objects;
using Xunit;

public class ObjectPoolLeakTests
{
    public class CreationThrowingPoolable : IPoolable
    {
        public static bool ShouldThrowInNextAction = false;
        
        public CreationThrowingPoolable()
        {
            if (ShouldThrowInNextAction) throw new InvalidOperationException("Simulated OOM");
        }
        
        public void ResetForPool() { }
    }

    private static object? PopThreadLocalCache<T>()
    {
        try
        {
            var type = typeof(Nalix.Framework.Memory.Objects.ObjectPoolManager).Assembly
                .GetType("Nalix.Framework.Memory.Internal.PoolTypes.ThreadLocalCache`1")
                ?.MakeGenericType(typeof(T));
            FieldInfo? valueField = type?.GetField("t_value", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo? ownerField = type?.GetField("t_owner", BindingFlags.NonPublic | BindingFlags.Static);
            object? value = valueField?.GetValue(null);
            valueField?.SetValue(null, null);
            ownerField?.SetValue(null, null);
            return value;
        }
        catch
        {
            return null;
        }
    }

    [Fact]
    public void GetMultiple_ShouldReturnAcquiredObjects_OnException()
    {
        // Clear any leftover thread-local cache for this test run
        _ = PopThreadLocalCache<CreationThrowingPoolable>();

        var pool = new ObjectPool(10);
        
        // 1. Pre-fill the pool with 5 healthy objects
        CreationThrowingPoolable.ShouldThrowInNextAction = false;
        pool.Prealloc<CreationThrowingPoolable>(5);
        
        Assert.Equal(5, pool.TotalAvailableCount);
        
        // 2. Make the next creation throw
        CreationThrowingPoolable.ShouldThrowInNextAction = true;
        
        // 3. Try to get 10 objects (5 from pool, 6th will fail)
        var ex = Record.Exception(() => pool.GetMultiple<CreationThrowingPoolable>(10));
        Assert.NotNull(ex);
        
        // 4. Verify that the 5 objects taken from the pool were RETURNED
        Assert.Equal(5, pool.TotalAvailableCount);
        
        // Cleanup
        CreationThrowingPoolable.ShouldThrowInNextAction = false;
    }

    [Fact]
    public void TypedGetMultiple_ShouldReturnAcquiredObjects_OnException()
    {
        // Clear any leftover thread-local cache for this test run
        _ = PopThreadLocalCache<CreationThrowingPoolable>();

        var manager = new ObjectPoolManager();
        var pool = manager.GetTypedPool<CreationThrowingPoolable>();
        
        // 1. Pre-fill the pool with 5 healthy objects
        CreationThrowingPoolable.ShouldThrowInNextAction = false;
        pool.Prealloc(5);
        
        var info = pool.GetInfo();
        Assert.Equal(5, (int)info["AvailableCount"]);
        
        // 2. Make the next creation throw
        CreationThrowingPoolable.ShouldThrowInNextAction = true;
        
        // 3. Try to get 10 objects (5 from pool, 6th will fail)
        var ex = Record.Exception(() => pool.GetMultiple(10));
        Assert.NotNull(ex);
        
        // 4. Verify that the 5 objects taken from the pool were RETURNED
        info = pool.GetInfo();
        int available = (int)info["AvailableCount"];
        if (PopThreadLocalCache<CreationThrowingPoolable>() != null)
        {
            available++;
        }
        Assert.Equal(5, available);
        
        // Cleanup
        CreationThrowingPoolable.ShouldThrowInNextAction = false;
    }
}
