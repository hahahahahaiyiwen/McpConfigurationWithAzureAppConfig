# MCP Configuration with Azure App Configuration

This demo project illustrates how to use Azure App Configuration to dynamically configure and connect to different Model Context Protocol (MCP) servers without application redeployment.

## Overview

The Model Context Protocol (MCP) enables AI-powered applications to connect to various AI model servers. This project demonstrates how to:

1. Store MCP server configurations in Azure App Configuration
2. Load and parse these configurations at runtime
3. Switch between different MCP servers dynamically

## Setup Azure App Configuration

1. Create an Azure App Configuration store in the Azure Portal
2. Add the following key-values to your configuration store:

    **For an Azure MCP Server (stdio type):**
    Key: MCP:AzureMCPServer
    Value:
    ```json
    {
        "name": "Azure MCP Server",
        "type": "stdio",
        "command": "npx",
        "args": [
            "-y",
            "@azure/mcp@latest",
            "server",
            "start"
        ]
    }
    ```

    **For a Remote SSE Server:**  
    Key: MCP:Fetch
    ```json
    Value: 
    {
        "name": "Fetch",
        "type": "sse",
        "url": "https://remote.mcpservers.org/fetch"
    }
    ```

3. Note your Azure App Configuration endpoint URL for the next step

## Project Configuration

1. Configure your application to use Azure App Configuration:
```
// Program.cs
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(builder.Configuration.GetConnectionString("You-Azure-App-Configuration-Endpoint"))
        .Select("MCP*")
        .Select("AzureOpenAI*");
});
```