using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Domain.Http.Telegram;
using SmartBulk.Domain.Http.Telegram.Models;
using SmartBulk.Domain.Results;
using Xunit;

namespace SmartBulk.UnitTests;

public class TelegramUpdateHandlerTests
{
    private readonly Mock<ITelegramClient> _telegramClientMock = new();
    private readonly Mock<INaturalLanguageMealLogService> _naturalLanguageServiceMock = new();
    private readonly Mock<IMealLogService> _mealLogServiceMock = new();
    private readonly Mock<ICafeteriaReminderService> _reminderServiceMock = new();
    private readonly Mock<ICafeteriaMenuService> _cafeteriaMenuServiceMock = new();
    private readonly Mock<ILogger<TelegramUpdateHandler>> _loggerMock = new();

    private readonly TelegramOptions _options = new()
    {
        BotToken = "123456:ABC-DEF",
        ChatId = 999888777,
        SecretToken = "test-secret"
    };

    private TelegramUpdateHandler CreateHandler() =>
        new(
            _telegramClientMock.Object,
            _naturalLanguageServiceMock.Object,
            _mealLogServiceMock.Object,
            _reminderServiceMock.Object,
            _cafeteriaMenuServiceMock.Object,
            Options.Create(_options),
            _loggerMock.Object);

