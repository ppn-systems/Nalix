using System;
using System.Collections;
using System.Collections.Generic;

namespace Notio.Lite.Collections;

internal struct CollectionEnumerator(CollectionProxy proxy) : IDictionaryEnumerator, IEnumerator<object?>
{
    private int _currentIndex = -1;

    public readonly DictionaryEntry Entry => _currentIndex >= 0 ? new(Key, Value) : default;

    public readonly object Key => _currentIndex;

    public readonly object? Value => _currentIndex >= 0 ? proxy[_currentIndex] : default;

    public object? Current => _currentIndex >= 0 ? Entry : default;

    public bool MoveNext()
    {
        var elementCount = proxy.Count;
        _currentIndex++;
        if (_currentIndex < elementCount)
            return true;

        _currentIndex = elementCount - 1;
        return false;
    }

    public void Reset() => _currentIndex = -1;

    void IDisposable.Dispose()
    {
        // placeholder
    }
}