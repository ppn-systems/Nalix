# BufferAllocator Class Documentation

## Overview

The `BufferAllocator` class is responsible for managing buffers of different sizes, handling dynamic allocation and deallocation of these buffers. It optimizes the buffer pool size and ensures efficient memory management for performance-sensitive applications, such as game servers or high-performance systems.

## Class Definition

```csharp
public sealed class BufferAllocator : IBufferPool
```

## Constants

`MinimumIncrease`

- Type: `int`
- Description: The minimum number of buffers to increase when resizing the pool.

`MaxBufferIncreaseLimit`

- Type: `int`
- Description: The maximum limit for increasing buffer capacity.

### Properties

`BufferConfig`

- Type: `BufferConfig`
- Description: Retrieves the buffer configuration from the system configuration manager.

`MaxBufferSize`

- Type: `int`
- Description: Returns the largest buffer size in the buffer allocations list.

### Constructor

```csharp
BufferAllocator(BufferConfig? bufferConfig = null, ILogger? logger = null)
```

- Description: Initializes a new instance of the BufferAllocator class with optional buffer configuration and logger.
- Parameters:

  - bufferConfig (optional): Custom buffer configuration. Defaults to system configuration.
  - logger (optional): Custom logger for tracing.

## Methods

```csharp
AllocateBuffers()
```

- Description: Allocates buffers based on the configuration settings.
- Exception: Throws InvalidOperationException if buffers have already been allocated.

```csharp
Rent(int size = 1024)
```

- Description: Rents a buffer of at least the requested size.
- Parameters:
  - size: The size of the buffer to rent. Default is 1024.
- Returns: A byte array representing the rented buffer.

```csharp
Return(byte[] buffer)
```

- Description: Returns the buffer to the appropriate pool.
- Parameters:
  - buffer: The buffer to return.

```csharp
GetAllocationForSize(int size)
```

- Description: Retrieves the allocation ratio for a given buffer size.
- Parameters:
  - size: The size of the buffer.
- Returns: The allocation ratio for the buffer size.
- Exception: Throws ArgumentException if no allocation ratio is found for the buffer size.

```csharp
ParseBufferAllocations(string bufferAllocationsString)
```

- Description: Parses the buffer allocation settings from a configuration string.
- Parameters:
  - bufferAllocationsString: A semicolon-separated list of buffer size and allocation ratio pairs.
- Returns: An array of tuples representing the buffer size and allocation ratio.
- Exception: Throws ArgumentException if the input string is malformed.

```csharp
ShrinkBufferPoolSize(BufferPoolShared pool)
```

- Description: Shrinks the buffer pool size using an optimal algorithm.
- Parameters:
  - pool: The buffer pool to shrink.

```csharp
IncreaseBufferPoolSize(BufferPoolShared pool)
```

- Description: Increases the buffer pool size using an optimal algorithm.
- Parameters:
  - pool: The buffer pool to increase.

```csharp
Dispose()
```

- Description: Releases all resources associated with the buffer pools.

### Event Handling

#### EventShrink

- Description: Event triggered when the buffer pool size needs to be shrunk. This is managed by the BufferManager.

#### EventIncrease

- Description: Event triggered when the buffer pool size needs to be increased. This is managed by the BufferManager.

Configuration String Format
The buffer allocation string should be formatted as follows:
<buffer_size>,<allocation_ratio>;
<buffer_size>,<allocation_ratio>;
...

## Example

`1024,0.40;2048,0.60`
In this example:

- A buffer size of 1024 will have 40% of the total buffer count allocated.
- A buffer size of 2048 will have 60% of the total buffer count allocated.
