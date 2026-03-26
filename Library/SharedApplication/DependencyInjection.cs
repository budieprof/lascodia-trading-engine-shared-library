
using Autofac;
using Lascodia.Trading.Engine.EventBus;
using Lascodia.Trading.Engine.EventBus.Abstractions;
using Lascodia.Trading.Engine.EventBusRabbitMQ;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Confluent.Kafka;
using Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;
using Lascodia.Trading.Engine.SharedApplication.Common.Services;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using AutoMapper;
using Lascodia.Trading.Engine.IntegrationEventLogEF.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Lascodia.Trading.Engine.SharedApplication.Common.Behaviours;
using Lascodia.Trading.Engine.SharedLibrary.Mappings;
using Microsoft.AspNetCore.Http;
using Lascodia.Trading.Engine.SharedApplication.Common.Models;
using Microsoft.AspNetCore.Builder;
using Lascodia.Trading.Engine.EventBus.Events;
using Lascodia.Trading.Engine.SharedDomain.Common;
using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;
using Lascodia.Trading.Engine.SharedLibrary;
using Lascodia.Trading.Engine.SharedApplication.Common.ExceptionHandlers;

namespace Lascodia.Trading.Engine.SharedApplication;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddSharedApplicationDependency(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICurrentUserService, CurrentUserService>();


        string? broker_type = configuration["BrokerType"];
        if(broker_type == "rabbitmq")
        {
            services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();
                var factory = new ConnectionFactory()
                {
                    HostName = configuration.GetSection("RabbitMQConfig")["Host"],//host.docker.internal
                    DispatchConsumersAsync = true,
                    UserName = configuration.GetSection("RabbitMQConfig")["Username"],//guess
                    Password = configuration.GetSection("RabbitMQConfig")["Password"],//guest
                    Port = Protocols.DefaultProtocol.DefaultPort
                };
                var retryCount = 5;
                return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
            });
            services.AddSingleton<IEventBus, Lascodia.Trading.Engine.EventBusRabbitMQ.EventBusRabbitMQ>(sp =>
            {
                string? subscriptionClientName = configuration.GetSection("RabbitMQConfig")["QueueName"];
                var rabbitMQPersistentConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                var logger = sp.GetRequiredService<ILogger<Lascodia.Trading.Engine.EventBusRabbitMQ.EventBusRabbitMQ>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                var retryCount = 5;
                return new Lascodia.Trading.Engine.EventBusRabbitMQ.EventBusRabbitMQ(rabbitMQPersistentConnection, logger, sp, eventBusSubcriptionsManager, subscriptionClientName, retryCount);
            });
        }
        else if(broker_type== "kafka")
        {
            services.AddSingleton<IEventBus, Lascodia.Trading.Engine.EventBusKafka.EventBusKafka>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Lascodia.Trading.Engine.EventBusKafka.EventBusKafka>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                var retryCount = 5;
                var productConfig = new ProducerConfig()
                {
                    BootstrapServers = configuration.GetSection("KafkaConfig")["BootstrapServers"],
                    ClientId = Dns.GetHostName()
                };
                var consumerConfig = new ConsumerConfig()
                {
                    GroupId = configuration.GetSection("KafkaConfig")["ClientGroupId"],
                    BootstrapServers = configuration.GetSection("KafkaConfig")["BootstrapServers"],
                    AutoOffsetReset = AutoOffsetReset.Earliest
                };
                return new Lascodia.Trading.Engine.EventBusKafka.EventBusKafka(logger, sp, eventBusSubcriptionsManager, productConfig, consumerConfig, retryCount);
            });
        }

        
        services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();
        

        return services;
    }

    public static void AutoConfigureEventHandler(this IEventBus eventBus, Assembly assembly)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
            .ToList();
        foreach (var handlerType in types)
        {
            eventBus.Subscribe(handlerType);
        }
    }

    public static void AutoUnconfigureEventHandler(this IEventBus eventBus, Assembly assembly)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
            .ToList();
        foreach (var handlerType in types)
        {
            eventBus.Unsubscribe(handlerType);
        }
    }


    public static void AutoRegisterEventHandler(this IServiceCollection services, Assembly assembly)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
            .ToList();
        foreach (var handlerType in types)
        {
            services.AddTransient(handlerType);
        }
    }

    public static void AutoRegisterBackgroundJobs(this IServiceCollection services, Assembly assembly)
    {
        var test = assembly.GetExportedTypes().ToList();

        var types = assembly.GetExportedTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IHostedService))).ToList();
        foreach (var handlerType in types)
        {
            services.AddSingleton(typeof(IHostedService), handlerType);
        }
    }
    public static void AutoRegisterConfigurationOptions(this IServiceCollection services, IConfiguration configuration, params Assembly[] assemblies)
    {
        List<Type> types = assemblies.SelectMany(a => a.GetExportedTypes()).Where(c => c.IsClass && !c.IsAbstract && c.IsPublic && c.BaseType.IsGenericType && c.BaseType.GetGenericTypeDefinition() == typeof(ConfigurationOption<>)).ToList();
        foreach (Type type in types)
        {
            var configObject = Activator.CreateInstance(type);
            configuration.Bind(type.Name, configObject);
            services.AddSingleton(type, configObject);
        }
    }
    public static void ConfigureAppServices(this IServiceCollection services, Assembly assembly)
    {
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddMediatR(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        services.AddScoped(provider =>
        {
            var expression = new MapperConfigurationExpression();
            expression.AddProfile(new MappingProfile(provider.GetRequiredService<IHttpContextAccessor>(), assembly));
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new MapperConfiguration(expression, loggerFactory).CreateMapper();
        });

        services.AddTransient<IIntegrationEventService>(s =>
        {
            var logger = s.GetRequiredService<ILogger<IntegrationEventService>>();
            var eventBus = s.GetRequiredService<IEventBus>();
            var eventService = s.GetRequiredService<IIntegrationEventLogService>();
            return new IntegrationEventService(logger, eventBus, eventService, assembly.GetName().ToString());
        });
        services.AutoRegisterEventHandler(assembly);
        services.AutoRegisterBackgroundJobs(assembly);
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new TimeSpanJsonConverter());
            });

        services.AddEndpointsApiExplorer();
        
        services.AddAuthorization(options =>
        {
            options.AddPolicy("apiScope", policy =>
            {
                policy.RequireAuthenticatedUser();
                //policy.RequireClaim("scope", "qtc_bulk_transaction_api");
            });
        });
        services.AddCors();
        services.AddResponseCaching();
        services.AddHttpContextAccessor();
        services.AddHttpClient();
        services.AddHttpClient("ProxyClient")
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                string? proxyServer = configuration.GetValue<string>("ProxyConfig:ProxyServer");

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                if (!string.IsNullOrWhiteSpace(proxyServer))
                {
                    handler.Proxy = new WebProxy(proxyServer, true)
                    {
                        UseDefaultCredentials = true
                    };
                    handler.UseProxy = true;
                }

                return handler;
            });
        services.AddHealthChecks();
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1024; // Maximum number of cache entries
            options.CompactionPercentage = 0.25; // Remove 25% of entries when limit is reached
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5); // Scan for expired items every 5 minutes
        });
    }

    public static void RunAppPipeline<T, Db, IDb>(this WebApplication app, Func<IServiceScope, bool> calback) where IDb : IDbContext where Db : DbContext
    {
        app.UseCors(options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        app.UseResponseCaching();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health");
        app.UseMiddleware<ValidationExceptionHandlerMiddleware>();
        app.MapControllers().RequireAuthorization("apiScope");

        using (var services = app.Services.CreateScope())
        {
            services.DbMigrate<T, Db, IDb>();
            calback(services);
        }

        app.Run();
    }

    public static void DbMigrate<T,Db, IDb>(this IServiceScope service) where Db : DbContext
    {
        var logger = service.ServiceProvider.GetRequiredService<ILogger<T>>();
        try
        {
            var db = service.ServiceProvider.GetRequiredService<IDb>() as Db;
            db?.Database.Migrate();
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
        }
    }


    public static DbContextOptionsBuilder SetDB<T>(this DbContextOptionsBuilder options, IConfiguration configuration, string connectionString = "WriteDbConnection")
    {
        return options.UseSqlServer(
                configuration.GetConnectionString(connectionString) ?? "", s => s.CommandTimeout((int)TimeSpan.FromMinutes(10).TotalSeconds).MigrationsAssembly(typeof(T).Assembly.FullName).EnableRetryOnFailure());
    }

    public static DbContextOptionsBuilder SetPostgresDB<T>(this DbContextOptionsBuilder options, IConfiguration configuration, string connectionString = "WriteDbConnection")
    {
        return options.UseNpgsql(
                configuration.GetConnectionString(connectionString) ?? "", s => s.CommandTimeout((int)TimeSpan.FromMinutes(10).TotalSeconds).MigrationsAssembly(typeof(T).Assembly.FullName).EnableRetryOnFailure());
    }


    private static IEnumerable<IntegrationEvent> PreprocessAuditables(IDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        var modifiedEntries = context.GetDbContext().ChangeTracker.Entries()
            .Where(x => x.Entity is IAuditable
                && (x.State == EntityState.Added || x.State == EntityState.Modified));
        var token = httpContextAccessor?.HttpContext?.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").LastOrDefault();



        var username = string.Empty;

        if (!token.IsEmptyOrNull())
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            JwtSecurityToken? jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

            username = jwtToken?.Claims.First(x => x.Type == "userName").Value;
        }
        DateTime now = DateTime.UtcNow;
        foreach (var entry in modifiedEntries)
        {
            //var auditableEntity = entry.Entity as Entity<string>;

            var modifiedProperties = entry.CurrentValues.Properties
                        .Where(p => entry.CurrentValues[p.Name] != entry.OriginalValues[p.Name])
                        .Select(p => p.Name).ToList();

            var newData = new NameValueCollection();
            var oldData = new NameValueCollection();

            modifiedProperties.ForEach(s =>
            {
                newData[s] = entry.CurrentValues[s]?.ToString();
                oldData[s] = entry.OriginalValues[s]?.ToString();
            });

            var evt = new DataChangeLogIntegrationEvent(entry.Entity.GetPropValue<string>("Id") ?? "", username ?? "", entry.Entity.GetType().ToString(), entry.State.ToString(), newData.GetJson(), oldData.GetJson(), now);

            yield return evt;
        }
    }

    public static async Task<bool> SendDataToRebirthKafkaTopic(string topic, string bootstrapServer, string data, string username, string password)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServer,
            SaslMechanism = SaslMechanism.ScramSha512,
            SecurityProtocol = SecurityProtocol.SaslPlaintext,
            SaslUsername = username,
            SaslPassword = password,
        };

        // Create a producer
        using (var producer = new ProducerBuilder<Null, string>(config).Build())
        {
            try
            {
                // Send a message to the topic
                var deliveryResult = await producer.ProduceAsync(topic, new Message<Null, string> { Value = data });

                // Flush to ensure the message is sent before disposal
                producer.Flush(TimeSpan.FromSeconds(10));

                Console.WriteLine($"Delivered '{deliveryResult.Value}' to '{deliveryResult.TopicPartitionOffset}'");
                return true;
            }
            catch (ProduceException<Null, string> e)
            {
                Console.WriteLine($"Delivery failed: {e.Error.Reason}");
                return false;
            }
        }
    }

}

[ExcludeFromCodeCoverage]
public class TimeSpanJsonConverter : System.Text.Json.Serialization.JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (TimeSpan.TryParseExact(s, @"hh\:mm\:ss", null, out var ts))
            return ts;
        if (TimeSpan.TryParse(s, out ts))
            return ts;
        throw new System.Text.Json.JsonException($"Cannot convert \"{s}\" to TimeSpan.");
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, TimeSpan value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(@"hh\:mm\:ss"));
    }
}