    [Fact]
    public async Task HandleUpdateAsync_WhenUnauthorizedChatId_IgnoresSilentlyWithoutReplying()
    {
        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 1,
            Message = new TelegramMessageModel
            {
                MessageId = 10,
                Chat = new TelegramChatModel { Id = 111222333 }, // unauthorized chat
                Text = "comi 3 ovos"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _telegramClientMock.Verify(t => t.SendMessageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<TelegramInlineKeyboardMarkupModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _naturalLanguageServiceMock.Verify(n => n.ProcessNaturalLanguageMealAsync(It.IsAny<NaturalLanguageMealLogRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenStartCommand_SendsWelcomeMessage()
    {
        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 2,
            Message = new TelegramMessageModel
            {
                MessageId = 11,
                Chat = new TelegramChatModel { Id = 999888777 },
                Text = "/start"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _telegramClientMock.Verify(t => t.SendMessageAsync(
            999888777,
            It.Is<string>(msg => msg.Contains("SmartBulk Bot")),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenGapCommand_SendsCurrentGap()
    {
        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel
            {
                CalorieTarget = 3000,
                ProteinTarget = 160,
                CalorieGap = 800,
                ProteinGap = 45
            });

        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 3,
            Message = new TelegramMessageModel
            {
                MessageId = 12,
                Chat = new TelegramChatModel { Id = 999888777 },
                Text = "/gap"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _telegramClientMock.Verify(t => t.SendMessageAsync(
            999888777,
            It.Is<string>(msg => msg.Contains("800 kcal") && msg.Contains("45 g")),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenUndoCommand_DeletesLatestMealAndSendsUpdatedGap()
    {
        _mealLogServiceMock.Setup(m => m.DeleteLatestMealLogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel
            {
                CalorieGap = 1200,
                ProteinGap = 70
            });

        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 4,
            Message = new TelegramMessageModel
            {
                MessageId = 13,
                Chat = new TelegramChatModel { Id = 999888777 },
                Text = "/desfazer"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _mealLogServiceMock.Verify(m => m.DeleteLatestMealLogAsync(It.IsAny<CancellationToken>()), Times.Once);
        _telegramClientMock.Verify(t => t.SendMessageAsync(
            999888777,
            It.Is<string>(msg => msg.Contains("Última refeição desfeita")),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenNaturalLanguageMealSucceeds_SendsSummaryAndUndoButton()
    {
        var mealUuid = Guid.NewGuid();
        _naturalLanguageServiceMock.Setup(n => n.ProcessNaturalLanguageMealAsync(It.IsAny<NaturalLanguageMealLogRequestModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NaturalLanguageMealLogResponseModel
            {
                Success = true,
                HasUnmatchedItems = false,
                MealName = "Café da Manhã",
                Summary = "Ovo cozido (150g), Pão (100g)",
                TotalCalories = 450,
                TotalProtein = 22,
                TotalCarbohydrates = 50,
                TotalFat = 15,
                MealLogUuid = mealUuid,
                NewDailyGap = new DailyGapOverviewModel { CalorieGap = 1500, ProteinGap = 80 }
            });

        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 5,
            Message = new TelegramMessageModel
            {
                MessageId = 14,
                Chat = new TelegramChatModel { Id = 999888777 },
                Text = "comi 3 ovos e 2 paes"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _telegramClientMock.Verify(t => t.SendMessageAsync(
            999888777,
            It.Is<string>(msg => msg.Contains("Café da Manhã") && msg.Contains("450 kcal")),
            It.Is<TelegramInlineKeyboardMarkupModel>(markup => markup.InlineKeyboard[0][0].CallbackData == $"undo:{mealUuid}"),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenNaturalLanguageHasUnmatchedItems_SendsClarifyingQuestion()
    {
        _naturalLanguageServiceMock.Setup(n => n.ProcessNaturalLanguageMealAsync(It.IsAny<NaturalLanguageMealLogRequestModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NaturalLanguageMealLogResponseModel
            {
                Success = true,
                HasUnmatchedItems = true,
                Question = "Não encontrei o alimento 'Pitaya Dourada'. Qual alimento deseja selecionar?"
            });

        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 6,
            Message = new TelegramMessageModel
            {
                MessageId = 15,
                Chat = new TelegramChatModel { Id = 999888777 },
                Text = "comi 1 pitaya dourada"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _telegramClientMock.Verify(t => t.SendMessageAsync(
            999888777,
            It.Is<string>(msg => msg.Contains("Pitaya Dourada")),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenCallbackQueryUndo_AnswersAndDeletesMeal()
    {
        var mealUuid = Guid.NewGuid();

        _mealLogServiceMock.Setup(m => m.DeleteMealLogAsync(mealUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel { CalorieGap = 1800, ProteinGap = 90 });

        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 7,
            CallbackQuery = new TelegramCallbackQueryModel
            {
                Id = "cb_123",
                From = new TelegramChatModel { Id = 999888777 },
                Data = $"undo:{mealUuid}"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _telegramClientMock.Verify(t => t.AnswerCallbackQueryAsync("cb_123", "Processando...", It.IsAny<CancellationToken>()), Times.Once);
        _mealLogServiceMock.Verify(m => m.DeleteMealLogAsync(mealUuid, It.IsAny<CancellationToken>()), Times.Once);
        _telegramClientMock.Verify(t => t.SendMessageAsync(
            999888777,
            It.Is<string>(msg => msg.Contains("Refeição desfeita com sucesso")),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenCallbackQueryPlateProfile_DelegatesToReminderService()
    {
        var mealUuid = Guid.NewGuid();
        _reminderServiceMock.Setup(r => r.ProcessPlateProfileCallbackAsync(999888777, $"plate:{mealUuid}:Generous", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 8,
            CallbackQuery = new TelegramCallbackQueryModel
            {
                Id = "cb_456",
                From = new TelegramChatModel { Id = 999888777 },
                Data = $"plate:{mealUuid}:Generous"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _telegramClientMock.Verify(t => t.AnswerCallbackQueryAsync("cb_456", "Processando...", It.IsAny<CancellationToken>()), Times.Once);
        _reminderServiceMock.Verify(r => r.ProcessPlateProfileCallbackAsync(999888777, $"plate:{mealUuid}:Generous", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenStatusCommand_SendsCurrentGap()
    {
        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel
            {
                CalorieTarget = 2800,
                ProteinTarget = 150,
                CalorieGap = 650,
                ProteinGap = 35
            });

        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 9,
            Message = new TelegramMessageModel
            {
                MessageId = 16,
                Chat = new TelegramChatModel { Id = 999888777 },
                Text = "/status"
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _telegramClientMock.Verify(t => t.SendMessageAsync(
            999888777,
            It.Is<string>(msg => msg.Contains("650 kcal") && msg.Contains("35 g")),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleUpdateAsync_WhenPhotoWithoutMealCaption_IngestsWeeklyCafeteriaMenu()
    {
        _telegramClientMock.Setup(t => t.GetFileAsync("photo_file_id_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TelegramFileModel>.Ok(new TelegramFileModel { FilePath = "photos/file_1.jpg" }));

        _telegramClientMock.Setup(t => t.DownloadFileAsBase64Async("photos/file_1.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok("dGVzdGZpbGVjb250ZW50"));

        _cafeteriaMenuServiceMock.Setup(c => c.IngestMenuAsync(It.IsAny<CafeteriaMenuIngestModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CafeteriaMenuModel>.Ok(new CafeteriaMenuModel
            {
                WeekStartDate = new DateOnly(2026, 10, 26),
                Meals = new List<CafeteriaMealModel>
                {
                    new() { Date = new DateOnly(2026, 10, 26), MealType = "Lunch" },
                    new() { Date = new DateOnly(2026, 10, 26), MealType = "Dinner" },
                    new() { Date = new DateOnly(2026, 10, 27), MealType = "Lunch" },
                    new() { Date = new DateOnly(2026, 10, 27), MealType = "Dinner" },
                    new() { Date = new DateOnly(2026, 10, 28), MealType = "Lunch" },
                    new() { Date = new DateOnly(2026, 10, 28), MealType = "Dinner" },
                    new() { Date = new DateOnly(2026, 10, 29), MealType = "Lunch" },
                    new() { Date = new DateOnly(2026, 10, 29), MealType = "Dinner" },
                    new() { Date = new DateOnly(2026, 10, 30), MealType = "Lunch" },
                    new() { Date = new DateOnly(2026, 10, 30), MealType = "Dinner" }
                }
            }));

        var handler = CreateHandler();

        var update = new TelegramUpdateModel
        {
            UpdateId = 10,
            Message = new TelegramMessageModel
            {
                MessageId = 17,
                Chat = new TelegramChatModel { Id = 999888777 },
                Photo = new List<TelegramPhotoSizeModel>
                {
                    new() { FileId = "thumb", FileSize = 100 },
                    new() { FileId = "photo_file_id_1", FileSize = 5000 }
                }
            }
        };

        var result = await handler.HandleUpdateAsync(update);

        result.Success.Should().BeTrue();
        _cafeteriaMenuServiceMock.Verify(c => c.IngestMenuAsync(
            It.Is<CafeteriaMenuIngestModel>(m => m.ImageBase64 == "dGVzdGZpbGVjb250ZW50"),
            It.IsAny<CancellationToken>()), Times.Once);

        _telegramClientMock.Verify(t => t.SendMessageAsync(
            999888777,
            It.Is<string>(msg => msg.Contains("Cardápio semanal extraído com sucesso") && msg.Contains("Total de refeições: 10")),
            null,
            "Markdown",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

