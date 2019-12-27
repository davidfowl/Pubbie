using Pubbie;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
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
                if (line == null)
                {
                    break;
                }
                index = (index + 1) % topics.Length;
                await client.PublishAsync(topics[index], Encoding.UTF8.GetBytes(line));
            }
        }
    }
}
