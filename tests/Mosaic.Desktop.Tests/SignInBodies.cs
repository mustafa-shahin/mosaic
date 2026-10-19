namespace Mosaic.Desktop.Tests;

// What POST /v1/auth/sign-in answers, in the API's field names but without the success flag and trace id.
internal static class SignInBodies
{
    public const string Accepted =
        """{"data":{"token":"mosaic_Zm9yLXRlc3RzLW9ubHktbm90LWEtcmVhbC10b2tlbi0xMjM0","session":{"userId":"0199f2a6-3c41-7d2e-9b1a-5f0c8e4d2a17","email":"ada@example.com","fullName":"Ada Lovelace"}}}""";

    public const string WrongPassword = """{"error":{"code":"error.invalid_credentials"}}""";
}
