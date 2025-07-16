using Ecom.Core.Entities;
using Ecom.Core.interfaces;
using Ecom.Core.Services;
using Ecom.Core.Sharing;
using Ecom.infrastructure.Data;
using Ecom.infrastructure.Repositries;
using Ecom.infrastructure.Repositries.Service;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Net;
using System.Net.Security;
using System.Text;
namespace Ecom.infrastructure;

public static class infrastructureRegisteration
{
    public static IServiceCollection infrastructureConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure logging
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        //apply DbContext
        services.AddDbContext<AppDbContext>(op =>
        {
            op.UseSqlServer(configuration.GetConnectionString("DefaultConnction"));
        });
        services.AddScoped(typeof(IGenericRepositry<>), typeof(GenericRepositry<>));
        
        //apply Unit OF Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        //register email services
        //services.AddScoped<IEmailService, EmailService>();
        //services.AddScoped<EmailStringBody>();

        //register IOrder Service
        services.AddScoped<IOrderService, OrderService>();

        //register token
        services.AddScoped<IGenerateToken, GenerateToken>();

        //register payment service
        services.AddScoped<IPaymentService, PaymentService>();

        //apply Redis Connectoon
        services.AddSingleton<IConnectionMultiplexer>(i =>
        {
            var config = ConfigurationOptions.Parse(configuration.GetConnectionString("redis"));
            return ConnectionMultiplexer.Connect(config);
        });
       
        services.AddSingleton<IImageManagementService, ImageManagementService>();
        services.AddSingleton<IFileProvider>(
            new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")));

        //// Configure Identity
        //services.AddIdentity<AppUser, IdentityRole>(options =>
        //{
        //    // Password settings
        //    options.Password.RequireDigit = true;
        //    options.Password.RequireLowercase = true;
        //    options.Password.RequireNonAlphanumeric = true;
        //    options.Password.RequireUppercase = true;
        //    options.Password.RequiredLength = 6;

        //    // User settings
        //    options.User.RequireUniqueEmail = true;

        //    // SignIn settings
        //    options.SignIn.RequireConfirmedEmail = true;

        //    // Lockout settings
        //    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        //    options.Lockout.MaxFailedAccessAttempts = 5;
        //    options.Lockout.AllowedForNewUsers = true;
        //})
        //.AddEntityFrameworkStores<AppDbContext>()
        //.AddDefaultTokenProviders();
        services.AddIdentity<AppUser, IdentityRole>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 6;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;
            //options.SignIn.RequireConfirmedEmail = true;
        })
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();
        // Configure CORS
        //services.AddCors(options =>
        //{
        //    options.AddPolicy("AllowSpecificOrigin",
        //        builder =>
        //        {
        //            builder.WithOrigins("http://localhost:4200")
        //                   .AllowAnyMethod()
        //                   .AllowAnyHeader()
        //                   .AllowCredentials();
        //        });
        //});
      
        // Configure Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Token:Secret"])),
                ValidateIssuer = true,
                ValidIssuer = configuration["Token:Issuer"],
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Cookies["token"];
                    if (!string.IsNullOrEmpty(token))
                    {
                        context.Token = token;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }
}
