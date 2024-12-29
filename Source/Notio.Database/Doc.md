1. Tạo và Lấy dữ liệu từ các bảng:
Bạn có thể sử dụng DbContext để thêm, cập nhật, xóa hoặc lấy dữ liệu từ các bảng.

Thêm mới người dùng:

```csharp
using (var context = new AppDbContext(options))
{
    var newUser = new User
    {
        Username = "exampleUser",
        PasswordHash = "hashedPassword",
        DisplayName = "Example User",
        Email = "user@example.com"
    };

    context.Users.Add(newUser);
    context.SaveChanges();
}
```

Thêm mới cuộc trò chuyện:

```csharp
using (var context = new AppDbContext(options))
{
    var newChat = new Chat
    {
        ChatName = "Example Chat"
    };

    context.Chats.Add(newChat);
    context.SaveChanges();
}
```

Gửi tin nhắn:

```csharp
using (var context = new AppDbContext(options))
{
    var message = new Message
    {
        ChatId = chatId,
        SenderId = userId,
        MessageType = MessageType.Text,
        MessageContent = "Hello World",
        CreatedAt = DateTime.UtcNow
    };

    context.Messages.Add(message);
    context.SaveChanges();
}
```
2. Lấy thông tin nhắn trong một cuộc trò chuyện:
Bạn có thể lấy danh sách tin nhắn theo cuộc trò chuyện:

```csharp
using (var context = new AppDbContext(options))
{
    var chatMessages = context.Messages
        .Where(m => m.ChatId == chatId)
        .OrderBy(m => m.CreatedAt)
        .ToList();
}
```
3. Quản lý UserChats (mối quan hệ many-to-many giữa User và Chat):
Để thêm hoặc gỡ bỏ người dùng từ một cuộc trò chuyện:

Thêm một người dùng vào cuộc trò chuyện:

```csharp
using (var context = new AppDbContext(options))
{
    var userChat = new UserChat
    {
        UserId = userId,
        ChatId = chatId,
        UserRole = ChatRole.Member
    };

    context.UserChats.Add(userChat);
    context.SaveChanges();
}
```
Gỡ bỏ người dùng khỏi cuộc trò chuyện:

```csharp
using (var context = new AppDbContext(options))
{
    var userChat = context.UserChats
        .FirstOrDefault(uc => uc.UserId == userId && uc.ChatId == chatId);

    if (userChat != null)
    {
        context.UserChats.Remove(userChat);
        context.SaveChanges();
    }
}
```
4. Tạo và Quản lý File đính kèm:

Để thêm file đính kèm cho tin nhắn:

```csharp
using (var context = new AppDbContext(options))
{
    var attachment = new MessageAttachment
    {
        MessageId = messageId,
        FileUrl = "/path/to/file.jpg",
        FileName = "file.jpg",
        FileSize = 1024,
        FileType = "image/jpeg",
        CreatedAt = DateTime.UtcNow
    };

    context.MessageAttachments.Add(attachment);
    context.SaveChanges();
}
```
Với các thao tác này, bạn có thể quản lý toàn bộ chuỗi liên quan đến nhắn tin và các hoạt động tương ứng trong cơ sở dữ liệu.
