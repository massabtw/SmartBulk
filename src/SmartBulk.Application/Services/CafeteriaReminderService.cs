using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartBulk.Application.Helpers;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Entities;
using SmartBulk.Domain.Http.Telegram;
using SmartBulk.Domain.Http.Telegram.Models;
using SmartBulk.Domain.Nutrition;
using SmartBulk.Domain.Results;
using SmartBulk.Domain.UnitOfWorks;

namespace SmartBulk.Application.Services;

public class CafeteriaReminderService : ICafeteriaReminderService
{
    private readonly IUnitOfWorkApp _unitOfWork;
    private readonly ICafeteriaMenuService _cafeteriaService;
    private readonly IMealLogService _mealLogService;
    private readonly ITelegramClient _telegramClient;
    private readonly ICafeteriaReminderTracker _reminderTracker;
    private readonly TelegramOptions _telegramOptions;
    private readonly ILogger<CafeteriaReminderService> _logger;

    public CafeteriaReminderService(
        IUnitOfWorkApp unitOfWork,
        ICafeteriaMenuService cafeteriaService,
        IMealLogService mealLogService,
        ITelegramClient telegramClient,
        ICafeteriaReminderTracker reminderTracker,
        IOptions<TelegramOptions> telegramOptions,
        ILogger<CafeteriaReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _cafeteriaService = cafeteriaService;
        _mealLogService = mealLogService;
        _telegramClient = telegramClient;
        _reminderTracker = reminderTracker;
        _telegramOptions = telegramOptions.Value;
        _logger = logger;
    }

