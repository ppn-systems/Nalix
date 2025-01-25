
CREATE TABLE Users (
    UserId BIGINT AUTO_INCREMENT PRIMARY KEY,       -- ID duy nhất của người dùng
    Username VARCHAR(50) NOT NULL,                  -- Tên đăng nhập không được để trống
    PasswordHash CHAR(60) NOT NULL,                 -- Cố định độ dài cho bcrypt hash
    DisplayName NVARCHAR(100),                      -- Hỗ trợ Unicode tốt hơn cho tên hiển thị
    Email VARCHAR(100),                             -- Email của người dùng
    AvatarUrl VARCHAR(255),                         -- Đường dẫn đến avatar của người dùng
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  -- Thời gian tạo tài khoản
    LastLogin TIMESTAMP NULL,                       -- Thời gian đăng nhập gần nhất
    IsActive BOOLEAN DEFAULT TRUE,                  -- Trạng thái hoạt động của tài khoản, mặc định là TRUE
    UNIQUE KEY uk_username (Username),              -- Tạo khóa duy nhất cho tên đăng nhập
    UNIQUE KEY uk_email (Email),                    -- Tạo khóa duy nhất cho email
    KEY idx_status_created (IsActive, CreatedAt)    -- Index kết hợp để tối ưu hóa truy vấn theo trạng thái và thời gian tạo
) ENGINE=InnoDB ROW_FORMAT=COMPRESSED;              -- Sử dụng định dạng nén để tiết kiệm không gian lưu trữ

CREATE TABLE Chats (
    ChatId BIGINT AUTO_INCREMENT PRIMARY KEY,       -- ID duy nhất của cuộc trò chuyện
    ChatName VARCHAR(100),                          -- Tên cuộc trò chuyện
    IsGroupChat BOOLEAN DEFAULT FALSE,              -- Xác định xem có phải là cuộc trò chuyện nhóm hay không, mặc định là FALSE
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  -- Thời gian tạo cuộc trò chuyện
    LastActivityAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP, -- Thời gian hoạt động cuối cùng, tự động cập nhật
    KEY idx_last_activity (LastActivityAt)          -- Chỉ mục cho thời gian hoạt động cuối cùng
) ENGINE=InnoDB
PARTITION BY RANGE (UNIX_TIMESTAMP(CreatedAt)) (    -- Phân vùng dữ liệu theo năm
    PARTITION p_2023 VALUES LESS THAN (UNIX_TIMESTAMP('2024-01-01 00:00:00')),
    PARTITION p_2024 VALUES LESS THAN (UNIX_TIMESTAMP('2025-01-01 00:00:00')),
    PARTITION p_future VALUES LESS THAN MAXVALUE
);

CREATE TABLE UserChats (
    UserId BIGINT NOT NULL,                                 -- ID của người dùng
    ChatId BIGINT NOT NULL,                                 -- ID của cuộc trò chuyện
    UserRole ENUM('Admin', 'Member') DEFAULT 'Member',      -- Vai trò của người dùng trong cuộc trò chuyện, mặc định là Member
    JoinedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,           -- Thời gian tham gia cuộc trò chuyện
    LastReadMessageId BIGINT,                               -- ID của tin nhắn cuối cùng mà người dùng đã đọc
    PRIMARY KEY (UserId, ChatId),                           -- Khóa chính là kết hợp của UserId và ChatId
    KEY idx_chat_role (ChatId, UserRole),                   -- Chỉ mục kết hợp cho truy vấn theo ChatId và UserRole
    FOREIGN KEY fk_user (UserId) REFERENCES Users(UserId)   -- Khóa ngoại tham chiếu tới bảng Users
        ON DELETE CASCADE ON UPDATE CASCADE,                -- Xóa hoặc cập nhật liên kết khi người dùng bị xóa hoặc cập nhật
    FOREIGN KEY fk_chat (ChatId) REFERENCES Chats(ChatId)   -- Khóa ngoại tham chiếu tới bảng Chats
        ON DELETE CASCADE ON UPDATE CASCADE                 -- Xóa hoặc cập nhật liên kết khi cuộc trò chuyện bị xóa hoặc cập nhật
) ENGINE=InnoDB;

