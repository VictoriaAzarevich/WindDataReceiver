namespace WindDataReceiver.MessageBroker
{
    public interface IRabbitMQPublisher
    {
        Task PublishMessageAsync<T>(T message, string queueName);
    }
}
