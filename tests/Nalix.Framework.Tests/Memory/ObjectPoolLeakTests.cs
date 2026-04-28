
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

    [Fact]
    public void GetMultiple_ShouldReturnAcquiredObjects_OnException()
    {
        var pool = new ObjectPool(10);
        
        // 1. Pre-fill the pool with 5 healthy objects
        CreationThrowingPoolable.ShouldThrowInNextAction = false;
        pool.Prealloc<CreationThrowingPoolable>(5);
        
        Assert.Equal(5, pool.TotalAvailableCount);
        
        // 2. Make the next creation throw
        CreationThrowingPoolable.ShouldThrowInNextAction = true;
        
        // 3. Try to get 10 objects (5 from pool, 6th will fail)
        // new T() constraint might throw TargetInvocationException if it uses reflection
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
        Assert.Equal(5, (int)info["AvailableCount"]);
        
        // Cleanup
        CreationThrowingPoolable.ShouldThrowInNextAction = false;
    }
}
