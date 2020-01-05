using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Bedrock.Framework.Protocols;

namespace Pubbie
{
    public class MessageReaderWriter : IMessageReader<Message>, IMessageWriter<Message>
    {
        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out Message message)
        {
            var reader = new SequenceReader<byte>(input);

            if (!reader.TryReadLittleEndian(out long id))
            {
                consumed = input.Start;
                examined = input.End;
                message = default;
                return false;
            }

            message = new Message
            {
                Id = id,
            };

            if (!reader.TryRead(out var type))
            {
                consumed = input.Start;
                examined = input.End;
                message = default;
                return false;
            }

            message.MessageType = (MessageType)type;

            if (!reader.TryReadLittleEndian(out int length) ||
                 reader.Remaining < length)
            {
                consumed = input.Start;
                examined = input.End;
                message = default;
                return false;
            }

            var topicPayload = input.Slice(reader.Position, length);
            message.Topic = Encoding.UTF8.GetString(topicPayload.IsSingleSegment ? topicPayload.FirstSpan : topicPayload.ToArray());
            reader.Advance(length);

            if (reader.TryReadLittleEndian(out length))
            {
                if (reader.Remaining < length)
                {
                    consumed = input.Start;
                    examined = input.End;
                    message = default;
                    return false;
                }

                var payload = input.Slice(reader.Position, length);
                message.Payload = payload.IsSingleSegment ? payload.First : payload.ToArray();
                reader.Advance(length);

                consumed = reader.Position;
                examined = consumed;
            }
            else
            {
                consumed = reader.Position;
                examined = input.End;
            }

            return true;
        }

        public void WriteMessage(Message message, IBufferWriter<byte> output)
        {
            var idBytes = output.GetSpan(8);
            BinaryPrimitives.WriteInt64LittleEndian(idBytes, message.Id);
            output.Advance(8);

            var messageTypeBytes = output.GetSpan(1);
            messageTypeBytes[0] = (byte)message.MessageType;
            output.Advance(1);

            var topicBytesCount = Encoding.UTF8.GetByteCount(message.Topic);
            var topic = output.GetSpan(4);
            BinaryPrimitives.WriteInt32LittleEndian(topic, topicBytesCount);
            output.Advance(4);

            var topicBytes = output.GetSpan(topicBytesCount);
            Encoding.UTF8.GetBytes(message.Topic, topicBytes);
            output.Advance(topicBytesCount);

            var payloadLengthPrefix = output.GetSpan(4);
            BinaryPrimitives.WriteInt32LittleEndian(payloadLengthPrefix, message.Payload.Length);
            output.Advance(4);

            output.Write(message.Payload.Span);
        }
    }
}
