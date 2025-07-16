using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using WindDataReceiver.MessageBroker;

namespace WindDataReceiver.Services
{
    public class ComPortWorker : BackgroundService
    {
        private readonly ILogger<ComPortWorker> _logger;
        private readonly SerialPort _serialPort;
        private readonly StringBuilder _dataBuffer;
        private readonly IRabbitMQPublisher _rabbitMQPublisher;
        private const int NumberOfBits = 15;
        private const char StartChar = '$';
        private const string EndChars = "\r\n";
        private const char SplitChar = ',';

        public ComPortWorker(ILogger<ComPortWorker> logger, IRabbitMQPublisher rabbitMQPublisher)
        {
            _logger = logger;
            _serialPort = new SerialPort
            {
                PortName = "COM9",
                BaudRate = 2400,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One
            };
            _dataBuffer = new StringBuilder();
            _rabbitMQPublisher = rabbitMQPublisher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _serialPort.Open();
                _logger.LogInformation("COM is open.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        string data = _serialPort.ReadExisting();

                        if (data != "")
                        {
                            _dataBuffer.Append(data);
                            _logger.LogInformation($"Received data: {data}.");

                            await ProcessBufferAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Data reading error: {ex.Message}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Port opening error: {ex.Message}.");
            }
            finally
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                    _logger.LogInformation("COM is closed.");
                }
            }
        }


        private async Task ProcessBufferAsync()
        {
            string bufferContent = _dataBuffer.ToString();

            while (bufferContent.Contains(StartChar) && bufferContent.Contains(EndChars))
            {
                int startIndex = bufferContent.IndexOf(StartChar);
                int endIndex = bufferContent.IndexOf(EndChars, startIndex);

                if (endIndex > startIndex)
                {
                    string message = bufferContent.Substring(startIndex, endIndex - startIndex + 2);
                    _dataBuffer.Remove(0, endIndex + 2);
                    bufferContent = _dataBuffer.ToString();
                    await ProcessDataAsync(message);
                }
                else
                {
                    _logger.LogWarning($"Incorrect package: {_dataBuffer}.");
                    _dataBuffer.Remove(0, startIndex);
                    // await _rabbitMQPublisher.PublishMessageAsync("Incorrect package", RabbitMQQueues.WindDataQueue);
                    break;
                }
            }

            if (_dataBuffer.Length >= NumberOfBits)
            {
                if (bufferContent.Contains(StartChar))
                {
                    while (bufferContent.Count(c => c == StartChar) != 1)
                    {
                        _logger.LogWarning($"Incorrect package: {_dataBuffer}.");
                        _dataBuffer.Remove(0, bufferContent.IndexOf(StartChar, 1));
                        bufferContent = _dataBuffer.ToString();
                    }
                    //await _rabbitMQPublisher.PublishMessageAsync("Incorrect package", RabbitMQQueues.WindDataQueue);
                }
                else
                {
                    _logger.LogWarning($"Incorrect package: {bufferContent}.");
                    _dataBuffer.Clear();
                    // await _rabbitMQPublisher.PublishMessageAsync("Incorrect package", RabbitMQQueues.WindDataQueue);
                }
            }
        }

        private async Task ProcessDataAsync(string data)
        {
            // string pattern = @"^\$\d+(\.\d+)?\,\d+(\.\d+)?\r$";

            if (SensorRegexFactory.SensorRegex().IsMatch(data))
            {
                int startIndex = data.IndexOf(StartChar) + 1;
                int endIndex = data.IndexOf(SplitChar);
                string ws = data.Substring(startIndex, endIndex - 1);
                string wd = data.Substring(endIndex + 1).Trim();
                WindData windData = new WindData
                {
                    WindSpeed = double.Parse(ws, NumberStyles.Any, CultureInfo.InvariantCulture),
                    WindDirection = double.Parse(wd, NumberStyles.Any, CultureInfo.InvariantCulture),
                    Datestamp = DateTime.UtcNow
                };
                await _rabbitMQPublisher.PublishMessageAsync(windData, RabbitMQQueues.WindDataQueue);
                _logger.LogInformation($"ws = {ws}, wd = {wd}");
            }
            else
            {
                // await _rabbitMQPublisher.PublishMessageAsync("Parsing error", RabbitMQQueues.WindDataQueue);
                _logger.LogWarning($"Parsing error: {data}.");
            }
        }
    }

    public static partial class SensorRegexFactory
    {
        [GeneratedRegex(@"^\$\d + (\.\d +)?\,\d+(\.\d+)?\r$")]
        public static partial Regex SensorRegex();
    }
}
