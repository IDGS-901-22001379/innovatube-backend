using System.Text;
using InnovaTube.Api.Infrastructure;
using InnovaTube.Api.Security;
using InnovaTube.Api.Services.Interfaces;
using InnovaTube.Api.Services.Implementations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ==========================
// Servicios
// ==========================

// Controllers
builder.Services.AddControllers();

// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "InnovaTube API",
        Version = "v1",
        Description = "API de autenticación y favoritos de InnovaTube"
    });

    // ======== Seguridad para JWT en Swagger (Authorize) =========
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Introduce: Bearer {tu token JWT}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// MySQL factory
builder.Services.AddSingleton<MySqlConnectionFactory>();

// Opciones JWT desde appsettings.json
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

// ==========================
// Autenticación JWT
// ==========================

var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

// Autorización (para usar [Authorize])
builder.Services.AddAuthorization();

// Servicios propios
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IVideoService, VideoService>();
// builder.Services.AddScoped<IFavoritesService, FavoritesService>();

var app = builder.Build();

// ==========================
// Middleware / pipeline
// ==========================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "InnovaTube API v1");
        // c.RoutePrefix = string.Empty; // si quieres que Swagger salga en "/"
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
