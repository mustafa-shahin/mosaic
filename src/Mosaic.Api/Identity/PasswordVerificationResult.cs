namespace Mosaic.Api.Identity;

// SuccessRehashNeeded: right password, but the row was hashed at a lower cost than today's,
// so sign-in should store a new hash
public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}
