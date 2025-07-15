namespace Zooscape.Infrastructure.SignalRHub.Messages;

public class OutgoingMessages
{
    public static readonly OutgoingMessages Registered = new("Registered");
    public static readonly OutgoingMessages Connect = new("Connect");
    public static readonly OutgoingMessages Disconnect = new("Disconnect");
    public static readonly OutgoingMessages StartGame = new("StartGame");
    public static readonly OutgoingMessages EndGame = new("EndGame");
    public static readonly OutgoingMessages GameState = new("GameState");

    private readonly string _value;

    private OutgoingMessages(string value)
    {
        _value = value;
    }

    public static implicit operator string(OutgoingMessages value)
    {
        return value._value;
    }
}
