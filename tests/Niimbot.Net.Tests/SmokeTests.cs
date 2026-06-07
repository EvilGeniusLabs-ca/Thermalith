using Niimbot.Net;
using Xunit;

namespace Niimbot.Net.Tests;

public class SmokeTests
{
    [Fact]
    public void TransportState_has_expected_members()
    {
        Assert.Equal(TransportState.Disconnected, default);
    }
}
