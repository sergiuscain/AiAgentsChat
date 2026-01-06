namespace AiAgentsChat.Api.Models;

public class ChatSession
{
    public string ChatId { get; set; }
    public List<string> Participants { get; set; } = new();
    public List<ChatMessageVM> MessageHistory { get; set; } = new();
}
