using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Mosaic.Api.Validation;
using Mosaic.Domain.Errors;

namespace Mosaic.Api.Tests;

// A refusal kind with no line in StatusFor would answer 500 and look like a server bug.
public sealed class ExceptionStatusTests
{
    [Fact]
    public void EveryRefusalKindMapsToAStatusOtherThan500()
    {
        var assemblies = new[] { typeof(MosaicException).Assembly, typeof(ValidationException).Assembly };

        var kinds = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && type.IsAssignableTo(typeof(MosaicException)))
            .ToList();

        Assert.NotEmpty(kinds);

        var unmapped = kinds
            // no constructor runs: the handler switches on the runtime type and nothing else
            .Where(kind => MosaicExceptionHandler.StatusFor((Exception)RuntimeHelpers.GetUninitializedObject(kind))
                == StatusCodes.Status500InternalServerError)
            .Select(kind => kind.Name)
            .Order()
            .ToList();

        Assert.True(unmapped.Count == 0, $"No status for: {string.Join(", ", unmapped)}");
    }
}
