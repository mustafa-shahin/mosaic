using System.Net.Http.Json;
using System.Text.Json;
using Mosaic.Contracts;

namespace Mosaic.Desktop.Services;

public sealed class ApiClient(HttpClient http)
{
    public async Task<ApiResponse<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            var response = await http.GetAsync(path, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(
                MosaicJson.Options, cancellationToken);

            return body ?? Failed<T>(DesktopErrorCodes.UnexpectedResponse, "The response body was empty.");
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

    private static ApiResponse<T> Failed<T>(string code, string message) =>
        ApiResponse.Fail<T>(new ApiError { Code = code, Message = message });
}
