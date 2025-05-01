using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Types;
using OpenAI.Chat;
using System.Text.Json;
using ModelContextProtocolClientConfiguration;

namespace ConsoleChatApp;

public class AzureOpenAISerivce
{
    private readonly AzureOpenAISerivceOptions _options;
    private readonly ChatClient _chatClient;
    private ConfiguredMcpClient _mcpClient;


    public AzureOpenAISerivce(IOptions<AzureOpenAISerivceOptions> options, ConfiguredMcpClient mcpClient)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));

        _chatClient = new AzureOpenAIClient(
            new Uri(_options.Endpoint),
            new DefaultAzureCredential())
            .GetChatClient(_options.Deployment);

        _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
    }

    public async Task<string> SendMessageAsync(ChatMessage input)
    {
        IList<McpClientTool> tools = await _mcpClient.ListToolsAsync();

        ChatCompletionOptions options = new ChatCompletionOptions();

        foreach (var tool in tools)
        {
            options.Tools.Add(
                ChatTool.CreateFunctionTool(
                    functionName: tool.Name,
                    functionDescription: tool.Description,
                    functionParameters: BinaryData.FromObjectAsJson(tool.JsonSchema)));
        }

        List<ChatMessage> messages =
        [
            input
        ];

        bool requiresAction;

        do
        {
            requiresAction = false;

            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    {
                        // Add the assistant message to the conversation history.
                        messages.Add(new AssistantChatMessage(completion));
                        break;
                    }

                case ChatFinishReason.ToolCalls:
                    {
                        // First, add the assistant message with tool calls to the conversation history.
                        messages.Add(new AssistantChatMessage(completion));

                        // Then, add a new tool message for each tool call that is resolved.
                        foreach (ChatToolCall toolCall in completion.ToolCalls)
                        {
                            Console.WriteLine($"System: Tool call - {toolCall.FunctionName}");

                            McpClientTool? tool = tools.FirstOrDefault(t => t.Name == toolCall.FunctionName);

                            if (tool != null)
                            {
                                var response = await tool.InvokeAsync(
                                    new Microsoft.Extensions.AI.AIFunctionArguments(JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                        toolCall.FunctionArguments,
                                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                                    )));
                                    

                                CallToolResponse toolResponse = JsonSerializer.Deserialize<CallToolResponse>((JsonElement)response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                string result = toolResponse.Content.First(c => c.Type == "text").Text;

                                messages.Add(new ToolChatMessage(toolCall.Id, result));

                                Console.WriteLine($"System: Tool call result - {result}");
                                }
                        }

                        requiresAction = true;
                        break;
                    }

                case ChatFinishReason.Length:
                    throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                case ChatFinishReason.ContentFilter:
                    throw new NotImplementedException("Omitted content due to a content filter flag.");

                case ChatFinishReason.FunctionCall:
                    throw new NotImplementedException("Deprecated in favor of tool calls.");

                default:
                    throw new NotImplementedException(completion.FinishReason.ToString());
            }
        } while (requiresAction);

        return messages[^1].Content[0].Text;
    }
}
