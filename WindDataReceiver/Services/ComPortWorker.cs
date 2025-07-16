using Contracts;
using MassTransit;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace WindDataReceiver.Services
{
    public class ComPortWorker(ILogger<ComPortWorker> logger,
        IPublishEndpoint publishEndpoint) : BackgroundService
    {
        private readonly ILogger<ComPortWorker> _logger = logger;
        private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
        private readonly SerialPort _serialPort = new()
        {
            PortName = "COM9",
            BaudRate = 2400,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One
        };
        private readonly StringBuilder _dataBuffer = new();
        private const int NumberOfBits = 15;
        private const char StartChar = '$';
        private const string EndChars = "\r\n";
        private static readonly Regex _sensorRegex = SensorRegexFactory.SensorRegex();

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

                    await Task.Delay(50, stoppingToken);
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
                }
                else
                {
                    _logger.LogWarning($"Incorrect package: {bufferContent}.");
                    _dataBuffer.Clear();
                }
            }
        }

        private async Task ProcessDataAsync(string data)
        {
            var match = _sensorRegex.Match(data);
            if (match.Success)
            {
                double windSpeed = double.Parse(match.Groups["ws"].Value, NumberStyles.Any, CultureInfo.InvariantCulture);
                double windDirection = double.Parse(match.Groups["wd"].Value, NumberStyles.Any, CultureInfo.InvariantCulture);

                await _publishEndpoint.Publish<ISensorDataMessage>(new
                {
                    WindSpeed = windSpeed,
                    WindDirection = windDirection,
                    Datestamp = DateTime.UtcNow
                });

                _logger.LogInformation($"ws = {windSpeed}, wd = {windDirection}");
            }
            else
            {
                _logger.LogWarning($"Parsing error: {data}.");
            }
        }
    }

    public static partial class SensorRegexFactory
    {
        [GeneratedRegex(@"^\$(?<ws>\d+(\.\d+)?),(?<wd>\d+(\.\d+)?)\r?\n?$")]
        public static partial Regex SensorRegex();
    }
}
