namespace Mosaic.Desktop.Services;

// One place that knows where the API is. A settings screen replaces the default in a later slice.
public static class ApiEndpoint
{
    private const string OverrideVariable = "MOSAIC_API_URL";
    private const string DevelopmentDefault = "http://localhost:5175";

    public static Uri BaseAddress { get; } =
        new(Environment.GetEnvironmentVariable(OverrideVariable) ?? DevelopmentDefault);
}
