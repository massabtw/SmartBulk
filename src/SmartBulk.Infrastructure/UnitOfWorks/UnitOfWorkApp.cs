using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.UnitOfWorks;
using SmartBulk.Infrastructure.Repositories;

namespace SmartBulk.Infrastructure.UnitOfWorks;

public class UnitOfWorkApp : UnitOfWork<SqlConnection>, IUnitOfWorkApp
{
    public IHealthRepository Health => new HealthRepository(this);
    public IUserSettingsRepository UserSettings => new UserSettingsRepository(this);
    public IFoodRepository Food => new FoodRepository(this);
    public IWeightLogRepository WeightLog => new WeightLogRepository(this);
    public ICreatineLogRepository CreatineLog => new CreatineLogRepository(this);
    public ICafeteriaMenuRepository CafeteriaMenu => new CafeteriaMenuRepository(this);
    public ICafeteriaDayOverrideRepository CafeteriaDayOverride => new CafeteriaDayOverrideRepository(this);
    public IMealLogRepository MealLog => new MealLogRepository(this);
    public IMealTemplateRepository MealTemplate => new MealTemplateRepository(this);
    public IHomeMealPlanRepository HomeMealPlan => new HomeMealPlanRepository(this);
    public IShoppingListRepository ShoppingList => new ShoppingListRepository(this);

    public UnitOfWorkApp(IConfiguration configuration)
    {
        ConnectionString = configuration.GetConnectionString("AppDb") 
            ?? configuration.GetConnectionString("DefaultConnection");
    }

    public UnitOfWorkApp(string connectionString)
    {
        ConnectionString = connectionString;
    }
}
