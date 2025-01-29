using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;

namespace WindDataReceiver.MessageBroker
{
    public class RabbitMQPublisher : IRabbitMQPublisher
    {
        private readonly ILogger<RabbitMQPublisher> _logger;
        private readonly RabbitMQSetting _rabbitMQSetting;

        public RabbitMQPublisher(ILogger<RabbitMQPublisher> logger, IOptions<RabbitMQSetting> rabbitMQSetting)
        {
            _logger = logger;
            _rabbitMQSetting = rabbitMQSetting.Value;
        }

        public async Task PublishMessageAsync<T>(T message, string queueName)
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMQSetting.HostName,
                UserName = _rabbitMQSetting.UserName,
                Password = _rabbitMQSetting.Password,
            };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();
            await channel.QueueDeclareAsync(queue: queueName, durable: true,
                exclusive: false, autoDelete: false, arguments: null);

            var messageJson = JsonConvert.SerializeObject(message);
            var body = Encoding.UTF8.GetBytes(messageJson);

            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName,
                mandatory: true, basicProperties: properties, body: body);

            _logger.LogInformation($"Sent: {messageJson}");
        }
    }
}
