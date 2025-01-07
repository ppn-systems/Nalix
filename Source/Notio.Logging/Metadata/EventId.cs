using System;

namespace Notio.Logging.Metadata;

/// <summary>
/// Xác định một sự kiện ghi log. Trình nhận dạng chính là thuộc tính "Id", với thuộc tính "Name" cung cấp một mô tả ngắn về loại sự kiện này.
/// </summary>
/// <remarks>
/// Khởi tạo một phiên bản của cấu trúc <see cref="EventId"/>.
/// </remarks>
/// <param name="id">Trình nhận dạng số của sự kiện này.</param>
/// <param name="name">Tên của sự kiện này.</param>
public readonly struct EventId(int id, string? name = null) : IEquatable<EventId>
{
    public static readonly EventId Empty = new(0);

    /// <summary>
    /// Tạo ngầm một EventId từ <see cref="int"/> được cung cấp.
    /// </summary>
    /// <param name="i">Giá trị <see cref="int"/> để chuyển đổi thành EventId.</param>
    public static implicit operator EventId(int i)
    {
        return new EventId(i);
    }

    /// <summary>
    /// Kiểm tra xem hai phiên bản <see cref="EventId"/> được chỉ định có cùng giá trị không. Chúng bằng nhau nếu có cùng ID.
    /// </summary>
    /// <param name="left">Phiên bản đầu tiên của <see cref="EventId"/>.</param>
    /// <param name="right">Phiên bản thứ hai của <see cref="EventId"/>.</param>
    /// <returns><see langword="true" /> nếu các đối tượng bằng nhau.</returns>
    public static bool operator ==(EventId left, EventId right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Kiểm tra xem hai phiên bản <see cref="EventId"/> được chỉ định có khác nhau không.
    /// </summary>
    /// <param name="left">Phiên bản đầu tiên của <see cref="EventId"/>.</param>
    /// <param name="right">Phiên bản thứ hai của <see cref="EventId"/>.</param>
    /// <returns><see langword="true" /> nếu các đối tượng không bằng nhau.</returns>
    public static bool operator !=(EventId left, EventId right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Lấy trình nhận dạng số của sự kiện này.
    /// </summary>
    public int Id { get; } = id;

    /// <summary>
    /// Lấy tên của sự kiện này.
    /// </summary>
    public string? Name { get; } = name;

    /// <inheritdoc />
    public override string ToString()
    {
        return Name ?? Id.ToString();
    }

    /// <summary>
    /// So sánh phiên bản hiện tại với một đối tượng khác cùng loại. Hai sự kiện bằng nhau nếu có cùng ID.
    /// </summary>
    /// <param name="other">Một đối tượng để so sánh với đối tượng này.</param>
    /// <returns><see langword="true" /> nếu đối tượng hiện tại bằng <paramref name="other" />; nếu không, <see langword="false" />.</returns>
    public bool Equals(EventId other)
    {
        return Id == other.Id;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is EventId eventId && Equals(eventId);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Id;
    }
}