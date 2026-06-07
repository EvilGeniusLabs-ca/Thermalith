namespace Niimbot.Net.Framing;

/// <summary>Raised when received bytes violate the NIIMBOT framing or a response is malformed.</summary>
public class NiimbotProtocolException : Exception
{
    public NiimbotProtocolException(string message) : base(message) { }

    public NiimbotProtocolException(string message, Exception inner) : base(message, inner) { }
}
