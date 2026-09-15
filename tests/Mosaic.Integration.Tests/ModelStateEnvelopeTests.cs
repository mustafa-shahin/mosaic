using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Mosaic.Api;
using Mosaic.Contracts;

namespace Mosaic.Integration.Tests;

public sealed class ModelStateEnvelopeTests
{
    private static ApiResponse Build(Action<ModelStateDictionary> fill)
    {
        var modelState = new ModelStateDictionary();

        fill(modelState);

        var result = Assert.IsType<BadRequestObjectResult>(ModelStateEnvelope.Build(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), modelState)));

        return Assert.IsType<ApiResponse>(result.Value);
    }

    [Fact]
    public void AnAttributeThatNamedACode_KeepsIt()
    {
        var response = Build(state => state.AddModelError("Email", ErrorCodes.Required));

        Assert.False(response.Success);
        Assert.Equal(ErrorCodes.ValidationFailed, response.Error!.Code);
        Assert.Equal(ErrorCodes.Required, response.Error.FieldErrors!["email"][0].Code);
    }

    // the binder's own English never reaches a user, so it becomes one code
    [Fact]
    public void TheBindersOwnMessage_BecomesInvalidValue()
    {
        var response = Build(state =>
            state.AddModelError("$.port", "The JSON value could not be converted to System.Int32."));

        Assert.Equal(ErrorCodes.InvalidValue, response.Error!.FieldErrors!["port"][0].Code);
    }

    [Fact]
    public void TwoRulesOnOneField_BothArrive()
    {
        var response = Build(state =>
        {
            state.AddModelError("Email", ErrorCodes.Required);
            state.AddModelError("Email", ErrorCodes.InvalidValue);
        });

        Assert.Equal(2, response.Error!.FieldErrors!["email"].Count);
    }
}