CREATE TABLE Messages (
    MessageId BIGINT AUTO_INCREMENT PRIMARY KEY,                                -- ID duy nhất của tin nhắn
    ChatId BIGINT NOT NULL,                                                     -- ID của cuộc trò chuyện mà tin nhắn thuộc về
    SenderId BIGINT NOT NULL,                                                   -- ID của người gửi tin nhắn
    MessageType ENUM('Text', 'Image', 'Video', 'File') NOT NULL DEFAULT 'Text', -- Loại tin nhắn, mặc định là text
    MessageContent TEXT COMPRESSED,                                             -- Nội dung tin nhắn, sử dụng nén để tiết kiệm không gian lưu trữ
    MediaMetadata JSON,                                                         -- Lưu metadata của media dưới dạng JSON
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,                              -- Thời gian tạo tin nhắn
    IsDeleted BOOLEAN DEFAULT FALSE,                                            -- Cờ xác định tin nhắn đã bị xóa hay chưa, mặc định là FALSE
    KEY idx_chat_created (ChatId, CreatedAt),                                   -- Chỉ mục kết hợp cho truy vấn theo ChatId và thời gian tạo
    KEY idx_sender_created (SenderId, CreatedAt),                               -- Chỉ mục kết hợp cho truy vấn theo SenderId và thời gian tạo
    FOREIGN KEY fk_chat (ChatId) REFERENCES Chats(ChatId)                       -- Khóa ngoại tham chiếu tới bảng Chats
        ON DELETE CASCADE ON UPDATE CASCADE,                                    -- Xóa hoặc cập nhật liên kết khi cuộc trò chuyện bị xóa hoặc cập nhật
    FOREIGN KEY fk_sender (SenderId) REFERENCES Users(UserId)                   -- Khóa ngoại tham chiếu tới bảng Users
        ON DELETE CASCADE ON UPDATE CASCADE                                     -- Xóa hoặc cập nhật liên kết khi người gửi bị xóa hoặc cập nhật
) ENGINE=InnoDB
PARTITION BY RANGE (UNIX_TIMESTAMP(CreatedAt)) (                                -- Phân vùng dữ liệu theo năm
    PARTITION p_2023 VALUES LESS THAN (UNIX_TIMESTAMP('2024-01-01 00:00:00')),
    PARTITION p_2024 VALUES LESS THAN (UNIX_TIMESTAMP('2025-01-01 00:00:00')),
    PARTITION p_future VALUES LESS THAN MAXVALUE
);

-- Tạo bảng riêng cho file đính kèm
CREATE TABLE MessageAttachments (
    AttachmentId BIGINT AUTO_INCREMENT PRIMARY KEY,                     -- ID duy nhất của file đính kèm
    MessageId BIGINT NOT NULL,                                          -- ID của tin nhắn chứa file đính kèm
    FileUrl VARCHAR(255),                                               -- Đường dẫn tới file đính kèm
    FileName VARCHAR(255),                                              -- Tên của file đính kèm
    FileSize BIGINT,                                                    -- Kích thước của file đính kèm
    FileType VARCHAR(50),                                               -- Loại của file đính kèm
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,                      -- Thời gian tạo file đính kèm
    FOREIGN KEY fk_message (MessageId) REFERENCES Messages(MessageId)   -- Khóa ngoại tham chiếu tới bảng Messages
        ON DELETE CASCADE ON UPDATE CASCADE,                            -- Xóa hoặc cập nhật liên kết khi tin nhắn bị xóa hoặc cập nhật
    KEY idx_message (MessageId)                                         -- Chỉ mục cho MessageId
) ENGINE=InnoDB;