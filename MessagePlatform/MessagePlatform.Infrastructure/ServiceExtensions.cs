using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MessagePlatform.Core.Interfaces;
using MessagePlatform.Infrastructure.Services;

namespace MessagePlatform.Infrastructure
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register services
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddScoped<IMessageService, MessageService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddSingleton<IUserConnectionService, UserConnectionService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IFileStorageService, AzureBlobStorageService>();

            // Configure Redis
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = "MessagePlatform";
            });

            return services;
        }
    }
}