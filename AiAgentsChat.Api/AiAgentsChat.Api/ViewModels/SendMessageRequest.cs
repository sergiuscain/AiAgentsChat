namespace AiAgentsChat.Api.ViewModels;
public class SendMessageRequest
{
    public string ChatId { get; set; }
    public string SenderName { get; set; }
    public string Message { get; set; }
}
