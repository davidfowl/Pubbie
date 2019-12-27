using System;

namespace Pubbie
{
    public class Message
    {
        public long Id { get; set; }

        public MessageType MessageType { get; set; }

        public string Topic { get; set; }

        public ReadOnlyMemory<byte> Payload { get; set; }

        public override string ToString()
        {
            return $"Id={Id}, MessageType={MessageType.ToString()}, Topic={Topic}, Payload={Payload.Length}";
        }
    }
}
