using FluentAssertions;
using SmartBulk.Domain.Entities;
using SmartBulk.Infrastructure.Repositories;
using SmartBulk.Infrastructure.UnitOfWorks;
using Xunit;

namespace SmartBulk.IntegrationTests;

public class MealTemplateRepositoryIntegrationTests
{
    [Fact]
    public async Task MealTemplateRepository_WhenSqlServerAvailable_PerformsCrudAndSoftDelete()
    {
        var activeConnection = await TestDatabaseHelper.TryGetActiveConnectionAsync();
        if (activeConnection == null)
        {
            return;
        }

        var uow = new UnitOfWorkApp(activeConnection);
        var foodRepo = new FoodRepository(uow);
        var templateRepo = new MealTemplateRepository(uow);

        // 1. Ensure test food exists
        var food = new FoodEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Aveia Teste Template",
            Calories = 394,
            Protein = 13.9m,
            Carbohydrates = 66.6m,
            Fat = 8.5m,
            IsCustom = true
        };
        var foodId = await foodRepo.CreateAsync(food);
        food.Id = foodId;

        // 2. Create template
        var template = new MealTemplateEntity
        {
            Uuid = Guid.NewGuid(),
            Name = "Café da Manhã Padrão",
            MealType = "Breakfast"
        };
        var templateId = await templateRepo.CreateAsync(template);
        templateId.Should().BeGreaterThan(0);
        template.Id = templateId;

        // 3. Add item
        var item = new MealTemplateItemEntity
        {
            Uuid = Guid.NewGuid(),
            MealTemplateId = templateId,
            FoodId = foodId,
            ServingGrams = 80
        };
        var itemId = await templateRepo.CreateItemAsync(item);
        itemId.Should().BeGreaterThan(0);

        // 4. Get by Uuid with populated items and food
        var fetched = await templateRepo.GetByUuidAsync(template.Uuid);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Café da Manhã Padrão");
        fetched.MealType.Should().Be("Breakfast");
        fetched.Items.Should().HaveCount(1);
        fetched.Items[0].Food.Should().NotBeNull();
        fetched.Items[0].Food!.Name.Should().Be("Aveia Teste Template");
        fetched.Items[0].ServingGrams.Should().Be(80);

        // 5. Get all filtered by meal type
        var breakfasts = await templateRepo.GetAllAsync("Breakfast");
        breakfasts.Should().Contain(t => t.Uuid == template.Uuid);

        // 6. Update template name and meal type
        fetched.Name = "Café Hipercalórico";
        fetched.MealType = "Breakfast";
        var updated = await templateRepo.UpdateAsync(fetched);
        updated.Should().BeTrue();

        // 7. Replace items
        var newItem = new MealTemplateItemEntity
        {
            Uuid = Guid.NewGuid(),
            MealTemplateId = templateId,
            FoodId = foodId,
            ServingGrams = 120
        };
        await templateRepo.ReplaceItemsAsync(templateId, new[] { newItem });

        var afterReplace = await templateRepo.GetByUuidAsync(template.Uuid);
        afterReplace.Should().NotBeNull();
        afterReplace!.Items.Should().HaveCount(1);
        afterReplace.Items[0].ServingGrams.Should().Be(120);

        // 8. Soft delete template
        var deleted = await templateRepo.DeleteAsync(template.Uuid);
        deleted.Should().BeTrue();

        var afterDelete = await templateRepo.GetByUuidAsync(template.Uuid);
        afterDelete.Should().BeNull();
    }
}

