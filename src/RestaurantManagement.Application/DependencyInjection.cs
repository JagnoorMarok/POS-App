using Microsoft.Extensions.DependencyInjection;

namespace RestaurantManagement.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application layer services into the dependency injection container.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application use cases / services here as features are added in future milestones.
        return services;
    }
}
