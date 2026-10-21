using FluentValidation;
using Labaik.Application.Common.Authentication;
using Labaik.Application.Common.Behaviors;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Labaik.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IAuthTokenFactory, AuthTokenFactory>();
        services.AddScoped<ICompanyServiceGate, CompanyServiceGate>();

        return services;
    }
}
