using RemoteControlLAN.Shared.Messages;
using Xunit;

namespace RemoteControlLAN.Tests;

public sealed class MessageEnvelopeTests
{
    [Fact]
    public void Create_UsesCamelCasePayload_AndRoundTrips()
    {
        var message = MessageEnvelope.Create("COMMAND", "LIST_DIR", new ListDirPayload { Path = "/tmp/demo" }, "session", "agent");
        var json = System.Text.Json.JsonSerializer.Serialize(message, JsonConfig.Default);
        Assert.Contains("sessionId", json); Assert.Contains("/tmp/demo", json);
        var restored = System.Text.Json.JsonSerializer.Deserialize<MessageEnvelope>(json, JsonConfig.Default);
        Assert.Equal("/tmp/demo", restored!.GetPayload<ListDirPayload>()!.Path);
    }
}
