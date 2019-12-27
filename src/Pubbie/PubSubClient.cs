using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Pubbie
{
    public class PubSubClient : IAsyncDisposable
    {
        private readonly Client _client;
        private long _nextOperationId;
        private ProtocolWriter<Message> _writer;
        private ProtocolReader<Message> _reader;
        private ConnectionContext _connection;
        private ConcurrentDictionary<long, TaskCompletionSource<object>> _operations = new ConcurrentDictionary<long, TaskCompletionSource<object>>();
        private ConcurrentDictionary<string, Func<string, ReadOnlyMemory<byte>, Task>> _topics = new ConcurrentDictionary<string, Func<string, ReadOnlyMemory<byte>, Task>>();
        private Task _readingTask;

        public PubSubClient(Client client)
        {
            _client = client;
        }

        public PubSubClient()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });
            var sp = services.BuildServiceProvider();

            _client = new ClientBuilder(sp)
                .UseSockets()
                .Build();
        }

        public async Task ConnectAsync(EndPoint endPoint)
        {
            // REVIEW: Should this be a static factory?
            _connection = await _client.ConnectAsync(endPoint);
            var protocol = new MessageProtocol();
            _writer = _connection.CreateWriter(protocol);
            _reader = _connection.CreateReader(protocol);
            _readingTask = ProcessReadsAsync();
        }

        private async Task ProcessReadsAsync()
        {
            try
            {
                while (true)
                {
                    var result = await _reader.ReadAsync();
                    var message = result.Message;

                    if (result.IsCompleted || result.IsCanceled)
                    {
                        break;
                    }

                    switch (message.MessageType)
                    {
                        case MessageType.Error:
                            {
                                if (_operations.TryRemove(message.Id, out var operation))
                                {
                                    if (message.Payload.Length > 0)
                                    {
                                        operation.TrySetException(new Exception(Encoding.UTF8.GetString(message.Payload.Span)));
                                    }
                                    else
                                    {
                                        operation.TrySetException(new Exception($"Operation {message.Id} failed"));
                                    }
                                }
                            }
                            break;
                        case MessageType.Success:
                            {
                                if (_operations.TryRemove(message.Id, out var operation))
                                {
                                    operation.TrySetResult(null);
                                }
                            }
                            break;
                        case MessageType.Data:
                            if (_topics.TryGetValue(message.Topic, out var callback))
                            {
                                // REVIEW: This will deadlock if the client is used in the 
                                // callback
                                await callback(message.Topic, message.Payload);
                            }
                            break;
                        default:
                            break;
                    }

                    _reader.Advance();
                }
            }
            finally
            {
                await _reader.DisposeAsync();
            }
        }

        public ChannelWriter<T> CreateWriter<T>(string topic, Func<T, ReadOnlyMemory<byte>> serialize)
        {
            var channel = Channel.CreateBounded<T>(1);

            async Task ProcessMessages()
            {
                await foreach (var item in channel.Reader.ReadAllAsync())
                {
                    var message = serialize(item);

                    await PublishAsync(topic, message);
                }
            }

            Task.Run(ProcessMessages);

            return channel.Writer;
        }

        public async IAsyncEnumerable<T> CreateReader<T>(string topic, Func<ReadOnlyMemory<byte>, T> deserialize)
        {
            var channel = Channel.CreateBounded<T>(1);

            await SubscribeAsync(topic, async (t, data) =>
            {
                while (await channel.Writer.WaitToWriteAsync())
                {
                    if (channel.Writer.TryWrite(deserialize(data)))
                    {
                        break;
                    }
                }
            });

            await foreach(var item in channel.Reader.ReadAllAsync())
            {
                yield return item;
            }

            await UnsubscribeAsync(topic);
        }

        public async Task SubscribeAsync(string topic, Func<string, ReadOnlyMemory<byte>, Task> callback)
        {
            if (_topics.TryGetValue(topic, out _))
            {
                return;
            }

            var id = Interlocked.Increment(ref _nextOperationId);

            var operation = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _operations[id] = operation;

            _topics[topic] = callback;

            await _writer.WriteAsync(new Message
            {
                Id = id,
                MessageType = MessageType.Subscribe,
                Topic = topic
            });

            await operation.Task;
        }

        public async Task UnsubscribeAsync(string topic)
        {
            var id = Interlocked.Increment(ref _nextOperationId);

            var operation = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _operations[id] = operation;

            await _writer.WriteAsync(new Message
            {
                Id = id,
                MessageType = MessageType.Unsubscribe,
                Topic = topic
            });

            await operation.Task;

            _topics.TryRemove(topic, out _);
        }

        public async Task PublishAsync(string topic, ReadOnlyMemory<byte> data)
        {
            var id = Interlocked.Increment(ref _nextOperationId);

            var operation = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _operations[id] = operation;

            await _writer.WriteAsync(new Message
            {
                Id = id,
                MessageType = MessageType.Publish,
                Topic = topic,
                Payload = data
            });

            await operation.Task;
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection == null)
            {
                return;
            }

            await _writer.DisposeAsync();

            _reader.Connection.Transport.Input.CancelPendingRead();

            await _readingTask;

            foreach (var operation in _operations)
            {
                _operations.TryRemove(operation.Key, out _);

                // Cancel all pending operations
                operation.Value.TrySetCanceled();
            }

            await _connection.DisposeAsync();
        }
    }
}
