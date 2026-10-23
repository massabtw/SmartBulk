using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Services;

namespace SmartBulk.Application;

public static class IoC
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<IHealthService, HealthService>();
        services.AddTransient<IUserSettingsService, UserSettingsService>();
        services.AddTransient<IFoodService, FoodService>();
        services.AddTransient<IWeightLogService, WeightLogService>();
        services.AddTransient<ICreatineLogService, CreatineLogService>();
        services.AddTransient<ICafeteriaMenuService, CafeteriaMenuService>();
        services.AddTransient<IMealLogService, MealLogService>();

        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        services.AddValidatorsFromAssembly(typeof(IoC).Assembly);

        return services;
    }
}
