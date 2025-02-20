SELECT 
    uc.UserId, 
    uc.ChatId, 
    c.ChatName, 
    m.Content AS LastMessageContent, 
    m.CreatedAt AS LastMessageCreatedAt 
FROM 
    UserChats uc
JOIN 
    Chats c ON uc.ChatId = c.ChatId
LEFT JOIN 
    Messages m ON m.ChatId = uc.ChatId
WHERE 
    uc.UserId = 9;
