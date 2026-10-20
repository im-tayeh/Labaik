using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Labaik.Api.Handlers;

internal sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not FluentValidation.ValidationException validationException)
        {
            return false; // Not ours: let the next handler try.
        }

        // Keys are JSON paths in the same camelCase as the request body: "fullName", "arrival.entryPoint",
        // "stays[0].placeName". Done here once for every validator.
        var errors = validationException.Errors
            .GroupBy(failure => ToJsonPath(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "One or more validation errors occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21"
        };
        problemDetails.Extensions["errors"] = errors;
        problemDetails.Extensions["code"] = "Validation.Failed";

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    // "Stays[0].PlaceName" -> "stays[0].placeName" (each segment camelCased, indexers kept).
    internal static string ToJsonPath(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(segment =>
        {
            var bracket = segment.IndexOf('[');
            return bracket < 0
                ? JsonNamingPolicy.CamelCase.ConvertName(segment)
                : JsonNamingPolicy.CamelCase.ConvertName(segment[..bracket]) + segment[bracket..];
        }));
}
