using System.Collections.Generic;
using System.Linq;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Memory.Pools;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

public sealed partial class MemoryTests
{
    [Fact]
    public void Add_ObjectMapContainsEntries_UpdatesCollectionMembers()
    {
        ObjectMap<string, int> map = new()
        {
            { "one", 1 },
            new KeyValuePair<string, int>("two", 2)
        };

        map["three"] = 3;
        bool containsPair = map.Contains(new KeyValuePair<string, int>("two", 2));
        bool containsKey = map.ContainsKey("one");
        bool tryGetValue = map.TryGetValue("three", out int value);
        KeyValuePair<string, int>[] copied = new KeyValuePair<string, int>[4];
        map.CopyTo(copied, 1);

        Assert.Equal(3, map.Count);
        Assert.False(map.IsReadOnly);
        Assert.Equal(3, map["three"]);
        Assert.True(containsPair);
        Assert.True(containsKey);
        Assert.True(tryGetValue);
        Assert.Equal(3, value);
        Assert.Equal(3, map.Keys.Count);
        Assert.Equal(3, map.Values.Count);
        Assert.Equal(3, map.ToArray().Length);
        Assert.Contains(copied, static item => item.Key == "one");
    }

    [Fact]
    public void ResetForPool_ObjectMapHasEntries_ClearsMap()
    {
        ObjectMap<string, int> map = ObjectMap<string, int>.Rent();
        map.Add("one", 1);
        map.Add("two", 2);

        bool removedByKey = map.Remove("one");
        bool removedByPair = map.Remove(new KeyValuePair<string, int>("two", 2));
        map.ResetForPool();
        map.Return();

        Assert.True(removedByKey);
        Assert.True(removedByPair);
        Assert.Empty(map);
    }

    [Fact]
    public void Rent_ListPoolLifecycle_UpdatesStatistics()
    {
        ListPool<int> pool = new(maxPoolSize: 4, initialCapacity: 2);

        List<int> list = pool.Rent(3);
        list.AddRange([1, 2, 3]);
        pool.Return(list);
        Dictionary<string, object> stats = pool.GetStatistics();

        Assert.True(pool.AvailableCount >= 1);
        Assert.True(pool.CreatedCount >= 1);
        Assert.Equal(1L, pool.TotalRentOperations);
        Assert.Equal(1L, pool.TotalReturnOperations);
        Assert.Equal(0L, pool.RentedCount);
        Assert.True(pool.UptimeMs >= 0);
        Assert.Equal(pool.AvailableCount, stats["AvailableCount"]);
        Assert.NotNull(ListPool<int>.Instance);
    }

    [Fact]
    public void Prealloc_ListPoolStateChanges_TrimClearAndResetStatisticsWork()
    {
        ListPool<int> pool = new(maxPoolSize: 4, initialCapacity: 2);

        pool.Prealloc(3, 5);
        int trimmed = pool.Trim(1);
        int cleared = pool.Clear();
        pool.ResetStatistics();

        Assert.True(trimmed >= 0);
        Assert.True(cleared >= 0);
        Assert.Equal(0L, pool.CreatedCount);
        Assert.Equal(0L, pool.TotalRentOperations);
        Assert.Equal(0L, pool.TotalReturnOperations);
        Assert.Equal(0L, pool.TrimmedCount);
    }
}
