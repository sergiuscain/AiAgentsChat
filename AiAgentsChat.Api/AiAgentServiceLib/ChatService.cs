using AiAgentServiceLib.Models;
using RabbitMQ.Client.Events;
using RabbitMQS;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                // Берем только последние N сообщений (например, 10)
                var recentMessages = session.MessageHistory
                    .Where(m => m.MessageType == "text")
                    .TakeLast(10) // Ограничиваем историю
                    .ToList();

                // Исключаем сообщения текущего агента из истории чтобы он не отвечал на себя
                var filteredHistory = recentMessages
                    .Where(m => m.SenderName != agentName)
                    .ToList();

                // Формируем промпт с четкой инструкцией
                var historyText = string.Join("\n",
                    filteredHistory.Select(m => $"{m.SenderName}: {m.Content}"));

                var prompt = $@"
Ты - AI агент по имени {agentName}. Ты находишься в групповом чате с другими агентами.

Правила поведения:
1. Отвечай ТОЛЬКО если обращаются к тебе напрямую или если твой ответ действительно важен
2. Не повторяй то, что уже сказали другие агенты
3. Если вопрос задан всем, подожди немного - возможно, другой агент уже ответит
4. Будь кратким и по делу
5. Учитывай контекст предыдущих сообщений
6. Если ты не отвечаешь, пиши - 'non666non'  такое сообщение автоматически отфильтруется
7. Ты можешь общаться с другими участниками чата, для этого прямо указывай, к кому ты образаешься
Запомни эти правила и следуй им всегда.

История последних сообщений:
{historyText}

Текущее сообщение от {message.SenderName}: {message.Content}

Твой ({agentName}) ответ (только если он полезен и уникален, иначе не отвечай):";

                // Получаем ответ от агента
                var response = await _agentsFabric.PostPromptAsync(prompt, agentName);

                // Отправляем ответ в чат, только если он валиден!!
                if(IsValidResponse(response, agentName))
                    await SendMessageAsync(chatId, agentName, response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message for agent {agentName}: {ex.Message}");
        }
    }

    private bool IsValidResponse(string response, string agentName)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;
        if (response.Contains("non666non"))
            return false;
    //    // 1. Приведение к нижнему регистру только один раз
    //    //var lower = response.ToLowerInvariant();

    //    // 2. Недопустимые фразы (регулярный поиск, чтобы поймать любые варианты)
    //    var invalidPhrases = new[]
    //    {
    //    @"не\s+отвечаю",
    //    @"не\s+отвечает",
    //    @"ошибка[:\s]*",
    //    @"error[:\s]*",
    //    @"service request failed",
    //    @"bad request",
    //    @"status:\s*400",
    //    "non666non",
    //};

    //    foreach (var pattern in invalidPhrases)
    //    {
    //        if (Regex.IsMatch(lower, pattern))
    //            return false;
    //    }

    //    // 3. Минимальная длина – можно менять в зависимости от требований
    //    const int minLength = 2;   // 2 символов уже считается «коротким», но допустимым
    //    if (response.Trim().Length < minLength)
    //        return false;

    //    // 4. Проверка таблицы через регулярное выражение (первый столбец начинается с |, вторые — ---)
    //    var tablePattern = @"^\s*\|\s*.+\n(?:\s*\|\s*-+\s*\|.*\n?)+";
    //    if (Regex.IsMatch(response, tablePattern))
    //        return false;

    //    // 5. Любой другой случай считается валидным
        return true;
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
