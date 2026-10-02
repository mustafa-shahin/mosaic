using Mosaic.Contracts.Identity;
using Mosaic.Domain.Identity;

namespace Mosaic.Application.Identity;

// SignInUser and GetSession answer with the same shape, so both build it here
internal static class UserMapper
{
    public static SessionResult ToSessionResult(User user) => new()
    {
        UserId = user.Id,
        Email = user.Email,
        FullName = user.FullName,
    };
}
