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
                            .Select("MCP*")
                            .Select("AzureOpenAI*")
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

            IConfigurationRefresherProvider refresherProvider = host.Services.GetRequiredService<IConfigurationRefresherProvider>();

            LLMService openAIService = host.Services.GetRequiredService<LLMService>();

            Console.WriteLine("Ask me anything! Type 'exit' to quit.");

            while (true)
            {
                // Refresh the configuration to get the latest values
                foreach (var refresher in refresherProvider.Refreshers)
                {
                    _ = await refresher.TryRefreshAsync();
                }

                // Read user input and send it to the OpenAI service
                // The user can type 'exit' to quit the loop
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