    public async Task CheckAndSendRemindersAsync(DateTimeOffset currentTimeUtc, CancellationToken cancellationToken = default)
    {
        if (_telegramOptions.ChatId == 0 || string.IsNullOrWhiteSpace(_telegramOptions.BotToken))
        {
            return;
        }

        try
        {
            // 1. Get UserSettings (reminder times and timezone)
            var settings = await _unitOfWork.UserSettings.GetAsync(cancellationToken);
            var tzId = string.IsNullOrWhiteSpace(settings?.TimeZoneId) ? "America/Sao_Paulo" : settings.TimeZoneId;
            var localTime = TimeZoneDateHelper.ToLocalTime(currentTimeUtc, tzId);
            var today = DateOnly.FromDateTime(localTime.DateTime);
            var timeOfDay = localTime.TimeOfDay;

            // 2. Check if today has HasCafeteria = true and confirmed menu
            var guaranteedResult = await _cafeteriaService.GetDailyGuaranteedAsync(today, cancellationToken);
            if (!guaranteedResult.Success || guaranteedResult.Data is null)
            {
                return;
            }

            var guaranteed = guaranteedResult.Data;
            if (!guaranteed.HasCafeteria || !guaranteed.HasConfirmedMenu)
            {
                return;
            }

            var lunchTime = settings?.LunchReminderTime ?? new TimeSpan(12, 30, 0);
            var dinnerTime = settings?.DinnerReminderTime ?? new TimeSpan(19, 30, 0);

            // 3. Lunch Reminder
            if (timeOfDay >= lunchTime && timeOfDay < dinnerTime)
            {
                if (!_reminderTracker.HasBeenSent(today, "Lunch"))
                {
                    var lunchMeal = guaranteed.Meals.FirstOrDefault(m => string.Equals(m.MealType, "Lunch", StringComparison.OrdinalIgnoreCase));
                    if (lunchMeal != null)
                    {
                        await SendMealReminderAsync(lunchMeal.MealUuid, "Almoço", today, cancellationToken);
                        _reminderTracker.MarkAsSent(today, "Lunch");
                    }
                }
            }

            // 4. Dinner Reminder
            if (timeOfDay >= dinnerTime)
            {
                if (!_reminderTracker.HasBeenSent(today, "Dinner"))
                {
                    var dinnerMeal = guaranteed.Meals.FirstOrDefault(m => string.Equals(m.MealType, "Dinner", StringComparison.OrdinalIgnoreCase));
                    if (dinnerMeal != null)
                    {
                        await SendMealReminderAsync(dinnerMeal.MealUuid, "Jantar", today, cancellationToken);
                        _reminderTracker.MarkAsSent(today, "Dinner");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cafeteria reminders for time {Time}", currentTimeUtc);
        }
    }

    public async Task<Result> ProcessPlateProfileCallbackAsync(long chatId, string callbackData, CancellationToken cancellationToken = default)
    {
        try
        {
            // Format: "plate:{mealUuid}:{profileName}"
            var parts = callbackData.Split(':');
            if (parts.Length < 3 || !Guid.TryParse(parts[1], out var mealUuid))
            {
                return Result.Fail("Callback de perfil de prato inválido.");
            }

            var profileStr = parts[2].Trim();
            var profile = PlateProfileExtensions.ParseOrDefault(profileStr);

            var updateModel = new CafeteriaMealPlateProfileUpdateModel
            {
                PlateProfile = profile.ToString()
            };

            var updateResult = await _cafeteriaService.UpdateMealPlateProfileAsync(mealUuid, updateModel, cancellationToken);
            if (!updateResult.Success)
            {
                await _telegramClient.SendMessageAsync(chatId, "Não foi possível atualizar o perfil do prato.", cancellationToken: cancellationToken);
                return updateResult;
            }

            var gapResult = await _mealLogService.GetDailyGapOverviewAsync(null, cancellationToken);
            var newGap = gapResult.Data;

            var confirmationMessage = $"🍽️ Perfil do prato registrado: *{profile.GetDisplayName()}*!\n\n" +
                                      $"🎯 Gap atualizado de hoje:\n" +
                                      $"• Calorias: {newGap?.CalorieGap ?? 0:0.#} kcal\n" +
                                      $"• Proteína: {newGap?.ProteinGap ?? 0:0.#} g";

            await _telegramClient.SendMessageAsync(chatId, confirmationMessage, cancellationToken: cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing plate profile callback: {Data}", callbackData);
            return ErrorMessages.UnknownError;
        }
    }

    private async Task SendMealReminderAsync(
        Guid mealUuid,
        string mealDisplayName,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var mealEntity = await _unitOfWork.CafeteriaMenu.GetMealByUuidAsync(mealUuid, cancellationToken);
        var itemsSummary = mealEntity?.Items != null && mealEntity.Items.Count > 0
            ? string.Join("\n", mealEntity.Items.Select(i => $"• {i.Food?.Name ?? i.RawName} ({(int)i.ServingGrams}g)"))
            : "• Prato do refeitório";

        decimal standardCalories = 0;
        decimal standardProtein = 0;
        if (mealEntity?.Items != null)
        {
            foreach (var item in mealEntity.Items)
            {
                if (item.Food != null && item.ServingGrams > 0)
                {
                    var nut = NutritionalValue.FromPer100G(item.Food.Calories, item.Food.Protein, item.Food.Carbohydrates, item.Food.Fat, item.ServingGrams);
                    standardCalories += nut.Calories;
                    standardProtein += nut.Protein;
                }
            }
        }

        var messageText = $"🍽️ *Hora do {mealDisplayName}!*\n" +
                          $"Cardápio de hoje no refeitório:\n" +
                          $"{itemsSummary}\n" +
                          $"_({(int)standardCalories} kcal | {(int)standardProtein}g proteína)_\n\n" +
                          $"Como foi o seu prato?";

        var keyboard = new TelegramInlineKeyboardMarkupModel
        {
            InlineKeyboard = new List<List<TelegramInlineKeyboardButtonModel>>
            {
                new()
                {
                    new() { Text = "Padrão (1.0x)", CallbackData = $"plate:{mealUuid}:Standard" },
                    new() { Text = "Prato bom (1.25x)", CallbackData = $"plate:{mealUuid}:Generous" }
                },
                new()
                {
                    new() { Text = "Prato ruim (0.75x)", CallbackData = $"plate:{mealUuid}:Small" },
                    new() { Text = "Não comi (0.0x)", CallbackData = $"plate:{mealUuid}:None" }
                }
            }
        };

        await _telegramClient.SendMessageAsync(
            _telegramOptions.ChatId,
            messageText,
            replyMarkup: keyboard,
            parseMode: "Markdown",
            cancellationToken: cancellationToken);
    }
}

