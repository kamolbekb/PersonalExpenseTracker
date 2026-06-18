using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Users;
using ExpenseTracker.Infrastructure.ExchangeRates;
using ExpenseTracker.Infrastructure.Identity;
using ExpenseTracker.Infrastructure.Persistence;
using ExpenseTracker.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set the ConnectionStrings__Default environment variable.");
        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(cs));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<ICurrentUser, UserProvisioningService>();
        services.AddSingleton<TelegramInitDataValidator>();
        services.AddSingleton<IClock, TashkentClock>();
        services.AddHttpClient<IRateSource, CbuRateProvider>(c => c.BaseAddress = new Uri("https://cbu.uz/"));
        return services;
    }
}
