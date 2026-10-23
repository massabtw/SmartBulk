using SmartBulk.Domain.Repositories;

namespace SmartBulk.Domain.UnitOfWorks;

public interface IUnitOfWorkApp : IUnitOfWork
{
    IHealthRepository Health { get; }
    IUserSettingsRepository UserSettings { get; }
    IFoodRepository Food { get; }
    IWeightLogRepository WeightLog { get; }
    ICreatineLogRepository CreatineLog { get; }
    ICafeteriaMenuRepository CafeteriaMenu { get; }
    ICafeteriaDayOverrideRepository CafeteriaDayOverride { get; }
    IMealLogRepository MealLog { get; }
}
