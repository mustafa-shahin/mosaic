using Microsoft.EntityFrameworkCore;
using Mosaic.Contracts.Auditing;
using Mosaic.Persistence;

namespace Mosaic.Api.Auditing;

public sealed class SearchAuditPeople(ManagementDbContext database)
{
    private const int MaxResults = 10;

    // ILIKE reads % and _ as wildcards, so a typed one gets this character in front and matches only itself
    private const string Escape = @"\";

    public async Task<IReadOnlyList<AuditPersonResult>> HandleAsync(string? search, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [];
        }

        var pattern = ContainsPattern(search.Trim());

        return await database.Users
            .Where(user => EF.Functions.ILike(user.FirstName + " " + user.LastName, pattern, Escape) ||
                           EF.Functions.ILike(user.Email, pattern, Escape))
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Take(MaxResults)
            .Select(user => new AuditPersonResult
            {
                Id = user.Id,
                Name = user.FirstName + " " + user.LastName,
                Email = user.Email,
            })
            .ToListAsync(cancellationToken);
    }

    private static string ContainsPattern(string text) =>
        "%" + text.Replace(Escape, Escape + Escape, StringComparison.Ordinal)
            .Replace("%", Escape + "%", StringComparison.Ordinal)
            .Replace("_", Escape + "_", StringComparison.Ordinal) + "%";
}
