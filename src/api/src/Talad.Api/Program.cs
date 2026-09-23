using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Talad.Api.Auth;
using Talad.Api.Members;
using Talad.Api.Promotions;
using Talad.Api.Sales;
using Talad.Api.Settings;
using Talad.Infrastructure;
using Talad.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        var o = jwt.Value;
        if (System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes — set it through configuration (e.g. Jwt__SigningKey)");
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = o.Issuer,
            ValidateAudience = true,
            ValidAudience = o.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = o.Key(),
            NameClaimType = "unique_name",
            RoleClaimType = "role",
        };
    });

// BR-talad-005@v1 · NFR-talad-005 — every endpoint needs a valid JWT unless it opts out explicitly,
// and only sign-in opts out.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapCatalogAndCartEndpoints();
app.MapMemberEndpoints();
app.MapMemberDiscountEndpoints();
app.MapPromotionEndpoints();

app.Run();

public partial class Program;
