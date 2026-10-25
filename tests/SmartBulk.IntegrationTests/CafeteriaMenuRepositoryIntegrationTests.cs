using FluentAssertions;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class CafeteriaMenuRepositoryIntegrationTests
{
    [Fact]
    public async Task CafeteriaMenuRepository_WhenSqlServerAvailable_PerformsPersistenceSuccessfully()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync();
        if (activeConnection == null)
        {
            return;
        }

        var uow = new UnitOfWorkApp(activeConnection);
        var repo = new CafeteriaMenuRepository(uow);
        var foodRepo = new FoodRepository(uow);

        var monday = new DateOnly(2026, 11, 2);

        // Create menu
        var menu = new CafeteriaMenuEntity
        {
            WeekStartDate = monday,
            Status = "Draft"
        };
        var menuId = await repo.CreateMenuAsync(menu);
        menuId.Should().BeGreaterThan(0);
        menu.Uuid.Should().NotBeEmpty();

        // Create meal
        var meal = new CafeteriaMealEntity
        {
            CafeteriaMenuId = menuId,
            Date = monday,
            MealType = "Lunch",
            PlateProfile = "Standard"
        };
        var mealId = await repo.CreateMealAsync(meal);
        mealId.Should().BeGreaterThan(0);
        meal.Uuid.Should().NotBeEmpty();

        // Get a food from TACO
        var tacoFoods = await foodRepo.SearchAsync("arroz", 1, 1);
        var food = tacoFoods.FirstOrDefault();

        // Create meal item
        var item = new CafeteriaMealItemEntity
        {
            CafeteriaMealId = mealId,
            FoodId = food?.Id,
            RawName = "Arroz branco",
            ServingGrams = 150m,
            IsPending = food == null
        };
        var itemId = await repo.CreateMealItemAsync(item);
        itemId.Should().BeGreaterThan(0);
        item.Uuid.Should().NotBeEmpty();

        // Retrieve full menu
        var loaded = await repo.GetByWeekStartDateAsync(monday);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("Draft");
        loaded.Meals.Should().ContainSingle();
        loaded.Meals[0].Items.Should().ContainSingle();
        loaded.Meals[0].Items[0].RawName.Should().Be("Arroz branco");

        // Retrieve menu by mealId
        var loadedByMeal = await repo.GetByMealIdAsync(mealId);
        loadedByMeal.Should().NotBeNull();
        loadedByMeal!.Id.Should().Be(menuId);

        // Update status
        var updated = await repo.UpdateStatusAsync(menuId, "Confirmed");
        updated.Should().BeTrue();

        var reloaded = await repo.GetByWeekStartDateAsync(monday);
        reloaded!.Status.Should().Be("Confirmed");
    }
}
