using Notio.Web.Sessions;
using Notio.Web.Utilities;
using System;
using System.Collections.Generic;

namespace Notio.Sessions;

public partial class LocalSessionManager
{
    private class SessionImpl : ISession
    {
        private readonly Dictionary<string, object> _data = new(Session.KeyComparer);

        private int _usageCount;

        public SessionImpl(string id, TimeSpan duration)
        {
            Id = Validate.NotNullOrEmpty(nameof(id), id);
            Duration = duration;
            LastActivity = DateTime.UtcNow;
            _usageCount = 1;
        }

        public string Id { get; }

        public TimeSpan Duration { get; }

        public DateTime LastActivity { get; private set; }

        public int Count
        {
            get
            {
                lock (_data)
                {
                    return _data.Count;
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (_data)
                {
                    return _data.Count == 0;
                }
            }
        }

        public object this[string key]
        {
            get
            {
                lock (_data)
                {
                    return _data.TryGetValue(key, out object? value) ? value : null!;
                }
            }
            set
            {
                lock (_data)
                {
                    if (value == null)
                    {
                        _ = _data.Remove(key);
                    }
                    else
                    {
                        _data[key] = value;
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_data)
            {
                _data.Clear();
            }
        }

        public bool ContainsKey(string key)
        {
            lock (_data)
            {
                return _data.ContainsKey(key);
            }
        }

#pragma warning disable CS8767

        public bool TryRemove(string key, out object? value)
#pragma warning restore CS8767
        {
            lock (_data)
            {
                if (!_data.TryGetValue(key, out value))
                {
                    value = null;  // Gán null cho value nếu không tìm thấy key
                    return false;
                }

                _ = _data.Remove(key);
                return true;
            }
        }

        public IReadOnlyList<KeyValuePair<string, object>> TakeSnapshot()
        {
            lock (_data)
            {
                return [.. _data];
            }
        }

#pragma warning disable CS8767

        public bool TryGetValue(string key, out object? value)
#pragma warning restore CS8767
        {
            lock (_data)
            {
                return _data.TryGetValue(key, out value);
            }
        }

        internal void BeginUse()
        {
            lock (_data)
            {
                _usageCount++;
                LastActivity = DateTime.UtcNow;
            }
        }

        internal void EndUse(Action unregister)
        {
            lock (_data)
            {
                --_usageCount;
                UnregisterIfNeededCore(unregister);
            }
        }

        internal void UnregisterIfNeeded(Action unregister)
        {
            lock (_data)
            {
                UnregisterIfNeededCore(unregister);
            }
        }

        private void UnregisterIfNeededCore(Action unregister)
        {
            if (_usageCount < 1 && (IsEmpty || DateTime.UtcNow > LastActivity + Duration))
            {
                unregister();
            }
        }
    }
}