using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talad.Application.Auth;
using Talad.Application.Navigation;
using Talad.Infrastructure.Auth;
using Talad.Infrastructure.Persistence;

namespace Talad.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<TaladDbContext>(o => o.UseNpgsql(config.GetConnectionString("Talad")));
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddSingleton<IdentityPasswordHasher>();
        services.AddSingleton<IPasswordVerifier>(sp => sp.GetRequiredService<IdentityPasswordHasher>());
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<LoginService>();
        services.AddScoped<CurrentUserService>();
        return services;
    }
}
