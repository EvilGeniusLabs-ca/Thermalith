using Niimbot.Net.Commands;

namespace Niimbot.Net;

/// <summary>Raised when the printer reports an error during a print or rejects a command.</summary>
public class PrintException : Exception
{
    public PrintException(string message, PrinterErrorCode? code = null) : base(message)
    {
        Code = code;
    }

    /// <summary>The device error code, when one was reported.</summary>
    public PrinterErrorCode? Code { get; }
}

/// <summary>Raised when a command receives no response within its timeout.</summary>
public class NiimbotTimeoutException : Exception
{
    public NiimbotTimeoutException(string message) : base(message) { }
}
