using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Application.Validations;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Llm;
using SmartBulk.Domain.Http.Llm.Models;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class NaturalLanguageMealLogServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<IFoodRepository> _foodRepoMock = new();
    private readonly Mock<ILlmClient> _llmClientMock = new();
    private readonly Mock<IMealLogService> _mealLogServiceMock = new();
    private readonly Mock<IFoodService> _foodServiceMock = new();
    private readonly IValidator<NaturalLanguageMealLogRequestModel> _requestValidator = new NaturalLanguageMealLogRequestModelValidator();
    private readonly IValidator<NaturalLanguageMealLlmExtractionModel> _extractionValidator = new NaturalLanguageMealLlmExtractionModelValidator();
    private readonly Mock<ILogger<NaturalLanguageMealLogService>> _loggerMock = new();

    public NaturalLanguageMealLogServiceTests()
    {
        _uowMock.Setup(u => u.Food).Returns(_foodRepoMock.Object);
    }

    private NaturalLanguageMealLogService CreateService() =>
        new(
            _uowMock.Object,
            _llmClientMock.Object,
            _mealLogServiceMock.Object,
            _foodServiceMock.Object,
            _requestValidator,
            _extractionValidator,
            _loggerMock.Object);

    [Fact]
    public async Task Process_WhenRequestHasNoTextAndNoImage_FailsValidation()
    {
        var service = CreateService();
        var result = await service.ProcessNaturalLanguageMealAsync(new NaturalLanguageMealLogRequestModel());

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("texto ou envie uma imagem"));
        _llmClientMock.Verify(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenLlmReturnsQuotaExceeded_ReturnsFriendlyQuotaMessage()
    {
        _llmClientMock.Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Fail("Limite de taxa ou cota excedida na LLM (HTTP 429)."));

        var service = CreateService();
        var result = await service.ProcessNaturalLanguageMealAsync(new NaturalLanguageMealLogRequestModel
        {
            Text = "comi 3 ovos e 2 pães"
        });

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cota esgotada") || e.Contains("Limite de requisições"));
    }

    [Fact]
    public async Task Process_WhenLlmFailsFirstAttemptThenSucceedsOnRetry_ProcessesCorrectly()
    {
        const string invalidJson = "invalid json response";
        const string validJson = @"{
            ""mealName"": ""Café da Manhã"",
            ""items"": [
                { ""name"": ""Ovo cozido"", ""grams"": 150 }
            ]
        }";

        _llmClientMock.SetupSequence(l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok(invalidJson))
            .ReturnsAsync(Result<string>.Ok(validJson));

        var eggFood = new FoodEntity
        {
            Id = 1,
            Uuid = Guid.NewGuid(),
            Name = "Ovo de galinha cozido",
            Calories = 146,
            Protein = 13.3m
        };

        _foodRepoMock.Setup(f => f.SearchAsync(null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity> { eggFood });

        var mealUuid = Guid.NewGuid();
        _mealLogServiceMock.Setup(m => m.CreateMealLogAsync(It.IsAny<MealLogCreateModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealLogModel
            {
                Uuid = mealUuid,
                Name = "Café da Manhã",
                TotalCalories = 219,
                TotalProtein = 20
            });

        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel { CalorieGap = 1000, ProteinGap = 50 });

        var service = CreateService();
        var result = await service.ProcessNaturalLanguageMealAsync(new NaturalLanguageMealLogRequestModel
        {
            Text = "comi 3 ovos cozidos"
        });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.MealLogUuid.Should().Be(mealUuid);
        result.Data.HasUnmatchedItems.Should().BeFalse();
        _llmClientMock.Verify(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Process_WhenFoodCannotBeMatched_DoesNotSaveAndReturnsQuestion()
    {
        const string validJson = @"{
            ""mealName"": ""Lanche Misterioso"",
            ""items"": [
                { ""name"": ""Fruta Exotica do Himalaia"", ""grams"": 200 }
            ]
        }";

        _llmClientMock.Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok(validJson));

        var eggFood = new FoodEntity { Id = 1, Uuid = Guid.NewGuid(), Name = "Ovo cozido" };
        _foodRepoMock.Setup(f => f.SearchAsync(null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity> { eggFood });

        _foodServiceMock.Setup(f => f.ResolveFoodByNameAsync("Fruta Exotica do Himalaia", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FoodModel>.Fail("Não encontrado"));

        var service = CreateService();
        var result = await service.ProcessNaturalLanguageMealAsync(new NaturalLanguageMealLogRequestModel
        {
            Text = "comi 200g de fruta exotica do himalaia"
        });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.HasUnmatchedItems.Should().BeTrue();
        result.Data.UnmatchedItems.Should().Contain("Fruta Exotica do Himalaia");
        result.Data.Question.Should().Contain("Não encontrei os seguintes alimentos");
        result.Data.MealLogUuid.Should().BeNull();

        _mealLogServiceMock.Verify(m => m.CreateMealLogAsync(It.IsAny<MealLogCreateModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenFoodNotFoundLocally_ResolvesViaOpenFoodFactsFallbackAndLogsMeal()
    {
        const string validJson = @"{
            ""mealName"": ""Lanche Proteico"",
            ""items"": [
                { ""name"": ""Iogurte Yopro Chocolate"", ""grams"": 250 }
            ]
        }";

        _llmClientMock.Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok(validJson));

        // Banco local não tem o alimento
        _foodRepoMock.Setup(f => f.SearchAsync(null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodEntity>());

        var resolvedUuid = Guid.NewGuid();
        var resolvedFood = new FoodModel
        {
            Uuid = resolvedUuid,
            Name = "Iogurte Yopro Chocolate (Danone)",
            Calories = 64,
            Protein = 6.8m,
            Carbohydrates = 7.2m,
            Fat = 0.9m,
            IsCustom = true
        };

        _foodServiceMock.Setup(f => f.ResolveFoodByNameAsync("Iogurte Yopro Chocolate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FoodModel>.Ok(resolvedFood));

        var mealUuid = Guid.NewGuid();
        _mealLogServiceMock.Setup(m => m.CreateMealLogAsync(It.IsAny<MealLogCreateModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealLogModel
            {
                Uuid = mealUuid,
                Name = "Lanche Proteico",
                TotalCalories = 160,
                TotalProtein = 17
            });

        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel { CalorieGap = 800, ProteinGap = 30 });

        var service = CreateService();
        var result = await service.ProcessNaturalLanguageMealAsync(new NaturalLanguageMealLogRequestModel
        {
            Text = "tomei um iogurte yopro chocolate de 250g"
        });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.HasUnmatchedItems.Should().BeFalse();
        result.Data.MealLogUuid.Should().Be(mealUuid);
        result.Data.Summary.Should().Contain("Iogurte Yopro Chocolate (Danone)");

        _foodServiceMock.Verify(f => f.ResolveFoodByNameAsync("Iogurte Yopro Chocolate", It.IsAny<CancellationToken>()), Times.Once);
        _mealLogServiceMock.Verify(m => m.CreateMealLogAsync(It.Is<MealLogCreateModel>(c => c.Items.Any(i => i.FoodUuid == resolvedUuid)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenAllFoodsMatch_SavesImmediatelyAndReturnsSummaryAndNewGap()
    {
        const string validJson = @"{
            ""mealName"": ""Café da Manhã Reforçado"",
            ""items"": [
                { ""name"": ""Ovo cozido"", ""grams"": 150 },
                { ""name"": ""Pão francês"", ""grams"": 100 },
                { ""name"": ""Requeijão"", ""grams"": 30 }
            ]
        }";

        _llmClientMock.Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok(validJson));

        var foods = new List<FoodEntity>
        {
            new() { Id = 1, Uuid = Guid.NewGuid(), Name = "Ovo de galinha cozido", Calories = 146, Protein = 13.3m },
            new() { Id = 2, Uuid = Guid.NewGuid(), Name = "Pão, francês", Calories = 300, Protein = 8 },
            new() { Id = 3, Uuid = Guid.NewGuid(), Name = "Requeijão cremoso", Calories = 250, Protein = 9 }
        };

        _foodRepoMock.Setup(f => f.SearchAsync(null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foods);

        var savedMealUuid = Guid.NewGuid();
        _mealLogServiceMock.Setup(m => m.CreateMealLogAsync(It.IsAny<MealLogCreateModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealLogModel
            {
                Uuid = savedMealUuid,
                Name = "Café da Manhã Reforçado",
                TotalCalories = 594,
                TotalProtein = 30.65m,
                TotalCarbohydrates = 60,
                TotalFat = 25
            });

        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel
            {
                CalorieTarget = 3000,
                ProteinTarget = 160,
                CalorieGap = 1200,
                ProteinGap = 65
            });

        var service = CreateService();
        var result = await service.ProcessNaturalLanguageMealAsync(new NaturalLanguageMealLogRequestModel
        {
            Text = "comi 3 ovos cozidos e 2 paes franceses com requeijao"
        });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.HasUnmatchedItems.Should().BeFalse();
        result.Data.MealLogUuid.Should().Be(savedMealUuid);
        result.Data.MealName.Should().Be("Café da Manhã Reforçado");
        result.Data.TotalCalories.Should().Be(594);
        result.Data.Summary.Should().Contain("Ovo de galinha cozido (150g)");
        result.Data.Summary.Should().Contain("Pão, francês (100g)");
        result.Data.NewDailyGap.Should().NotBeNull();
        result.Data.NewDailyGap!.CalorieGap.Should().Be(1200);

        _mealLogServiceMock.Verify(m => m.CreateMealLogAsync(It.Is<MealLogCreateModel>(c => c.Items.Count == 3), It.IsAny<CancellationToken>()), Times.Once);
    }
}
