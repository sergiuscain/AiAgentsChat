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
    private readonly ConcurrentDictionary<string, AsyncEventingBasicConsumer> _consumers;

    public ChatService(RabbitMQService rabbitMQ, AiAgentsFabric agentsFabric)
    {
        _rabbitMQ = rabbitMQ;
        _agentsFabric = agentsFabric;
        _activeChats = new ConcurrentDictionary<string, ChatSession>();
        _consumers = new ConcurrentDictionary<string, AsyncEventingBasicConsumer>();
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

                // Создаем очередь для каждого агента
                var queueName = $"agent_{agentName}_{chatId}";
                await _rabbitMQ.CreateQueue(
                        queueName: queueName,
                        routingKeys: new List<string>
                        {
                            $"{chatId}.#",
                            $"agent.{agentName}"
                        });

                // Подписываем агента на сообщения
                var consumer = await _rabbitMQ.CreateConsumerAsync(queueName);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    await ProcessMessageForAgentAsync(agentName, ea, chatId);
                };

                _consumers[queueName] = consumer;
            }
        }
        _activeChats[chatId] = session;

        // Отправляем системное сообщение о создании чата
        await _rabbitMQ.PublishMessage(new ChatMessage
        {
            ChatId = chatId,
            SenderName = "System",
            Content = $"Chat created with participants: {string.Join(", ", agentNames)}",
            MessageType = "system"
        }, $"{chatId}.system");

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
    public string GetAllAgentsName()
    {
        return _agentsFabric.GetAllAgentsName();
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var consumer in _consumers.Values)
        {
            consumer.ReceivedAsync -= null; // Очищаем обработчики
        }
        _consumers.Clear();

        if (_rabbitMQ is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }
}
