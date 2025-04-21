using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI.Chat;

namespace MCPConfig
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            IConfigurationRefresher _refresher = null;
            
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    // Add the Azure App Configuration to the host's configuration
                    config.AddAzureAppConfiguration(options =>
                    {
                        options.Connect(new Uri("https://haiyiwen-aiconfig-demo.azconfig.io"), new DefaultAzureCredential())
                            .Select("MCP")
                            .ConfigureRefresh(refresh =>
                            {
                                refresh.RegisterAll().SetRefreshInterval(TimeSpan.FromSeconds(30));
                            });
                    });
                })
                .ConfigureServices((context, services) =>
                
                {
                    services.AddAzureAppConfiguration();

                    services.AddSingleton<ConfiguredMCPClient>();

                    services.AddSingleton<LLMService>();

                    services.Configure<LLMServiceOptions>(context.Configuration.GetSection("AzureOpenAI"));
                });

            var host = builder.Build();

            IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

            var openAIService = host.Services.GetRequiredService<LLMService>();

            Console.WriteLine("Ask me anything! Type 'exit' to quit.");

            while (true)
            {
                Console.Write("You: ");
                var userInput = Console.ReadLine();

                if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                Console.WriteLine($"Agent: {await openAIService.SendMessageAsync(new UserChatMessage(userInput))}");
            }
        }
    }
}
