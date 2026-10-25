using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Application.Services;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Telegram;
using SmartBulk.Domain.Http.Telegram.Models;
using SmartBulk.Domain.Nutrition;
using SmartBulk.Domain.Repositories;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;
using Xunit;

namespace SmartBulk.UnitTests;

public class CafeteriaReminderServiceTests
{
    private readonly Mock<IUnitOfWorkApp> _uowMock = new();
    private readonly Mock<IUserSettingsRepository> _settingsRepoMock = new();
    private readonly Mock<ICafeteriaMenuRepository> _menuRepoMock = new();
    private readonly Mock<ICafeteriaMenuService> _cafeteriaServiceMock = new();
    private readonly Mock<IMealLogService> _mealLogServiceMock = new();
    private readonly Mock<ITelegramClient> _telegramClientMock = new();
    private readonly ICafeteriaReminderTracker _tracker = new CafeteriaReminderTracker();
    private readonly Mock<ILogger<CafeteriaReminderService>> _loggerMock = new();

    private readonly TelegramOptions _telegramOptions = new()
    {
        BotToken = "test-token",
        ChatId = 123456789
    };

    public CafeteriaReminderServiceTests()
    {
        _uowMock.Setup(u => u.UserSettings).Returns(_settingsRepoMock.Object);
        _uowMock.Setup(u => u.CafeteriaMenu).Returns(_menuRepoMock.Object);

        _settingsRepoMock.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSettingsEntity
            {
                TimeZoneId = "America/Sao_Paulo",
                LunchReminderTime = new TimeSpan(12, 30, 0),
                DinnerReminderTime = new TimeSpan(19, 30, 0)
            });
    }

    private CafeteriaReminderService CreateService() =>
        new(
            _uowMock.Object,
            _cafeteriaServiceMock.Object,
            _mealLogServiceMock.Object,
            _telegramClientMock.Object,
            _tracker,
            Options.Create(_telegramOptions),
            _loggerMock.Object);

    [Fact]
    public async Task CheckAndSendRemindersAsync_WhenHasCafeteriaFalse_DoesNotSend()
    {
        _tracker.Reset();

        // 2026-10-25 12:35 PM Sao Paulo (UTC-3) -> 15:35 UTC
        var currentTime = new DateTimeOffset(2026, 10, 25, 15, 35, 0, TimeSpan.Zero);
        var today = new DateOnly(2026, 10, 25);

        _cafeteriaServiceMock.Setup(c => c.GetDailyGuaranteedAsync(today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaDailyGuaranteedModel
            {
                Date = today,
                HasCafeteria = false,
                HasConfirmedMenu = true
            });

        var service = CreateService();
        await service.CheckAndSendRemindersAsync(currentTime);

        _telegramClientMock.Verify(t => t.SendMessageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<TelegramInlineKeyboardMarkupModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndSendRemindersAsync_WhenMenuNotConfirmed_DoesNotSend()
    {
        _tracker.Reset();

        var currentTime = new DateTimeOffset(2026, 10, 26, 15, 35, 0, TimeSpan.Zero); // 12:35 local
        var today = new DateOnly(2026, 10, 26);

        _cafeteriaServiceMock.Setup(c => c.GetDailyGuaranteedAsync(today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaDailyGuaranteedModel
            {
                Date = today,
                HasCafeteria = true,
                HasConfirmedMenu = false
            });

        var service = CreateService();
        await service.CheckAndSendRemindersAsync(currentTime);

        _telegramClientMock.Verify(t => t.SendMessageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<TelegramInlineKeyboardMarkupModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndSendRemindersAsync_WhenBeforeLunchTime_DoesNotSend()
    {
        _tracker.Reset();

        // 11:30 AM local -> 14:30 UTC (before 12:30 lunch)
        var currentTime = new DateTimeOffset(2026, 10, 26, 14, 30, 0, TimeSpan.Zero);
        var today = new DateOnly(2026, 10, 26);

        _cafeteriaServiceMock.Setup(c => c.GetDailyGuaranteedAsync(today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaDailyGuaranteedModel
            {
                Date = today,
                HasCafeteria = true,
                HasConfirmedMenu = true,
                Meals = new List<CafeteriaMealGuaranteedModel>
                {
                    new() { MealUuid = Guid.NewGuid(), MealType = "Lunch" }
                }
            });

        var service = CreateService();
        await service.CheckAndSendRemindersAsync(currentTime);

        _telegramClientMock.Verify(t => t.SendMessageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<TelegramInlineKeyboardMarkupModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndSendRemindersAsync_WhenLunchTime_SendsReminderWith4ButtonsAndQuotesDish()
    {
        _tracker.Reset();

        // 12:35 PM local -> 15:35 UTC
        var currentTime = new DateTimeOffset(2026, 10, 26, 15, 35, 0, TimeSpan.Zero);
        var today = new DateOnly(2026, 10, 26);
        var lunchUuid = Guid.NewGuid();

        _cafeteriaServiceMock.Setup(c => c.GetDailyGuaranteedAsync(today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaDailyGuaranteedModel
            {
                Date = today,
                HasCafeteria = true,
                HasConfirmedMenu = true,
                Meals = new List<CafeteriaMealGuaranteedModel>
                {
                    new() { MealUuid = lunchUuid, MealType = "Lunch" }
                }
            });

        var lunchEntity = new CafeteriaMealEntity
        {
            Id = 1,
            Uuid = lunchUuid,
            Date = today,
            MealType = "Lunch",
            Items = new List<CafeteriaMealItemEntity>
            {
                new()
                {
                    Id = 10,
                    RawName = "Peito de Frango",
                    ServingGrams = 150,
                    Food = new FoodEntity { Name = "Peito de frango", Calories = 165, Protein = 31 }
                }
            }
        };

        _menuRepoMock.Setup(m => m.GetMealByUuidAsync(lunchUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lunchEntity);

        var service = CreateService();
        await service.CheckAndSendRemindersAsync(currentTime);

        // Verify sent message quotes the dish and offers 4 buttons
        _telegramClientMock.Verify(t => t.SendMessageAsync(
            123456789,
            It.Is<string>(msg => msg.Contains("Hora do Almoço") && msg.Contains("Peito de frango")),
            It.Is<TelegramInlineKeyboardMarkupModel>(k =>
                k.InlineKeyboard.Count == 2 &&
                k.InlineKeyboard[0].Count == 2 &&
                k.InlineKeyboard[1].Count == 2 &&
                k.InlineKeyboard[0][0].Text.Contains("Padrão") &&
                k.InlineKeyboard[0][1].Text.Contains("Prato bom") &&
                k.InlineKeyboard[1][0].Text.Contains("Prato ruim") &&
                k.InlineKeyboard[1][1].Text.Contains("Não comi")),
            "Markdown",
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify reminder is marked as sent and not sent a second time
        await service.CheckAndSendRemindersAsync(currentTime);
        _telegramClientMock.Verify(t => t.SendMessageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<TelegramInlineKeyboardMarkupModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPlateProfileCallbackAsync_UpdatesProfileAndReturnsNewGap()
    {
        var mealUuid = Guid.NewGuid();

        _cafeteriaServiceMock.Setup(c => c.UpdateMealPlateProfileAsync(mealUuid, It.IsAny<CafeteriaMealPlateProfileUpdateModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMealModel
            {
                Uuid = mealUuid,
                PlateProfile = "Generous"
            });

        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel
            {
                CalorieGap = 500,
                ProteinGap = 20
            });

        var service = CreateService();
        var result = await service.ProcessPlateProfileCallbackAsync(123456789, $"plate:{mealUuid}:Generous");

        result.Success.Should().BeTrue();
        _cafeteriaServiceMock.Verify(c => c.UpdateMealPlateProfileAsync(
            mealUuid,
            It.Is<CafeteriaMealPlateProfileUpdateModel>(m => m.PlateProfile == "Generous"),
            It.IsAny<CancellationToken>()), Times.Once);

        _telegramClientMock.Verify(t => t.SendMessageAsync(
            123456789,
            It.Is<string>(msg => msg.Contains("Prato bom (1.25x)") && msg.Contains("500 kcal")),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPlateProfileCallbackAsync_WhenNoneProfile_UpdatesProfileSuccessfully()
    {
        var mealUuid = Guid.NewGuid();

        _cafeteriaServiceMock.Setup(c => c.UpdateMealPlateProfileAsync(mealUuid, It.IsAny<CafeteriaMealPlateProfileUpdateModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CafeteriaMealModel
            {
                Uuid = mealUuid,
                PlateProfile = "None"
            });

        _mealLogServiceMock.Setup(m => m.GetDailyGapOverviewAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyGapOverviewModel
            {
                CalorieGap = 1200,
                ProteinGap = 60
            });

        var service = CreateService();
        var result = await service.ProcessPlateProfileCallbackAsync(123456789, $"plate:{mealUuid}:None");

        result.Success.Should().BeTrue();
        _cafeteriaServiceMock.Verify(c => c.UpdateMealPlateProfileAsync(
            mealUuid,
            It.Is<CafeteriaMealPlateProfileUpdateModel>(m => m.PlateProfile == "None"),
            It.IsAny<CancellationToken>()), Times.Once);

        _telegramClientMock.Verify(t => t.SendMessageAsync(
            123456789,
            It.Is<string>(msg => msg.Contains("Não comi (0.0x)")),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

