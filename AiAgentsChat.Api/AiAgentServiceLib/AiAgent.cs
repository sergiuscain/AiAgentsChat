using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace AiAgentServiceLib.Agent;
public class AiAgent
{
    private OpenAIClient _openAIClient;
    private ChatClient _chatClient;
    private List<ChatMessage> _conversationHistory;

    //
    
    public AiAgent(string name, string apiKey, string serverUrl, string modelName, string startPrompt)
    {
        Init(name, apiKey, serverUrl, modelName, startPrompt);
    }
    /// <summary>
    /// Метод отправляет ваш запрос Ai ассистенту и возвращает ответ,
    /// клиент чата создается в конструкторе и так же там создается история сообщений (контекст),
    /// которая может быть очищена методом ClearConversationHistory
    /// </summary>
    /// <param name="prompt">Ваш запрос</param>
    /// <returns></returns>
    internal async Task<string> PostPromptAsync(string prompt)
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
    internal void ClearConversationHistory()
    {
        _conversationHistory.Clear();
        _conversationHistory.Add(new SystemChatMessage(Constants.StartPrompt));
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
    private void Init(string name ,string apiKey, string serverUrl, string modelName, string startPrompt)
    {
        _openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
        {
            Endpoint = new Uri(serverUrl)
        });
        _chatClient = _openAIClient.GetChatClient(modelName);

        _conversationHistory = new List<ChatMessage>
        {
            new SystemChatMessage(startPrompt + string.Format(Constants.NamePrompt, name))
        };
    }
}
