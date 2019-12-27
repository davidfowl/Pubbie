using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pubbie;

namespace Sample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            _ = RunServerAsync();

            await using var client = new PubSubClient();

            await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 8000));

            var topics = new string[]
            {
                "topic1",
                "topic2",
                "topic3"
            };

            foreach (var topic in topics)
            {
                await client.SubscribeAsync(topic, (t, data) =>
                {
                    Console.WriteLine($"[Client][{t}] <- " + Encoding.UTF8.GetString(data.Span));
                    return Task.CompletedTask;
                });
            }

            var index = 0;
            while (true)
            {
                var line = Console.ReadLine();
                index = (index + 1) % topics.Length;
                await client.PublishAsync(topics[index], Encoding.UTF8.GetBytes(line));
            }
        }

        static async Task RunServerAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                // builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });
            var sp = services.BuildServiceProvider();

            var server = new ServerBuilder(sp)
                   .UseSockets(sockets =>
                   {
                       sockets.ListenAnyIP(8000, builder => builder.UseConnectionHandler<PubSubServer>());

                   }).Build();

            await server.StartAsync();

            foreach (var ep in server.EndPoints)
            {
                Console.WriteLine($"Servers listening on {ep}");
            }

            var tcs = new TaskCompletionSource<object>();
            Console.CancelKeyPress += (sender, e) => tcs.TrySetResult(null);
            await tcs.Task;

            await server.StopAsync();
        }
    }
}