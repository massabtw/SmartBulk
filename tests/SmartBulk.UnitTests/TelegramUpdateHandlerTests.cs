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
}

