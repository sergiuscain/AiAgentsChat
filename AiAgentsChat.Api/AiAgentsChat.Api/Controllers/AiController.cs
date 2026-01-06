using AiAgentsChat.Api.Models;
using AiAgentsChat.Api.ViewModels;
using AiAgentServiceLib;
using AiAgentServiceLib.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
        var agents = _chatService.GetAllAgentsName();
        return Ok(agents);  
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

        return history.Select(chat => new ChatMessageVM
        {
            Id = chat.Id,
            ChatId = chat.ChatId,
            SenderName = chat.SenderName,
            Content = chat.Content,
            MessageType = chat.MessageType,
            Timestamp = chat.Timestamp  // Копируем timestamp!
        }).ToList();
    }
    [HttpGet("stream/{chatId}")]
    public async Task GetMessageStream(string chatId)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");

        Action<ChatMessage> onNewMessage = (message) =>
        {
            if (message.ChatId == chatId && !HttpContext.RequestAborted.IsCancellationRequested)
            {
                if (message.Timestamp == default)
                {
                    message.Timestamp = DateTime.UtcNow;
                }
                // Запускаем асинхронную операцию в отдельной задаче
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var jsonOptions = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
                            WriteIndented = false
                        };
                        var json = JsonSerializer.Serialize(message, jsonOptions);
                        await Response.WriteAsync($"data: {json}\n\n");
                        await Response.Body.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибки записи
                        Console.WriteLine($"Error writing SSE: {ex.Message}");
                    }
                });
            }
        };
        _chatService.SubscribeToChat(chatId, onNewMessage);
        try
        {
            // Отправляем начальное сообщение
            await Response.WriteAsync($"data: {{\"type\":\"connected\",\"chatId\":\"{chatId}\"}}\n\n");
            await Response.Body.FlushAsync();

            // Держим соединение открытым
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                await Task.Delay(30000);
                await Response.WriteAsync(": keep-alive\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
        {
            // Клиент разорвал соединение - нормально
        }
        finally
        {
            _chatService.UnsubscribeFromChat(chatId, onNewMessage);
        }

    }
}
