namespace AiAgentServiceLib.Models;

public class ChatSession
{
    public string ChatId { get; set; }
    public List<string> Participants { get; set; } = new();
    public List<ChatMessage> MessageHistory { get; set; } = new();
}
