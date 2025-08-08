using System.Text;
using Xunit;
using RealtimeDialog.Core.Protocol;
using Microsoft.Extensions.Logging;
using Moq;

namespace RealtimeDialog.Tests;

public class BinaryProtocolTests
{
    [Fact]
    public void Marshal_ShouldSerializeMessage()
    {
        // Arrange
        var message = new Message
        {
            Type = MsgType.FullClient,
            Payload = Encoding.UTF8.GetBytes("test payload")
        };
        var protocol = new BinaryProtocol();

        // Act
        byte[] result = protocol.Marshal(message);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
        public void Unmarshal_ShouldDeserializeMessage()
        {
            // Arrange
            var originalMessage = new Message
            {
                Type = MsgType.FullClient,
                Payload = Encoding.UTF8.GetBytes("test payload")
            };
            var protocol = new BinaryProtocol();
             protocol.SetVersion(VersionBits.Version1);
             protocol.SetHeaderSize(HeaderSizeBits.HeaderSize16);
             protocol.SetSerialization(SerializationBits.Raw);
             protocol.SetCompression(CompressionBits.None);
            
            byte[] serializedData = protocol.Marshal(originalMessage);
            var mockLogger = new Mock<ILogger<BinaryProtocol>>();
            ContainsSequenceFunc containsSequence = (bits) => false;

            // Act
            var result = BinaryProtocol.Unmarshal(serializedData, containsSequence, mockLogger.Object);

            // Assert
            Assert.Equal(originalMessage.Type, result.message.Type);
            Assert.Equal(originalMessage.Payload, result.message.Payload);
        }


}