using Atm.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAtmInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // SQLite today. Swap UseSqlite for UseMySql / UseNpgsql later with the same entities.
        services.AddDbContext<AtmDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IAtmRepository, EfAtmRepository>();
        services.AddScoped<AtmService>();
        return services;
    }
}
