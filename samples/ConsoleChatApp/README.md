# Console Chat Application with MCP Configuration

## Introduction

This sample Console Chat Application demonstrates how to seamlessly transition your AI development workflow from local to cloud environments using Azure App Configuration. It shows how to:

- Use the same Model Context Protocol (MCP) configuration from VSCode to cloud environment
- Dynamically configure MCP clients to connect to different MCP servers
- Leverage Azure App Configuration for centralized configuration management

## Prerequisites

- **.NET 9.0 SDK** - [Download and install](https://dotnet.microsoft.com/download/dotnet/9.0)

- **VSCode mcp.json** - [VSCode MCP configuration format](https://code.visualstudio.com/docs/copilot/chat/mcp-servers#_configuration-format)

- **Azure CLI** - [Installation guide](https://docs.microsoft.com/cli/azure/install-azure-cli)

- **Azure subscription** - [Create a free account](https://azure.microsoft.com/free/)

- **Azure App Configuration instance**
  - [Create Azure App Configuration store](https://learn.microsoft.com/en-us/azure/azure-app-configuration/quickstart-azure-app-configuration-create?tabs=azure-portal)
  - [Enable Microsoft Entra ID authentication](https://learn.microsoft.com/en-us/azure/azure-app-configuration/concept-enable-rbac)

- **Azure OpenAI service instance**
  - [Create Azure OpenAI service](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource)
  - [Deploy models to Azure OpenAI](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource)
  - [Enable Microsoft Entra ID authentication](https://learn.microsoft.com/en-us/azure/ai-services/authentication?tabs=powershell#authenticate-with-azure-ad)

## Setup Instructions

### 1. Configure Azure App Configuration Access

First, retrieve your Azure App Configuration store endpoint:

```bash
# Login to Azure CLI (if not already logged in)
az login

# Get the endpoint of your Azure App Configuration store
az appconfig show --name your-appconfig-name --resource-group your-resource-group --query endpoint --output tsv
```

Set the environment variable to configure your application to use this endpoint:

**Windows (Command Prompt)**
```cmd
setx AZURE_APPCONFIG_ENDPOINT "https://your-appconfig-instance.azconfig.io"
```

**Windows (PowerShell)**
```powershell
$env:AZURE_APPCONFIG_ENDPOINT = "https://your-appconfig-instance.azconfig.io"
[System.Environment]::SetEnvironmentVariable('AZURE_APPCONFIG_ENDPOINT', 'https://your-appconfig-instance.azconfig.io', 'User')
```

**macOS/Linux**
```bash
export AZURE_APPCONFIG_ENDPOINT="https://your-appconfig-instance.azconfig.io"
# Add to .bashrc or .zshrc for persistence
echo 'export AZURE_APPCONFIG_ENDPOINT="https://your-appconfig-instance.azconfig.io"' >> ~/.bashrc
```

### 2. Configure Azure OpenAI Settings

Store your Azure OpenAI configuration in Azure App Configuration:

```bash
# Add Azure OpenAI configuration
az appconfig kv set --name your-appconfig-name --key "AzureOpenAI:Endpoint" --value "https://your-openai-instance.openai.azure.com/" --yes

az appconfig kv set --name your-appconfig-name --key "AzureOpenAI:Deployment" --value "your-model-deployment-name" --yes
```

### 3. Import VS Code MCP Configuration

Use this script to automatically import your VS Code MCP configuration to Azure App Configuration:

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

### 4. Running the Application

Build and run the application:

```bash
# Navigate to the ConsoleChatApp directory
cd samples\ConsoleChatApp

# Build the application
dotnet build

# Run the application
dotnet run
```

## How It Works

1. The application uses Azure App Configuration client to retrieve MCP server configurations
2. It loads these configurations at startup and can refresh them periodically
3. The application connects to the specified MCP server using the retrieved configuration
4. Authentication with Azure services uses Microsoft Entra ID, leveraging your Azure CLI login credentials

## Troubleshooting

### Common Issues

**Authentication Errors:**
- Ensure you're logged in with Azure CLI (`az login`)
- Verify your account has appropriate permissions to access Azure App Configuration and Azure Open AI
- Check if Microsoft Entra ID authentication is properly enabled for the services and sufficient roles are granted

**Configuration Not Found:**
- Verify the environment variable is correctly set
- Confirm the keys are correctly formatted in Azure App Configuration
- Check for typos in configuration keys or values

**Application Startup Issues:**
- Check application logs for detailed error messages
- Verify all dependencies are installed and up-to-date
- Ensure the Azure App Configuration endpoint is accessible from your network

## Next Steps

- Explore the source code to understand the implementation details
- Try modifying the MCP configurations in Azure App Configuration to see dynamic updates
- Experiment with different MCP servers and model configurations
- Integrate this approach into your own AI applications

## Additional Resources

- [Model Context Protocol Documentation](https://github.com/microsoft/mcp)
- [Azure App Configuration Documentation](https://learn.microsoft.com/en-us/azure/azure-app-configuration/)
- [Azure OpenAI Service Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Microsoft Entra ID Authentication](https://learn.microsoft.com/en-us/azure/active-directory/authentication/)