using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RabbitMQS;

public class RabbitMQService : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchangeName;

    public RabbitMQService(string hostName = "localhost", string chatName = "ai-agents-chat")
    {
        _exchangeName = chatName;
        var factory = new ConnectionFactory();
        factory.HostName = hostName;
        _connection = factory.CreateConnectionAsync().Result;
        _channel = _connection.CreateChannelAsync().Result;

        _channel.ExchangeDeclareAsync(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);
    }
    public async Task CreateQueue(string queueName, List<string> routingKeys)
    {
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        foreach (var routingKey in routingKeys)
        {
            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: _exchangeName,
                routingKey: routingKey);
        }
    }
    public async Task PublishMessage<T>(T message, string routingKey)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await _channel.BasicPublishAsync(_exchangeName, routingKey, body);
    }

    public async Task<AsyncEventingBasicConsumer> CreateConsumerAsync(string queueName)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: true,
                consumer: consumer);

        return consumer;
    }
    public async ValueTask DisposeAsync()
    {
       await _channel.CloseAsync();
       await _connection.CloseAsync();
    }
}
