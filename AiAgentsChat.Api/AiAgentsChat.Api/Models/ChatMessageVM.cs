namespace AiAgentsChat.Api.Models;
public class ChatMessageVM
{
    public string ChatId { get; set; }
    public string SenderName { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string MessageType { get; set; } // "text", "system", "agent_response"

}