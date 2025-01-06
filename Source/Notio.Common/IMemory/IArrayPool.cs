namespace Notio.Common.IMemory;

/// <summary>
/// Quản lý các bộ đệm có nhiều kích thước khác nhau.
/// </summary>
public interface IArrayPool
{
    /// <summary>
    /// Lấy kích thước lớn nhất của buffer từ danh sách cấu hình.
    /// </summary>
    int MaxBufferSize { get; }

    /// <summary>
    /// Cấp phát bộ đệm cho bộ nhớ.
    /// </summary>
    void AllocateBuffers();

    /// <summary>
    /// Thuê một bộ đệm với kích thước cụ thể.
    /// </summary>
    /// <param name="size">Kích thước của bộ đệm cần thuê. Giá trị mặc định là 256.</param>
    /// <returns>Mảng byte đại diện cho bộ đệm đã thuê.</returns>
    byte[] Rent(int size = 256);

    /// <summary>
    /// Trả lại một bộ đệm để tái sử dụng.
    /// </summary>
    /// <param name="buffer">Bộ đệm cần trả lại.</param>
    void Return(byte[] buffer);

    /// <summary>
    /// Lấy thông tin phân bổ bộ nhớ cho một kích thước cụ thể.
    /// </summary>
    /// <param name="size">Kích thước của bộ nhớ cần kiểm tra.</param>
    /// <returns>Giá trị phân bổ cho kích thước đã cho.</returns>
    double GetAllocationForSize(int size);
}