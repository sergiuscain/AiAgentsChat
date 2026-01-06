using RabbitMQ.Client.Events;

namespace AiAgentServiceLib.Models;
public class ConsumerInfo
{
    public AsyncEventingBasicConsumer Consumer { get; set; }
    public AsyncEventHandler<BasicDeliverEventArgs> Handler { get; set; }
}
