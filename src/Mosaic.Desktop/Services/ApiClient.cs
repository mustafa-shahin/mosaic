using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mosaic.Contracts;

namespace Mosaic.Desktop.Services;

public sealed class ApiClient(HttpClient http)
{
    public async Task<ApiResponse<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (http.BaseAddress is null)
        {
            return Failed<T>(DesktopErrorCodes.ApiAddressInvalid, "The client has no usable base address.");
        }
        try
        {
            var response = await http.GetAsync(path, cancellationToken);

            // ReadFromJsonAsync throws on a body that is not JSON, and a bare 401 has none.
            if (!IsJson(response))
            {
                return Failed<T>(
                    response.IsSuccessStatusCode ? DesktopErrorCodes.UnexpectedResponse : CodeFor(response.StatusCode),
                    $"{(int)response.StatusCode} with {MediaTypeOf(response)}.");
            }

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(
                MosaicJson.Options, cancellationToken);

            if (body is null)
            {
                return Failed<T>(DesktopErrorCodes.UnexpectedResponse, "The response body was empty.");
            }

            // A failed status with no Error is not the API talking, and it used to read as success.
            if (!response.IsSuccessStatusCode && body.Error is null)
            {
                return Failed<T>(CodeFor(response.StatusCode), $"{(int)response.StatusCode} with no error in the body.");
            }

            return body;
        }
        // A caller who cancelled deliberately gets an exception, not a result they might act on.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return Failed<T>(DesktopErrorCodes.ApiUnreachable, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return Failed<T>(DesktopErrorCodes.ApiTimedOut, $"No response within {http.Timeout}.");
        }
        catch (JsonException ex)
        {
            return Failed<T>(DesktopErrorCodes.UnexpectedResponse, ex.Message);
        }
    }

    private static bool IsJson(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;

        return mediaType is not null
            && (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }

    private static string MediaTypeOf(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType ?? "no content type";

    private static string CodeFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => DesktopErrorCodes.NotSignedIn,
        HttpStatusCode.Forbidden => DesktopErrorCodes.NotAllowed,
        _ => DesktopErrorCodes.UnexpectedResponse
    };

    private static ApiResponse<T> Failed<T>(string code, string message) =>
        ApiResponse.Fail<T>(new ApiError { Code = code, Message = message });
}
