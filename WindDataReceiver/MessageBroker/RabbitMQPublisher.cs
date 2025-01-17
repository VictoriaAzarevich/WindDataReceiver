using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;

namespace WindDataReceiver.MessageBroker
{
    public class RabbitMQPublisher<T> : IRabbitMQPublisher<T>
    {
        private readonly RabbitMQSetting _rabbitMQSsetting;

        public RabbitMQPublisher(IOptions<RabbitMQSetting> rabbitMQSetting)
        {
            _rabbitMQSsetting = rabbitMQSetting.Value;
        }

        public async Task PublishMessageAsync(T message, string queueName)
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMQSsetting.HostName,
                UserName = _rabbitMQSsetting.UserName,
                Password = _rabbitMQSsetting.Password,
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

            Console.WriteLine($"[x] Sent: {messageJson}");
        }
    }
}
