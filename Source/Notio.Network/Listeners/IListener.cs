using System.Threading;

namespace Notio.Network.Listeners;

/// <summary>
/// Giao diện dành cho các lớp lắng nghe mạng.
/// </summary>
internal interface IListener
{
    /// <summary>
    /// Bắt đầu lắng nghe với một CancellationToken để có thể hủy quá trình nếu cần.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken dùng để hủy lắng nghe.</param>
    void BeginListening(CancellationToken cancellationToken);

    /// <summary>
    /// Kết thúc quá trình lắng nghe.
    /// </summary>
    void EndListening();
}
