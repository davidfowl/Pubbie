using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;

namespace Pubbie
{
    public class PubSubServer : ConnectionHandler
    {
        private readonly TopicStore _topics;

        public PubSubServer(TopicStore topicStore)
        {
            _topics = topicStore;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            var protocol = new MessageProtocol();
            var reader = connection.CreateReader(protocol);
            var writer = connection.CreateWriter(protocol);
            var connectionTopics = new List<string>();

            try
            {
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
                                if (_topics.TryGetValue(message.Topic, out var topic))
                                {
                                    var data = new Message
                                    {
                                        MessageType = MessageType.Data,
                                        Payload = message.Payload,
                                        Topic = message.Topic
                                    };

                                    topic.IncrementPublish();

                                    // TODO: Use WhenAll
                                    foreach (var pair in topic)
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
                            {
                                var topic = _topics.GetOrAdd(message.Topic);

                                topic.Add(connection.ConnectionId, writer);

                                connectionTopics.Add(message.Topic);

                                await writer.WriteAsync(new Message
                                {
                                    Id = message.Id,
                                    Topic = message.Topic,
                                    MessageType = MessageType.Success
                                });
                            }
                            break;
                        case MessageType.Unsubscribe:
                            {
                                RemoveTopic(connection, connectionTopics, message.Topic);

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
            finally
            {
                for (int i = connectionTopics.Count - 1; i >= 0; i--)
                {
                    RemoveTopic(connection, connectionTopics, connectionTopics[i]);
                }
            }
        }

        private void RemoveTopic(ConnectionContext connection, List<string> topics, string topicName)
        {
            if (_topics.TryGetValue(topicName, out var topic))
            {
                topic.Remove(connection.ConnectionId);
                // TODO: Remove topic from dictionary
            }

            topics.Remove(topicName);
        }
    }
}
