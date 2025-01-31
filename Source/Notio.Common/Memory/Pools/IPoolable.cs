namespace Notio.Common.Memory.Pools;

/// <summary>
/// Giao diện cho các đối tượng có thể được lưu trữ.
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// Đặt lại một instance <see cref="IPoolable"/> trước khi nó được trả về pool.
    /// </summary>
    public void ResetForPool();
}