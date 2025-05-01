# Running the Console Chat Application

This guide will help you set up and run the ConsoleChatApp sample using the same MCP configuration that you use locally with vscode and demonstrates how to use Azure App Configuration to dynamically configure Model Context Protocol (MCP) clients to connect to MCP servers.

## Prerequisites

- .NET 9.0 SDK

- Azure CLI

- Azure subscription

- Azure App Configuration instance
    - [Create Azure App Configuration store](https://learn.microsoft.com/en-us/azure/azure-app-configuration/quickstart-azure-app-configuration-create?tabs=azure-portal)
    - [Enable EntraID authentication](https://learn.microsoft.com/en-us/azure/azure-app-configuration/concept-enable-rbac)

- Azure OpenAI service instance
    - Create Azure OpenAI service
    - Enable EntraID authentication

## Setup Instructions

1. Configure Environment Variable

Get your Azure App Configuration store endpoint:

```
   # Login to Azure CLI (if not already logged in)
   az login

   # Get the endpoint of your Azure App Configuration store
   # Replace 'your-resource-group' and 'your-appconfig-name' with your actual values
   az appconfig show --name your-appconfig-name --resource-group your-resource-group --query endpoint --output tsv
```

Set the AZURE_APPCONFIG_ENDPOINT environment variable to your Azure App Configuration store endpoint:

Windows (Command Prompt)
```
setx AZURE_APPCONFIG_ENDPOINT "https://your-appconfig-instance.azconfig.io"
```
Windows (PowerShell)
```
$env:AZURE_APPCONFIG_ENDPOINT = "https://your-appconfig-instance.azconfig.io"
```
macOS/Linux
```
export AZURE_APPCONFIG_ENDPOINT="https://your-appconfig-instance.azconfig.io"
```

2. Save AzureOpenAI configuration in Azure App Configuration store

```bash
#!/bin/bash

# Add Azure OpenAI configuration
az appconfig kv set --name your-appconfig-name --key "AzureOpenAI:Endpoint" --value your-azureopenai-endpoint --yes

az appconfig kv set --name your-appconfig-name --key "AzureOpenAI:Deployment" --value your-azureopenai-deployment --yes
```

3. Save VSCODE MCP configuration in Azure App Configuration store
```bash
#!/bin/bash
# convert-vscode-mcp.sh

# Variables - replace with your values
APPCONFIG_NAME="your-appconfig-name"
RESOURCE_GROUP="your-resource-group"
MCP_CONFIG_FILE=".vscode/mcp.json"

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "jq is required but not installed. Please install it first."
    echo "Install instructions: https://stedolan.github.io/jq/download/"
    exit 1
fi

# Check if file exists
if [ ! -f "$MCP_CONFIG_FILE" ]; then
    echo "Error: VSCODE MCP configuration file not found at $MCP_CONFIG_FILE"
    exit 1
fi

# Check if logged in to Azure CLI
echo "Checking Azure CLI login status..."
if ! az account show &> /dev/null; then
    echo "You need to login to Azure CLI first."
    az login
fi

# Process each server from the configuration
echo "Processing MCP servers from $MCP_CONFIG_FILE..."
jq -c '.servers | to_entries[]' "$MCP_CONFIG_FILE" | while read -r server; do
    server_name=$(echo "$server" | jq -r '.key')
    server_config=$(echo "$server" | jq -r '.value')
    
    # Add name field to the server config
    server_config_with_name=$(echo "$server_config" | jq --arg name "$server_name" '. + {name: $name}')
    
    echo "Converting server: $server_name"
    
    # Set in Azure App Configuration
    echo "Adding to Azure App Configuration with key MCP:$server_name"
    az appconfig kv set \
      --name "$APPCONFIG_NAME" \
      --key "MCP:$server_name" \
      --value "$server_config_with_name" \
      --content-type "application/json" \
      --yes
    
    echo "Server $server_name configuration added successfully."
    echo "---------------------------------------------------"
done

echo "All MCP server configurations have been imported to Azure App Configuration."
```