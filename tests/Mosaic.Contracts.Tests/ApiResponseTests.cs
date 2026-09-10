using System.Text.Json;

namespace Mosaic.Contracts.Tests;

public sealed class ApiResponseTests
{
    [Fact]
    public void Fail_RoundTrippedThroughJson_KeepsCodeParamsAndFieldErrors()
    {
        var error = new ApiError
        {
            Code = "validation.failed",
            Message = "One or more fields are invalid.",
            Params = new Dictionary<string, string> { ["max"] = "40" },
            FieldErrors = new Dictionary<string, IReadOnlyList<FieldError>>
            {
                ["email"] =
                [
                    new FieldError
                    {
                        Code = "error.too_long",
                        Params = new Dictionary<string, string> { ["max"] = "254" }
                    }
                ]
            }
        };
        var original = ApiResponse.Fail(error);

        var json = JsonSerializer.Serialize(original, MosaicJson.Options);
        var restored = JsonSerializer.Deserialize<ApiResponse>(json, MosaicJson.Options);

        Assert.Equivalent(original, restored, strict: true);
    }

    [Fact]
    public void Ok_WithNoPayload_SerialisesToSuccessOnly()
    {
        var json = JsonSerializer.Serialize(ApiResponse.Ok(), MosaicJson.Options);

        Assert.Equal("""{"success":true}""", json);
    }

    [Fact]
    public void Fail_Serialised_UsesCamelCaseAndOmitsNullMembers()
    {
        var error = new ApiError { Code = "auth.invalid_credentials" };

        var json = JsonSerializer.Serialize(ApiResponse.Fail(error), MosaicJson.Options);

        Assert.Equal("""{"error":{"code":"auth.invalid_credentials"},"success":false}""", json);
    }
}
