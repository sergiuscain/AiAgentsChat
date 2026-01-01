using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace AiAgentServiceLib;
public class AiAgentService
{
    string modelName = "openai/gpt-oss-20b"; // Например, "openai/gpt-oss-20b"[citation:6]
    string serverUrl = "http://localhost:1234/v1"; // Базовый URL LM Studio[citation:7]
    string apiKey = "not-needed"; // Для LM Studio ключ не требуется, но поле обязательно[citation:7]
    private OpenAIClient _openAIClient;
    private ChatClient _chatClient;
    private List<ChatMessage> _conversationHistory;

    //
    string startPrompt = "Ты полезный AI-ассистент, который запоминает контекст разговора и общается с другими полезными AI-ассистентами и разработчиком, который создает приложение с использованием AI-ассистентов.";
    public AiAgentService()
    {
        Init();
    }
    /// <summary>
    /// Метод отправляет ваш запрос Ai ассистенту и возвращает ответ,
    /// клиент чата создается в конструкторе и так же там создается история сообщений (контекст),
    /// которая может быть очищена методом ClearConversationHistory
    /// </summary>
    /// <param name="prompt">Ваш запрос</param>
    /// <returns></returns>
    public async Task<string> PostPromptAsync(string prompt)
    {
        _conversationHistory.Add(new UserChatMessage(prompt));
        try
        {
            var completion = await _chatClient.CompleteChatAsync(_conversationHistory);
            if (completion.Value.FinishReason == ChatFinishReason.Stop)
            {
                var assistantResponse = completion.Value.Content[0].Text;
                if (_conversationHistory.Count > 20) _conversationHistory.RemoveAt(1);
                _conversationHistory.Add(new AssistantChatMessage(assistantResponse));
                return assistantResponse;
            }
            else
                return $"Завершено по причине: {completion.Value.FinishReason}";
        }
        catch(Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }
    /// <summary>
    /// Очищает контекст (историю сообщений)
    /// </summary>
    public void ClearConversationHistory()
    {
        _conversationHistory.Clear();
        _conversationHistory.Add(new SystemChatMessage(startPrompt));
    }
    /// <summary>
    /// Возвращает историю сообщений (контекст)
    /// </summary>
    /// <returns></returns>
    public List<ChatMessage> GetConversationHistory()
    {
        return [.. _conversationHistory];
    }
    /// <summary>
    /// Задает настройки по умолчанию
    /// </summary>
    public void Init()
    {
        _openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
        {
            Endpoint = new Uri(serverUrl)
        });
        _chatClient = _openAIClient.GetChatClient(modelName);

        _conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(startPrompt)
        };
    }
}
