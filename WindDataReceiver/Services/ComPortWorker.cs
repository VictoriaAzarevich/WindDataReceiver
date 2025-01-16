using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace WindDataReceiver.Services
{
    public class ComPortWorker : BackgroundService
    {
        private readonly ILogger<ComPortWorker> _logger;
        private readonly SerialPort _serialPort;
        private readonly StringBuilder _dataBuffer;
        private const int NumberOfBits = 15;
        private const char StartChar = '$';
        private const string EndChars = "\r\n";

        public ComPortWorker(ILogger<ComPortWorker> logger)
        {
            _logger = logger;
            _serialPort = new SerialPort
            {
                PortName = "COM2",
                BaudRate = 2400,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One
            };
            _dataBuffer = new StringBuilder();
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

                            ProcessBuffer();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Data reading error: {ex.Message}.");
                    }

                    await Task.Delay(1000, stoppingToken);
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


        private void ProcessBuffer()
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
                    ProcessData(message);
                }
                else
                {
                    _dataBuffer.Remove(0, startIndex);
                    _logger.LogWarning("Incorrect package.");
                    break;
                }
            }

            if (_dataBuffer.Length >= NumberOfBits)
            {
                if (bufferContent.Contains(StartChar))
                {
                    while (bufferContent.Count(c => c == StartChar) != 1)
                    {
                        _dataBuffer.Remove(0, bufferContent.IndexOf(StartChar, 1));
                        bufferContent = _dataBuffer.ToString();
                    }

                    _logger.LogWarning("Incorrect package.");
                }
                else
                {
                    _dataBuffer.Clear();
                    _logger.LogWarning("Incorrect package.");
                }
            }
        }

        private void ProcessData(string data)
        {
            string pattern = @"^\$\d+(\.\d+)?\,\d+(\.\d+)?\r$";

            if (Regex.IsMatch(data, pattern))
            {
                int startIndex = data.IndexOf(StartChar) + 1;
                int endIndex = data.IndexOf(',');
                string ws = data.Substring(startIndex, endIndex - 1);
                string wd = data.Substring(endIndex + 1).Trim();
                _logger.LogInformation($"ws = {ws}, wd = {wd}");
            }
            else
            {
                _logger.LogWarning($"Parsing error: {data}.");
            }
        }
    }
}
