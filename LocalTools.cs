using OpenAI.Chat;

namespace MCPConfig
{
    public static class LocalTools
    {
        public static readonly ChatTool getCurrentLocationTool = ChatTool.CreateFunctionTool(
            functionName: nameof(GetCurrentLocation),
            functionDescription: "Get the user's current location"
        );

        public static readonly ChatTool getCurrentWeatherTool = ChatTool.CreateFunctionTool(
            functionName: nameof(GetCurrentWeather),
            functionDescription: "Get the current weather in a given location",
            functionParameters: BinaryData.FromBytes("""
                {
                    "type": "object",
                    "properties": {
                        "location": {
                            "type": "string",
                            "description": "The city and state, e.g. Boston, MA"
                        },
                        "unit": {
                            "type": "string",
                            "enum": [ "celsius", "fahrenheit" ],
                            "description": "The temperature unit to use. Infer this from the specified location."
                        }
                    },
                    "required": [ "location" ]
                }
                """u8.ToArray())
        );

        public static string GetCurrentLocation()
        {
            // Simulate getting the user's current location.
            return "Seattle, WA";
        }

        public static string GetCurrentWeather(string location, string unit = "fahrenheit")
        {
            // Simulate getting the current weather for the specified location.
            return $"The current weather in {location} is 75 degrees {unit}.";
        }
    }
    
}