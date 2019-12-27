using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pubbie;

namespace Sample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var topicStore = new TopicStore();

            var host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(topicStore);
                    })
                    .ConfigureWebHostDefaults(b =>
                    {
                        b.UseKestrel(o =>
                        {
                            o.ListenAnyIP(8000, builder => builder.UseConnectionHandler<PubSubServer>());
                            o.ListenAnyIP(5000);
                        });

                        b.Configure(app =>
                        {
                            app.UseRouting();

                            app.UseEndpoints(e =>
                            {
                                e.MapGet("/topics", async context =>
                                {
                                    var response = from t in topicStore
                                                   select new
                                                   {
                                                       Name = t.Key,
                                                       Subscribers = t.Value.SubscriberCount,
                                                       Messages = t.Value.PublishCount
                                                   };

                                    context.Response.ContentType = "application/json";
                                    await JsonSerializer.SerializeAsync(context.Response.Body, response);
                                });

                                e.MapGet("/topics/{name}", async context =>
                                {
                                    var name = (string)context.Request.RouteValues["name"];
                                    context.Response.ContentType = "application/json";

                                    if (!topicStore.TryGetValue(name, out var t))
                                    {
                                        context.Response.StatusCode = 404;
                                        await JsonSerializer.SerializeAsync(context.Response.Body, new
                                        {
                                            Message = "No such topic found"
                                        });
                                        return;
                                    }

                                    await JsonSerializer.SerializeAsync(context.Response.Body, new
                                    {
                                        Name = name,
                                        Subscribers = t.SubscriberCount,
                                        Messages = t.PublishCount
                                    });
                                });
                            });
                        });
                    })
                    .Build();

            await host.RunAsync();
        }
    }
}