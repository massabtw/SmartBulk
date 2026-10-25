using FluentAssertions;
using SmartBulk.Domain.Constants;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class HomeMealPlanRepositoryIntegrationTests
{
    [Fact]
    public async Task HomeMealPlanRepository_WhenSqlServerAvailable_PerformsCrudAndSoftDelete()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync();
        if (activeConnection == null)
        {
            return;
        }

        var uow = new UnitOfWorkApp(activeConnection);
        var foodRepo = new FoodRepository(uow);
        var templateRepo = new MealTemplateRepository(uow);
        var planRepo = new HomeMealPlanRepository(uow);

        // 1. Food
        var food = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Banana Prata Plan Test",
            Calories = 98,
            Protein = 1.3m,
            Carbohydrates = 26m,
            Fat = 0.1m,
            IsCustom = true
        };
        var foodId = await foodRepo.CreateAsync(food);
        food.Id = foodId;

        // 2. Template
        var template = new MealTemplateEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Shake Teste Repo",
            MealType = MealTemplateTypes.Shake
        };
        var templateId = await templateRepo.CreateAsync(template);
        await templateRepo.CreateItemAsync(new MealTemplateItemEntity
        {
            Uuid = Guid.NewGuid(),
            MealTemplateId = templateId,
            FoodId = foodId,
            ServingGrams = 120
        });

        // 3. Create HomeMealPlan
        var monday = new DateOnly(2027, 1, 4);
        // Clean up any existing
        var existing = await planRepo.GetByWeekStartDateAsync(monday);
        if (existing != null)
        {
            await planRepo.DeleteAsync(existing.Uuid);
        }

        var plan = new HomeMealPlanEntity
        {
            Uuid = Guid.NewGuid(),
            WeekStartDate = monday
        };
        var planId = await planRepo.CreateAsync(plan);
        planId.Should().BeGreaterThan(0);
        plan.Id = planId;

        // 4. Create items
        var item1 = new HomeMealPlanItemEntity
        {
            Uuid = Guid.NewGuid(),
            HomeMealPlanId = planId,
            Date = monday,
            MealSlot = HomeMealSlots.Shake,
            MealTemplateId = templateId
        };
        var itemId = await planRepo.CreateItemAsync(item1);
        itemId.Should().BeGreaterThan(0);

        // 5. Get by week start date
        var fetched = await planRepo.GetByWeekStartDateAsync(monday);
        fetched.Should().NotBeNull();
        fetched!.WeekStartDate.Should().Be(monday);
        fetched.Items.Should().HaveCount(1);
        fetched.Items[0].MealSlot.Should().Be(HomeMealSlots.Shake);
        fetched.Items[0].MealTemplate.Should().NotBeNull();
        fetched.Items[0].MealTemplate!.Name.Should().Be("Shake Teste Repo");
        fetched.Items[0].MealTemplate!.Items.Should().HaveCount(1);
        fetched.Items[0].MealTemplate!.Items[0].Food.Should().NotBeNull();
        fetched.Items[0].MealTemplate!.Items[0].Food!.Name.Should().Be("Banana Prata Plan Test");

        // 6. Replace items
        var item2 = new HomeMealPlanItemEntity
        {
            Uuid = Guid.NewGuid(),
            HomeMealPlanId = planId,
            Date = monday.AddDays(1),
            MealSlot = HomeMealSlots.Breakfast,
            MealTemplateId = templateId
        };
        await planRepo.ReplaceItemsAsync(planId, new[] { item2 });

        var fetchedAfterReplace = await planRepo.GetByWeekStartDateAsync(monday);
        fetchedAfterReplace.Should().NotBeNull();
        fetchedAfterReplace!.Items.Should().HaveCount(1);
        fetchedAfterReplace.Items[0].Date.Should().Be(monday.AddDays(1));
        fetchedAfterReplace.Items[0].MealSlot.Should().Be(HomeMealSlots.Breakfast);

        // 7. Soft delete
        var deleted = await planRepo.DeleteAsync(plan.Uuid);
        deleted.Should().BeTrue();

        var fetchedAfterDelete = await planRepo.GetByWeekStartDateAsync(monday);
        fetchedAfterDelete.Should().BeNull();
    }
}

