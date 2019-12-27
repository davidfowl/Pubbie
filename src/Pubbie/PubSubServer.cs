using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;

namespace Pubbie
{
    public class PubSubServer : ConnectionHandler
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ProtocolWriter<Message>>> _topics = new ConcurrentDictionary<string, ConcurrentDictionary<string, ProtocolWriter<Message>>>();

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            var protocol = new MessageProtocol();
            var reader = Protocol.CreateReader(connection, protocol);
            var writer = Protocol.CreateWriter(connection, protocol);

            while (true)
            {
                var result = await reader.ReadAsync();
                var message = result.Message;

                if (result.IsCompleted)
                {
                    break;
                }

                switch (message.MessageType)
                {
                    case MessageType.Publish:
                        {
                            if (_topics.TryGetValue(message.Topic, out var clients))
                            {
                                var data = new Message
                                {
                                    MessageType = MessageType.Data,
                                    Payload = message.Payload,
                                    Topic = message.Topic
                                };

                                // TODO: Use WhenAll
                                foreach (var pair in clients)
                                {
                                    await pair.Value.WriteAsync(data);
                                }
                            }

                            await writer.WriteAsync(new Message
                            {
                                Id = message.Id,
                                Topic = message.Topic,
                                MessageType = MessageType.Success
                            });
                        }
                        break;
                    case MessageType.Subscribe:
                        var topics = _topics.GetOrAdd(message.Topic, (k) => new ConcurrentDictionary<string, ProtocolWriter<Message>>());

                        topics.TryAdd(connection.ConnectionId, writer);

                        await writer.WriteAsync(new Message
                        {
                            Id = message.Id,
                            Topic = message.Topic,
                            MessageType = MessageType.Success
                        });
                        break;
                    case MessageType.Unsubscribe:
                        {
                            if (_topics.TryGetValue(message.Topic, out var clients))
                            {
                                clients.TryRemove(connection.ConnectionId, out _);
                                // TODO: Remove topic from dictionary
                            }

                            await writer.WriteAsync(new Message
                            {
                                Id = message.Id,
                                Topic = message.Topic,
                                MessageType = MessageType.Success
                            });
                        }
                        break;
                    default:
                        break;
                }

                reader.Advance();
            }
        }
    }
}
