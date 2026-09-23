using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talad.Application.Auth;
using Talad.Application.Catalog;
using Talad.Application.Members;
using Talad.Application.Navigation;
using Talad.Application.Sales;
using Talad.Application.Settings;
using Talad.Infrastructure.Auth;
using Talad.Infrastructure.Persistence;
using Talad.Infrastructure.Storage;

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
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ProductCatalog>();
        services.AddScoped<CartService>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<MemberRegistration>();
        services.AddScoped<MemberDirectory>();
        services.AddScoped<MemberProfile>();
        services.AddScoped<IMemberDiscountRepository, MemberDiscountRepository>();
        services.AddScoped<MemberDiscountSettings>();
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
        services.AddSingleton<ProductImageFiles>();
        return services;
    }
}
