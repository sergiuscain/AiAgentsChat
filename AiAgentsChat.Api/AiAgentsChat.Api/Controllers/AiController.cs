using Microsoft.AspNetCore.Mvc;
using AiAgentServiceLib;
using AiAgentsChat.Api.Models;
using AiAgentsChat.Api.ViewModels;

namespace AiAgentsChat.Api.Controllers;
[Route("api/[controller]")]
[ApiController]
public class AiController : ControllerBase
{
    private readonly ChatService _chatService;
    public AiController(ChatService chatService)
    {
        _chatService = chatService;
    }
    [HttpPost("PostApiPrompt")]
    public async Task<IActionResult> PostApiPromptAsync(string prompt, string AiAgentName)
    {
           
        var result = await _chatService.PostPromptToDirectAsync(prompt, AiAgentName);
        if (result != null) 
            return Ok(result);
        return BadRequest("Ошибка отправке запроса");
    }
    [HttpPost("CreateAiAgent")]
    public IActionResult CreateAgent(string AiAgentName)
    {
        return Ok(_chatService.CreateAgent(AiAgentName));
    }
    [HttpGet("GetAllAgentsName")]
    public IActionResult GetAllAgentsName()
    {
        return Ok(_chatService.GetAllAgentsName());
    }
    [HttpPost("CreateChat")]
    public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
    {
        var chatId = await _chatService.CreateChat(request.AgentNames);
        return Ok(new { ChatId = chatId });
    }

    [HttpPost("Send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        await _chatService.SendMessageAsync(request.ChatId, request.SenderName, request.Message);
        return Ok();
    }
    [HttpGet("GetChatHistory")]
    public List<ChatMessageVM> GetChatMessage(string chatId)
    {
        var history = _chatService.GetChatHistory(chatId);
        // Здесь надо будет реализовать работу через автомаппер
        var historyViewModel = new List<ChatMessageVM>();
        foreach (var chat in history)
        {
            var chatMessageVM = new ChatMessageVM();
            chatMessageVM.ChatId = chat.ChatId;
            chatMessageVM.SenderName = chat.SenderName;
            chatMessageVM.Content = chat.Content;
            chatMessageVM.MessageType = chat.MessageType;

            historyViewModel.Add(chatMessageVM);
        }
        return historyViewModel;
    }
}
