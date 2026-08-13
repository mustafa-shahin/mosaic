using System.Text.Json;

namespace Mosaic.Contracts.Tests;

public class ApiResponseTests
{
    [Fact]
    public void Ok_WithNoPayload_ReportsSuccess()
    {
        var response = ApiResponse.Ok();

        Assert.True(response.Success);
        Assert.Null(response.Error);
    }

    [Fact]
    public void Fail_WithError_DoesNotReportSuccess()
    {
        var error = new ApiError
        {
            Code = "auth.invalid_credentials",
            Message = "Email or password is incorrect."
        };

        var response = ApiResponse.Fail(error);

        Assert.False(response.Success);
        Assert.Equal(error, response.Error);
    }

    [Fact]
    public void Ok_WithPayload_KeepsThePayload()
    {
        var payload = new { Name = "Test", Value = 42 };

        var response = ApiResponse.Ok(payload);

        Assert.True(response.Success);
        Assert.Equal(payload, response.Data);
    }

    [Fact]
    public void Fail_RoundTrippedThroughJson_KeepsCodeParamsAndFieldErrors()
    {
        var error = new ApiError
        {
            Code = "validation.failed",
            Message = "One or more fields are invalid.",
            Params = new Dictionary<string, string> { ["max"] = "40" },
            FieldErrors = new Dictionary<string, IReadOnlyList<string>>
            {
                ["email"] = ["validation.required"]
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
