using AiAgentServiceLib.Models;
using RabbitMQ.Client.Events;
using RabbitMQS;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AiAgentServiceLib;
public class ChatService
{
    private readonly RabbitMQService _rabbitMQ;
    private readonly AiAgentsFabric _agentsFabric;
    private readonly ConcurrentDictionary<string, ChatSession> _activeChats;
    private readonly ConcurrentDictionary<string, ConsumerInfo> _consumers;
    private readonly ConcurrentDictionary<string, List<Action<ChatMessage>>> _chatSubscribers = new();

    public ChatService(RabbitMQService rabbitMQ, AiAgentsFabric agentsFabric)
    {
        _rabbitMQ = rabbitMQ;
        _agentsFabric = agentsFabric;
        _activeChats = new ConcurrentDictionary<string, ChatSession>();
        _consumers = new ConcurrentDictionary<string, ConsumerInfo>();
        _chatSubscribers = new ConcurrentDictionary<string, List<Action<ChatMessage>>>();
    }

    public async Task<string> CreateChat(params string[] agentNames)
    {
        var chatId = $"chat_{Guid.NewGuid()}";
        var session = new ChatSession { ChatId = chatId };

        foreach (var agentName in agentNames)
        {
            if (_agentsFabric.Agents.ContainsKey(agentName))
            {
                session.Participants.Add(agentName);

                var queueName = $"agent_{agentName}_{chatId}";
                await _rabbitMQ.CreateQueue(
                    queueName: queueName,
                    routingKeys: new List<string>
                    {
                    $"{chatId}.#",
                    $"agent.{agentName}"
                    });

                var consumer = await _rabbitMQ.CreateConsumerAsync(queueName);

                // Создаем обработчик
                AsyncEventHandler<BasicDeliverEventArgs> handler = async (sender, ea) =>
                {
                    await ProcessMessageForAgentAsync(agentName, ea, chatId);
                };

                // Подписываемся
                consumer.ReceivedAsync += handler;

                // Сохраняем информацию о consumer и обработчике
                _consumers[queueName] = new ConsumerInfo
                {
                    Consumer = consumer,
                    Handler = handler
                };
            }
        }

        _activeChats[chatId] = session;

        // ... остальной код без изменений
        return chatId;
    }

    private async Task ProcessMessageForAgentAsync(string agentName, BasicDeliverEventArgs ea, string chatId)
    {
        try
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var message = JsonSerializer.Deserialize<ChatMessage>(json);

            if (message == null || message.SenderName == agentName)
                return;

            // Получаем историю чата
            if (_activeChats.TryGetValue(chatId, out var session))
            {
                // Формируем промпт для AI агента с историей диалога
                var history = string.Join("\n",
                    session.MessageHistory
                        .Where(m => m.MessageType == "text")
                        .Select(m => $"{m.SenderName}: {m.Content}"));

                var prompt = $"Continue the conversation. Previous messages:\n{history}\n\n{message.SenderName}: {message.Content}\n\nYour response:";

                // Получаем ответ от агента
                var response = await _agentsFabric.PostPromptAsync(prompt, agentName);

                // Отправляем ответ в чат
                await SendMessageAsync(chatId, agentName, response);
            }
        }
        catch (Exception ex)
        {
            // Логируем ошибку
            Console.WriteLine($"Error processing message for agent {agentName}: {ex.Message}");
        }
    }

    public async Task SendMessageAsync(string chatId, string senderName, string content)
    {
        var message = new ChatMessage
        {
            ChatId = chatId,
            SenderName = senderName,
            Content = content,
            MessageType = "text"
        };

        // Сохраняем в историю
        if (_activeChats.TryGetValue(chatId, out var session))
        {
            session.MessageHistory.Add(message);
        }

        // Публикуем в RabbitMQ
        await _rabbitMQ.PublishMessage(message, $"{chatId}.message");

        // Также отправляем каждому участнику
        foreach (var participant in session?.Participants ?? new List<string>())
        {
            if (participant != senderName)
            {
                await _rabbitMQ.PublishMessage(message, $"agent.{participant}");
            }
        }
        // Уведомляем всех подписчиков
        NotifySubscribers(chatId, message);
    }

    public List<ChatMessage> GetChatHistory(string chatId)
    {
        return _activeChats.TryGetValue(chatId, out var session)
            ? session.MessageHistory
            : new List<ChatMessage>();
    }


    public async Task<string>  PostPromptToDirectAsync(string prompt, string AiAgentName)
    {
        return await _agentsFabric.PostPromptAsync(prompt, AiAgentName);
    }

    public bool CreateAgent(string aiAgentName)
    {
        try
        {
            _agentsFabric.CreateAgent(aiAgentName);
            return true;
        }
        catch (Exception ex)
        {
            throw new Exception("Ошибка при попытке создать агента");
        }
    }
    public List<string> GetAllAgentsName()
    {
        return _agentsFabric.GetAllAgentsName();
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var consumerInfo in _consumers.Values)
        {
            // Правильно отписываемся от события
            if (consumerInfo.Handler != null)
            {
                consumerInfo.Consumer.ReceivedAsync -= consumerInfo.Handler;
            }
        }
        _consumers.Clear();

        if (_rabbitMQ is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }
    public void SubscribeToChat(string chatId, Action<ChatMessage> callback)
    {
        var subscribers = _chatSubscribers.GetOrAdd(chatId, id => new List<Action<ChatMessage>>());
        lock (subscribers)
        {
            subscribers.Add(callback);
        }
    }
    public void UnsubscribeFromChat(string chatId, Action<ChatMessage> callback)
    {
        if (_chatSubscribers.TryGetValue(chatId, out var subscribers))
        {
            lock (subscribers)
            {
                subscribers.Remove(callback);
            }
        }
    }
    private void NotifySubscribers(string chatId, ChatMessage message)
    {
        if (_chatSubscribers.TryGetValue(chatId, out var subscribers))
        {
            lock (subscribers)
            {
                foreach (var callback in subscribers)
                {
                    try
                    {
                        callback(message);
                    }
                    catch { /* Игнорируем ошибки подписчика */ }
                }
            }
        }
    }

    public List<string> GetAllChats()
    {
        var result = _activeChats.Keys.ToList();
        return result;
    }
    public bool DeleteChat(string chatId)
    {
        try
        {
            if (_activeChats.TryRemove(chatId, out var session))
            {
                // Очищаем подписчиков
                _chatSubscribers.TryRemove(chatId, out _);

                // Отписываемся от событий RabbitMQ
                foreach (var consumerKey in _consumers.Keys.Where(k => k.Contains(chatId)).ToList())
                {
                    if (_consumers.TryRemove(consumerKey, out var consumerInfo))
                    {
                        // Правильно отписываемся от события
                        if (consumerInfo.Handler != null)
                        {
                            consumerInfo.Consumer.ReceivedAsync -= consumerInfo.Handler;
                        }
                    }
                }

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting chat {chatId}: {ex.Message}");
            return false;
        }
    }
    public bool DeleteAgent(string agentName)
    {
        try
        {
            return _agentsFabric.DeleteAgent(agentName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting agent {agentName}: {ex.Message}");
            return false;
        }
    }
}
