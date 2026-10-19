using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mosaic.Contracts;
using Mosaic.Desktop.Services.Identity;

namespace Mosaic.Desktop.Services;

public sealed class ApiClient(HttpClient http, CurrentSession session)
{
    private const string BearerScheme = "Bearer";

    public Uri? BaseAddress => http.BaseAddress;

    public async Task<ApiResponse<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        return await SendAsync<T>(request, session.Current, cancellationToken);
    }

    public async Task<ApiResponse<TResult>> PostAsync<TBody, TResult>(
        string path, TBody body, CancellationToken cancellationToken)
    {
        // written with the options the API reads it with
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: MosaicJson.Options)
        };

        return await SendAsync<TResult>(request, session.Current, cancellationToken);
    }

    public async Task<ApiResponse> PostAsync<TBody>(string path, TBody body, CancellationToken cancellationToken)
    {
        // object reads whatever a newer server may send as data, and nothing here looks at it
        var response = await PostAsync<TBody, object>(path, body, cancellationToken);

        return new ApiResponse { Error = response.Error, TraceId = response.TraceId };
    }

    // sign-out names its session, because that session may already have ended here
    public async Task<ApiResponse> PostWithoutBodyAsync(string path, Session sendAs, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);

        var response = await SendAsync<object>(request, sendAs, cancellationToken);

        return new ApiResponse { Error = response.Error, TraceId = response.TraceId };
    }

    private async Task<ApiResponse<T>> SendAsync<T>(
        HttpRequestMessage request, Session? sent, CancellationToken cancellationToken)
    {
        if (http.BaseAddress is null)
        {
            return Failed<T>(DesktopErrorCodes.ApiAddressInvalid, "The client has no usable base address.");
        }

        // HttpClient sends an absolute address as it is, past BaseAddress, and the token would go with it
        if (request.RequestUri is { IsAbsoluteUri: true })
        {
            throw new ArgumentException("Only a path relative to the API's address may be sent.", nameof(request));
        }

        if (sent is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, sent.Token);
        }

        HttpStatusCode? status = null;

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);

            status = response.StatusCode;

            return await ReadAsync<T>(response, cancellationToken);
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
        finally
        {
            // only once the answer is read, because ending the session disposes the screens and that cancels this token
            if (sent is not null && status == HttpStatusCode.Unauthorized)
            {
                session.End(sent, SessionEndReason.Refused);
            }
        }
    }

    private static async Task<ApiResponse<T>> ReadAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
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

        // A failed status with no Error is not the API's envelope, and would otherwise read as success.
        if (!response.IsSuccessStatusCode && body.Error is null)
        {
            return Failed<T>(CodeFor(response.StatusCode), $"{(int)response.StatusCode} with no error in the body.");
        }

        return body;
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
