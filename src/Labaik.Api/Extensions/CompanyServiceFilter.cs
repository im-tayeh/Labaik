using Labaik.Api.Extensions;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Companies;
using Labaik.Domain.Common.Results;

namespace Labaik.Api.Extensions;

public static class CompanyServiceFilter
{
    public static TBuilder RequireCompanyService<TBuilder>(this TBuilder builder,
        Func<CompanyServices, bool> service) where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
            var gate = context.HttpContext.RequestServices.GetRequiredService<ICompanyServiceGate>();
            if (user.Id is { } id && !await gate.IsEnabledAsync(id, service, context.HttpContext.RequestAborted))
                return Error.Forbidden("Company.ServiceDisabled", "This service is not enabled for your company.").ToProblem();
            return await next(context);
        });
}
