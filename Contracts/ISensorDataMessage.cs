namespace Contracts
{
    public interface ISensorDataMessage
    {
        double WindSpeed { get; }
        double WindDirection { get; }
        DateTime Datestamp { get; }
    }
}
