using Microsoft.Extensions.DependencyInjection;
using OzgecmisAnalizi.Core.Abstractions;

namespace OzgecmisAnalizi.Core;

public static class CoreServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        return services;
    }
}
