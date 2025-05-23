namespace Nalix.Serialization.Internal.Sequence;

// Dùng để xây dựng ReadOnlySequence<byte> từ nhiều ReadOnlyMemory<byte>
internal sealed class SequenceBuilder
{
    private readonly System.Collections.Generic.List<Segment> list;
    private readonly System.Collections.Generic.Stack<Segment> segmentPool;

    public SequenceBuilder()
    {
        list = [];
        segmentPool = new System.Collections.Generic.Stack<Segment>();
    }

    // Thêm một đoạn dữ liệu vào SequenceBuilder
    public void Add(System.ReadOnlyMemory<byte> buffer, bool returnToPool)
    {
        // Lấy Segment từ pool hoặc tạo mới nếu không có
        if (!segmentPool.TryPop(out var segment))
        {
            segment = new Segment();
        }

        segment.SetBuffer(buffer, returnToPool);
        list.Add(segment);
    }

    // Nếu chỉ có một đoạn, trả về trực tiếp buffer để tránh tạo sequence
    public bool TryGetSingleMemory(out System.ReadOnlyMemory<byte> memory)
    {
        if (list.Count == 1)
        {
            memory = list[0].Memory;
            return true;
        }
        memory = default;
        return false;
    }

    // Xây dựng ReadOnlySequence<byte> từ các đoạn đã thêm
    public System.Buffers.ReadOnlySequence<byte> Build()
    {
        if (list.Count == 0)
        {
            return System.Buffers.ReadOnlySequence<byte>.Empty;
        }

        if (list.Count == 1)
        {
            return new System.Buffers.ReadOnlySequence<byte>(list[0].Memory);
        }

        long running = 0;
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);

        // Gán chỉ số và liên kết các segment lại với nhau
        for (int i = 0; i < span.Length; i++)
        {
            var next = i < span.Length - 1 ? span[i + 1] : null;
            span[i].SetRunningIndexAndNext(running, next);
            running += span[i].Memory.Length;
        }

        var firstSegment = span[0];
        var lastSegment = span[^1];

        return new System.Buffers.ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length);
    }

    // Đưa SequenceBuilder về trạng thái ban đầu và tái sử dụng các segment
    public void Reset()
    {
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);

        foreach (var item in span)
        {
            item.Reset();
            segmentPool.Push(item); // Đưa lại segment vào pool
        }
        list.Clear();
    }

    // Segment đại diện cho một đoạn trong ReadOnlySequence<byte>
    private class Segment : System.Buffers.ReadOnlySequenceSegment<byte>
    {
        private bool returnToPool;

        public Segment()
        {
            returnToPool = false;
        }

        // Gán dữ liệu buffer và cờ xác định có trả về pool không
        public void SetBuffer(System.ReadOnlyMemory<byte> buffer, bool returnToPool)
        {
            Memory = buffer;
            this.returnToPool = returnToPool;
        }

        // Đưa segment về trạng thái ban đầu
        public void Reset()
        {
            if (returnToPool)
            {
                // Trả lại buffer về ArrayPool nếu có
                if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(
                    Memory, out var segment) && segment.Array != null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(segment.Array, clearArray: false);
                }
            }

            Memory = default;
            RunningIndex = 0;
            Next = null;
        }

        // Gán chỉ số offset và liên kết với segment tiếp theo
        public void SetRunningIndexAndNext(long runningIndex, Segment nextSegment)
        {
            RunningIndex = runningIndex;
            Next = nextSegment;
        }
    }
}
