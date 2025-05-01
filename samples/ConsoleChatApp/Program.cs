﻿using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocolClientConfiguration;
using OpenAI.Chat;

namespace ConsoleChatApp;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                // Add the Azure App Configuration to the host's configuration
                config.AddAzureAppConfiguration(options =>
                {
                    string endpoint = Environment.GetEnvironmentVariable("AZURE_APPCONFIG_ENDPOINT") ?? throw new ArgumentNullException("AZURE_APPCONFIG_ENDPOINT");

                    options.Connect(new Uri(endpoint), new DefaultAzureCredential())
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

                services.AddSingleton<ConfiguredMcpClient>(sp =>
                    new ConfiguredMcpClient(sp.GetRequiredService<IConfiguration>(), new ConfiguredMcpClientOptions()));

                services.AddSingleton<AzureOpenAISerivce>();

                services.Configure<AzureOpenAISerivceOptions>(context.Configuration.GetSection("AzureOpenAI"));
            });

        var host = builder.Build();

        IConfigurationRefresherProvider refresherProvider = host.Services.GetRequiredService<IConfigurationRefresherProvider>();

        AzureOpenAISerivce openAIService = host.Services.GetRequiredService<AzureOpenAISerivce>();

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