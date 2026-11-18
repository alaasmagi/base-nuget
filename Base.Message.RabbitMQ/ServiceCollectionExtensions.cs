using Base.Contracts.Message;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Base.Message.RabbitMQ;

/// <summary>
/// Provides dependency injection helpers for RabbitMQ message publishing and consuming.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared RabbitMQ connection manager and options.
    /// </summary>
    /// <param name="services">The service collection to register the connection with.</param>
    /// <param name="options">The RabbitMQ connection and exchange settings.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, RabbitMqOptions options)
    {
        services.AddSingleton(options);
        services.TryAddRabbitMqConnectionManager();
        return services;
    }

    /// <summary>
    /// Registers RabbitMQ publishing services for the configured options.
    /// </summary>
    /// <param name="services">The service collection to register the publisher with.</param>
    /// <param name="options">The RabbitMQ connection and exchange settings.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services, RabbitMqOptions options)
    {
        services.AddRabbitMq(options);
        services.AddSingleton<Base.Contracts.Message.IBaseEventPublisher, RabbitMqEventPublisher>();
        return services;
    }

    /// <summary>
    /// Registers a hosted RabbitMQ consumer. Requires <see cref="AddRabbitMq"/> or
    /// <see cref="AddRabbitMqPublisher"/> to have registered the connection and options. A
    /// <see cref="RabbitMqConsumerBase{TContent}"/> also needs its <see cref="IBaseEventHandler{TContent}"/>
    /// registered separately.
    /// </summary>
    /// <typeparam name="TConsumer">The concrete <see cref="RabbitMqConsumerBase{TContent}"/> or
    /// <see cref="RabbitMqDeliveryConsumerBase"/> implementation to host.</typeparam>
    /// <param name="services">The service collection to register the consumer with.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    public static IServiceCollection AddRabbitMqConsumer<TConsumer>(this IServiceCollection services)
        where TConsumer : class, IHostedService
    {
        services.TryAddRabbitMqConnectionManager();
        services.AddHostedService<TConsumer>();
        return services;
    }

    /// <summary>
    /// Registers a hosted <see cref="RabbitMqRoutingConsumer"/> that reads every message from one pre-existing queue and
    /// dispatches it by routing key. Requires <see cref="AddRabbitMq"/> or <see cref="AddRabbitMqPublisher"/> to have
    /// registered the connection and options, and an <see cref="IBaseEventHandler{TContent}"/> for every routed type.
    /// </summary>
    /// <param name="services">The service collection to register the consumer with.</param>
    /// <param name="queueName">The pre-existing queue to consume from.</param>
    /// <param name="configureRoutes">Adds the routes, e.g. <c>routes =&gt; routes.Route&lt;UserEventContent&gt;("identity-hub")</c>.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    public static IServiceCollection AddRabbitMqRoutingConsumer(
        this IServiceCollection services,
        string queueName,
        Action<RabbitMqEventRouter> configureRoutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(configureRoutes);

        var router = new RabbitMqEventRouter();
        configureRoutes(router);

        services.TryAddRabbitMqConnectionManager();
        // Not AddHostedService: it de-duplicates by implementation type, which would drop a second queue's consumer.
        services.AddSingleton<IHostedService>(provider => new RabbitMqRoutingConsumer(
            provider.GetRequiredService<RabbitMqConnectionManager>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            router,
            provider.GetRequiredService<ILogger<RabbitMqRoutingConsumer>>(),
            queueName));
        return services;
    }

    private static void TryAddRabbitMqConnectionManager(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(RabbitMqConnectionManager)))
        {
            services.AddSingleton<RabbitMqConnectionManager>();
        }
    }
}
