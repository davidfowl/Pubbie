using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace Pubbie.Tests
{
    public class MessageReaderWriterTests
    {
        const int IdBytesCount = 8;
        const int MessageTypeBytesCount = 1;
        const int LengthBytesCount = 4;

        [Fact]
        public void WriteMessage_ShouldWriteId()
        {
            // Arrange
            var writer = new MessageReaderWriter();
            var message = new Message
            {
                Id = 1,
                Topic = string.Empty
            };
            var buffer = new ArrayBufferWriter<byte>();

            // Act
            writer.WriteMessage(message, buffer);

            // Assert
            var idBytes = buffer.WrittenSpan.Slice(0, IdBytesCount);
            var id = BinaryPrimitives.ReadInt64LittleEndian(idBytes);
            Assert.Equal(1, id);
        }

        [Theory]
        [InlineData(0, MessageType.Error)]
        [InlineData(1, MessageType.Success)]
        [InlineData(2, MessageType.Data)]
        [InlineData(3, MessageType.Publish)]
        [InlineData(4, MessageType.Subscribe)]
        [InlineData(5, MessageType.Unsubscribe)]
        public void WriteMessage_ShouldWriteMessageType(byte expectedByte, MessageType messageType)
        {
            // Arrange
            var writer = new MessageReaderWriter();
            var message = new Message
            {
                MessageType = messageType,
                Topic = string.Empty
            };
            var buffer = new ArrayBufferWriter<byte>();

            // Act
            writer.WriteMessage(message, buffer);

            // Assert
            var typeByte = buffer.WrittenSpan[IdBytesCount];
            Assert.Equal(expectedByte, typeByte);
        }

        [Fact]
        public void WriteMessage_ShouldWriteTopic()
        {
            //TODO: Test topic with non UTF8 characters

            // Arrange
            var writer = new MessageReaderWriter();
            var message = new Message
            {
                Topic = "foo"
            };
            var buffer = new ArrayBufferWriter<byte>();

            // Act
            writer.WriteMessage(message, buffer);

            // Assert
            var lengthBytes = buffer.WrittenSpan.Slice(IdBytesCount + MessageTypeBytesCount, LengthBytesCount);
            var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            Assert.Equal(3, length);

            var topicBytes = buffer.WrittenSpan.Slice(
                IdBytesCount + MessageTypeBytesCount + LengthBytesCount,
                length);
            var topic = Encoding.UTF8.GetString(topicBytes);
            Assert.Equal("foo", topic);
        }

        [Fact]
        public void WriteMessage_ShouldThrowArgumentNullException_WhenTopicIsNull()
        {
            //TODO: Improve handling of null Topic

            // Arrange
            var writer = new MessageReaderWriter();
            var message = new Message();
            var buffer = new ArrayBufferWriter<byte>();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => writer.WriteMessage(message, buffer));
            Assert.Equal("chars", exception.ParamName);
        }

        [Fact]
        public void WriteMessage_ShouldWritePayload()
        {
            // Arrange
            var writer = new MessageReaderWriter();
            var expectedPayload = new byte[] { 1, 2, 42 };
            var message = new Message
            {
                Topic = string.Empty,
                Payload = new ReadOnlyMemory<byte>(expectedPayload)
            };
            var buffer = new ArrayBufferWriter<byte>();

            // Act
            writer.WriteMessage(message, buffer);

            // Assert
            var lengthBytes = buffer.WrittenSpan.Slice(
                IdBytesCount + MessageTypeBytesCount + LengthBytesCount,
                LengthBytesCount);
            var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            Assert.Equal(3, length);

            var payload = buffer.WrittenSpan.Slice(
                IdBytesCount + MessageTypeBytesCount + LengthBytesCount + LengthBytesCount,
                length);
            Assert.Equal(expectedPayload, payload.ToArray());
        }

        [Fact]
        public void WriteMessage_ShouldNotWriteExtraBytes()
        {
            // Arrange
            var writer = new MessageReaderWriter();
            var topic = "foo";
            var payload = new byte[3];

            var message = new Message
            {
                Id = 1,
                Topic = topic,
                Payload = new ReadOnlyMemory<byte>(payload)
            };
            var buffer = new ArrayBufferWriter<byte>();

            // Act
            writer.WriteMessage(message, buffer);

            // Assert
            Assert.Equal(
                IdBytesCount + MessageTypeBytesCount + LengthBytesCount + topic.Length + LengthBytesCount + payload.Length,
                buffer.WrittenCount);
        }
    }
}
