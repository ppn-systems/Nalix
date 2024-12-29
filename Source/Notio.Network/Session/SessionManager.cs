using Notio.Infrastructure.Identification;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Notio.Network.Session;

/// <summary>
/// Quản lý các phiên làm việc của khách hàng.
/// <para>
/// Lớp này chịu trách nhiệm quản lý, thêm, xóa và lấy các session hiện tại của người dùng.
/// </para>
/// </summary>
public sealed class SessionManager(IConnLimiter connLimiter)
{
    // Lưu trữ tất cả các session hiện tại trong một ConcurrentDictionary.
    private readonly ConcurrentDictionary<UniqueId, SessionClient> _activeSessions = new();

    private readonly IConnLimiter _connLimiter = connLimiter;

    // Biến đếm số lượng session hiện tại.
    private int _sessionCount = 0;

    /// <summary>
    /// Xảy ra khi một phiên làm việc được thêm.
    /// </summary>
    public event Action<SessionClient>? SessionAdded;

    /// <summary>
    /// Xảy ra khi một phiên làm việc bị xóa.
    /// </summary>
    public event Action<UniqueId>? SessionRemoved;

    /// <summary>
    /// Thêm session mới vào danh sách và cập nhật số lượng session.
    /// </summary>
    /// <param name="session">Phiên làm việc cần thêm.</param>
    /// <returns>Trả về <c>true</c> nếu session được thêm thành công, ngược lại là <c>false</c>.</returns>
    public bool AddSession(SessionClient session)
    {
        if (!ManageConnLimit(session.EndPoint, true))
            return false;

        bool isNewSession = _activeSessions.TryAdd(session.Id, session);

        if (isNewSession)
        {
            Interlocked.Increment(ref _sessionCount);
            SessionAdded?.Invoke(session);
        }

        return isNewSession;
    }

    /// <summary>
    /// Lấy session theo ID.
    /// </summary>
    /// <param name="sessionId">ID của session cần tìm.</param>
    /// <returns>Trả về phiên làm việc nếu tìm thấy, nếu không trả về <c>null</c>.</returns>
    public SessionClient? GetSession(UniqueId sessionId)
    {
        _activeSessions.TryGetValue(sessionId, out SessionClient? session);
        return session;
    }

    /// <summary>
    /// Thử lấy session theo ID.
    /// </summary>
    /// <param name="sessionId">ID của session cần tìm.</param>
    /// <param name="session">Session tìm thấy nếu có, hoặc <c>null</c> nếu không có.</param>
    /// <returns>Trả về <c>true</c> nếu tìm thấy session, ngược lại là <c>false</c>.</returns>
    public bool TryGetSession(UniqueId sessionId, out SessionClient? session) =>
        _activeSessions.TryGetValue(sessionId, out session);

    /// <summary>
    /// Xóa session theo ID.
    /// </summary>
    /// <param name="sessionId">ID của session cần xóa.</param>
    /// <returns>Trả về <c>true</c> nếu xóa thành công, ngược lại là <c>false</c>.</returns>
    public bool RemoveSession(UniqueId sessionId)
    {
        bool isRemoved = _activeSessions.TryRemove(sessionId, out SessionClient? session);

        if (session != null)
        {
            ManageConnLimit(session.EndPoint, false);
            Interlocked.Decrement(ref _sessionCount);
            SessionRemoved?.Invoke(sessionId);
        }

        return isRemoved;
    }

    /// <summary>
    /// Lấy danh sách tất cả các session hiện tại.
    /// </summary>
    /// <returns>Trả về danh sách các session hiện tại.</returns>
    public IEnumerable<SessionClient> GetAllSessions()
    {
        return _activeSessions.Values;
    }

    /// <summary>
    /// Lấy số lượng session hiện tại.
    /// </summary>
    /// <returns>Số lượng session hiện tại.</returns>
    public int Count() => _sessionCount;

    private bool ManageConnLimit(string ipAddress, bool isAdding)
    {
        if (isAdding)
        {
            return _connLimiter.IsConnectionAllowed(ipAddress);
        }
        else
        {
            _connLimiter.ConnectionClosed(ipAddress);
            return true;
        }
    }
}