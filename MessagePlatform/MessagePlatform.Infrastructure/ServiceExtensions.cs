using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MessagePlatform.Core.Interfaces;
using MessagePlatform.Infrastructure.Services;
using MessagePlatform.Infrastructure.Data.SqlServer;
using MessagePlatform.Infrastructure.Data.CosmosDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Azure.Documents.Client;
using System;

namespace MessagePlatform.Infrastructure
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register services
            services.AddSingleton<ICacheService, CacheService>();
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

            // Configure SQL Server with Entity Framework Core
            services.AddDbContext<MessagePlatformDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("SqlServer") ?? "Server=(localdb)\\mssqllocaldb;Database=MessagePlatform;Trusted_Connection=True;"));

            // Register SQL repositories
            services.AddScoped(typeof(IRepository<>), typeof(SqlRepository<>));

            // Configure CosmosDB
            var cosmosDbEndpoint = configuration["CosmosDb:Endpoint"] ?? "https://localhost:8081";
            var cosmosDbKey = configuration["CosmosDb:AuthKey"] ?? "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
            var cosmosDbDatabase = configuration["CosmosDb:DatabaseId"] ?? "MessagePlatform";
            
            services.AddSingleton<DocumentClient>(s => 
                new DocumentClient(new Uri(cosmosDbEndpoint), cosmosDbKey));

            // Register CosmosDB repositories with factory pattern
            services.AddScoped<CosmosDbRepository<MessagePlatform.Core.Entities.Message>>(provider =>
            {
                var client = provider.GetRequiredService<DocumentClient>();
                return new CosmosDbRepository<MessagePlatform.Core.Entities.Message>(client, cosmosDbDatabase, "Messages");
            });

            services.AddScoped<CosmosDbRepository<MessagePlatform.Core.Entities.User>>(provider =>
            {
                var client = provider.GetRequiredService<DocumentClient>();
                return new CosmosDbRepository<MessagePlatform.Core.Entities.User>(client, cosmosDbDatabase, "Users");
            });

            services.AddScoped<CosmosDbRepository<MessagePlatform.Core.Entities.Group>>(provider =>
            {
                var client = provider.GetRequiredService<DocumentClient>();
                return new CosmosDbRepository<MessagePlatform.Core.Entities.Group>(client, cosmosDbDatabase, "Groups");
            });

            return services;
        }
    }
}