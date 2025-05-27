using System.Collections.Concurrent;

namespace Nalix.Serialization.Internal.Sequence;

internal static class SequenceBuilderPool
{
    // Hàng đợi lưu các SequenceBuilder đã được trả về
    private static readonly ConcurrentQueue<SequenceBuilder> queue = new();

    // Lấy một SequenceBuilder từ pool, nếu không có thì tạo mới
    public static SequenceBuilder Rent()
    {
        if (queue.TryDequeue(out var builder))
        {
            return builder;
        }
        return new SequenceBuilder();
    }

    // Trả SequenceBuilder về pool sau khi dùng xong
    public static void Return(SequenceBuilder builder)
    {
        builder.Reset(); // Đưa về trạng thái ban đầu
        queue.Enqueue(builder);
    }
}
