using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartBulk.Application.Interfaces;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Http.Telegram;
using SmartBulk.Domain.Http.Telegram.Models;
using SmartBulk.Domain.Results;

namespace SmartBulk.Application.Services;

public class TelegramUpdateHandler : ITelegramUpdateHandler
{
    private readonly ITelegramClient _telegramClient;
    private readonly INaturalLanguageMealLogService _naturalLanguageService;
    private readonly IMealLogService _mealLogService;
    private readonly ICafeteriaReminderService _reminderService;
    private readonly ICafeteriaMenuService _cafeteriaService;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(
        ITelegramClient telegramClient,
        INaturalLanguageMealLogService naturalLanguageService,
        IMealLogService mealLogService,
        ICafeteriaReminderService reminderService,
        ICafeteriaMenuService cafeteriaService,
        IOptions<TelegramOptions> options,
        ILogger<TelegramUpdateHandler> logger)
    {
        _telegramClient = telegramClient;
        _naturalLanguageService = naturalLanguageService;
        _mealLogService = mealLogService;
        _reminderService = reminderService;
        _cafeteriaService = cafeteriaService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> HandleUpdateAsync(TelegramUpdateModel update, CancellationToken cancellationToken = default)
    {
        if (update is null) return Result.Ok();

        // 1. Resolve incoming chat id
        var chatId = update.Message?.Chat?.Id
            ?? update.CallbackQuery?.Message?.Chat?.Id
            ?? update.CallbackQuery?.From?.Id
            ?? 0;

        if (chatId == 0) return Result.Ok();

        // 2. Strict authorization: only configured ChatId is served, others ignored silently
        if (_options.ChatId != 0 && chatId != _options.ChatId)
        {
            _logger.LogInformation("Ignoring update from unauthorized Telegram chat id: {ChatId}", chatId);
            return Result.Ok();
        }

        // 3. Handle Callback Query
        if (update.CallbackQuery != null)
        {
            return await HandleCallbackQueryAsync(update.CallbackQuery, chatId, cancellationToken);
        }

        // 4. Handle Message
        if (update.Message != null)
        {
            return await HandleMessageAsync(update.Message, chatId, cancellationToken);
        }

        return Result.Ok();
    }

    private async Task<Result> HandleCallbackQueryAsync(
        TelegramCallbackQueryModel callbackQuery,
        long chatId,
        CancellationToken cancellationToken)
    {
        var data = callbackQuery.Data ?? string.Empty;

        await _telegramClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Processando...", cancellationToken);

        if (data.StartsWith("plate:", StringComparison.OrdinalIgnoreCase))
        {
            return await _reminderService.ProcessPlateProfileCallbackAsync(chatId, data, cancellationToken);
        }

        if (data.StartsWith("undo:", StringComparison.OrdinalIgnoreCase) ||
            data.StartsWith("desfazer:", StringComparison.OrdinalIgnoreCase))
        {
            var uuidStr = data.Contains(':') ? data.Split(':')[1].Trim() : string.Empty;
            if (Guid.TryParse(uuidStr, out var mealUuid))
            {
                var deleteResult = await _mealLogService.DeleteMealLogAsync(mealUuid, cancellationToken);
                if (deleteResult.Success)
                {
                    var gapResult = await _mealLogService.GetDailyGapOverviewAsync(null, cancellationToken);
                    var newGap = gapResult.Data;

                    var responseText = $"🗑️ Refeição desfeita com sucesso!\n\n" +
                                       $"🎯 Gap atualizado de hoje:\n" +
                                       $"• Calorias: {newGap?.CalorieGap ?? 0:0.#} kcal\n" +
                                       $"• Proteína: {newGap?.ProteinGap ?? 0:0.#} g";

                    await _telegramClient.SendMessageAsync(chatId, responseText, cancellationToken: cancellationToken);
                    return Result.Ok();
                }
            }

            await _telegramClient.SendMessageAsync(chatId, "Não foi possível desfazer a refeição informada.", cancellationToken: cancellationToken);
        }

        return Result.Ok();
    }

    private async Task<Result> HandleMessageAsync(
        TelegramMessageModel message,
        long chatId,
        CancellationToken cancellationToken)
    {
        var text = (message.Text ?? message.Caption ?? string.Empty).Trim();

        // Command: /start
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            var welcomeText = "👋 Olá! Eu sou o SmartBulk Bot.\n\n" +
                              "Envie o que você comeu (por exemplo: 'comi 3 ovos mexidos e 100g de aveia') para registrar sua refeição, ou envie o print/foto do cardápio do refeitório para extrair a semana inteira!\n\n" +
                              "Comandos úteis:\n" +
                              "• /status ou /gap - Ver o Gap nutricional restante de hoje\n" +
                              "• /desfazer - Desfazer a última refeição registrada";

            await _telegramClient.SendMessageAsync(chatId, welcomeText, cancellationToken: cancellationToken);
            return Result.Ok();
        }

        // Command: /gap or /status
        if (text.Equals("/gap", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("/status", StringComparison.OrdinalIgnoreCase))
        {
            var gapResult = await _mealLogService.GetDailyGapOverviewAsync(null, cancellationToken);
            if (!gapResult.Success || gapResult.Data is null)
            {
                await _telegramClient.SendMessageAsync(chatId, "Não foi possível calcular o Gap no momento.", cancellationToken: cancellationToken);
                return Result.Ok();
            }

            var currentGap = gapResult.Data;
            var gapMessage = $"🎯 Status Nutricional de Hoje:\n\n" +
                             $"• Meta do dia: {currentGap.CalorieTarget} kcal | {currentGap.ProteinTarget:0.#} g proteína\n" +
                             $"• Refeitório (Almoço + Janta): {currentGap.CafeteriaCalories:0.#} kcal | {currentGap.CafeteriaProtein:0.#} g proteína\n" +
                             $"• Consumido fora/em casa: {currentGap.ConsumedCalories:0.#} kcal | {currentGap.ConsumedProtein:0.#} g proteína\n\n" +
                             $"🔥 Gap Restante para fechar o dia:\n" +
                             $"• Calorias: {currentGap.CalorieGap:0.#} kcal\n" +
                             $"• Proteína: {currentGap.ProteinGap:0.#} g";

            await _telegramClient.SendMessageAsync(chatId, gapMessage, cancellationToken: cancellationToken);
            return Result.Ok();
        }

        // Command: /desfazer or /undo
        if (text.Equals("/desfazer", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("/undo", StringComparison.OrdinalIgnoreCase))
        {
            var deleteResult = await _mealLogService.DeleteLatestMealLogAsync(cancellationToken);
            if (!deleteResult.Success)
            {
                await _telegramClient.SendMessageAsync(chatId, "Nenhuma refeição encontrada para desfazer hoje.", cancellationToken: cancellationToken);
                return Result.Ok();
            }

            var gapResult = await _mealLogService.GetDailyGapOverviewAsync(null, cancellationToken);
            var newGap = gapResult.Data;
            var undoText = $"🗑️ Última refeição desfeita!\n\n" +
                           $"🎯 Status de Hoje:\n" +
                           $"• Meta do dia: {newGap?.CalorieTarget ?? 0:0.#} kcal | {newGap?.ProteinTarget ?? 0:0.#} g proteína\n" +
                           $"• Refeitório (Almoço + Janta): {newGap?.CafeteriaCalories ?? 0:0.#} kcal | {newGap?.CafeteriaProtein ?? 0:0.#} g proteína\n" +
                           $"• Consumido fora/em casa: {newGap?.ConsumedCalories ?? 0:0.#} kcal | {newGap?.ConsumedProtein ?? 0:0.#} g proteína\n\n" +
                           $"🔥 Gap Restante:\n" +
                           $"• Calorias: {newGap?.CalorieGap ?? 0:0.#} kcal\n" +
                           $"• Proteína: {newGap?.ProteinGap ?? 0:0.#} g";

            await _telegramClient.SendMessageAsync(chatId, undoText, cancellationToken: cancellationToken);
            return Result.Ok();
        }

        // Photo or natural language meal logging
        string? imageBase64 = null;
        string? imageMimeType = null;

        if (message.Photo != null && message.Photo.Count > 0)
        {
            var largestPhoto = message.Photo.OrderByDescending(p => p.FileSize ?? 0).First();
            var fileInfo = await _telegramClient.GetFileAsync(largestPhoto.FileId, cancellationToken);
            if (fileInfo.Success && !string.IsNullOrWhiteSpace(fileInfo.Data?.FilePath))
            {
                var downloadResult = await _telegramClient.DownloadFileAsBase64Async(fileInfo.Data.FilePath, cancellationToken);
                if (downloadResult.Success)
                {
                    imageBase64 = downloadResult.Data;
                    imageMimeType = "image/jpeg";
                }
            }

            var isExplicitMealLog = text.StartsWith("comi", StringComparison.OrdinalIgnoreCase) ||
                                    text.StartsWith("almocei", StringComparison.OrdinalIgnoreCase) ||
                                    text.StartsWith("jantei", StringComparison.OrdinalIgnoreCase) ||
                                    text.StartsWith("meu almoço", StringComparison.OrdinalIgnoreCase) ||
                                    text.StartsWith("meu jantar", StringComparison.OrdinalIgnoreCase) ||
                                    text.StartsWith("prato:", StringComparison.OrdinalIgnoreCase);

            if (!isExplicitMealLog && !string.IsNullOrWhiteSpace(imageBase64))
            {
                await _telegramClient.SendMessageAsync(chatId, "⏳ Foto recebida! Processando cardápio semanal do refeitório (5 dias úteis)...", cancellationToken: cancellationToken);

                var ingestModel = new CafeteriaMenuIngestModel
                {
                    ImageBase64 = imageBase64,
                    ImageMimeType = imageMimeType,
                    Text = text
                };

                var ingestResult = await _cafeteriaService.IngestMenuAsync(ingestModel, cancellationToken);
                if (!ingestResult.Success || ingestResult.Data is null)
                {
                    var err = ingestResult.Errors.FirstOrDefault() ?? "Não foi possível extrair o cardápio da foto.";
                    await _telegramClient.SendMessageAsync(chatId, $"⚠️ Falha na ingestão do cardápio: {err}", cancellationToken: cancellationToken);
                    return Result.Ok();
                }

                var menu = ingestResult.Data;
                var distinctDates = menu.Meals.Select(m => m.Date).Distinct().OrderBy(d => d).ToList();
                var daysText = string.Join(", ", distinctDates.Select(d => d.ToString("dd/MM (ddd)")));

                var cafeteriaConfirmation = $"📋 *Cardápio semanal extraído com sucesso!*\n\n" +
                                            $"🗓️ *Semana de {menu.WeekStartDate:dd/MM/yyyy}*\n" +
                                            $"• Dias identificados ({distinctDates.Count}): {daysText}\n" +
                                            $"• Total de refeições: {menu.Meals.Count} (Almoço 1.0x / Jantar padrão 0.75x)\n\n" +
                                            $"💡 _Acesse a tela Cardápio no app para revisar ou confirmar a semana inteira!_";

                await _telegramClient.SendMessageAsync(chatId, cafeteriaConfirmation, parseMode: "Markdown", cancellationToken: cancellationToken);
                return Result.Ok();
            }
        }

        var request = new NaturalLanguageMealLogRequestModel
        {
            Text = text,
            ImageBase64 = imageBase64,
            ImageMimeType = imageMimeType
        };

        var nlResult = await _naturalLanguageService.ProcessNaturalLanguageMealAsync(request, cancellationToken);
        if (!nlResult.Success)
        {
            var errorMessage = nlResult.Errors.FirstOrDefault() ?? "Não foi possível processar a refeição.";
            await _telegramClient.SendMessageAsync(chatId, $"⚠️ {errorMessage}", cancellationToken: cancellationToken);
            return Result.Ok();
        }

        var responseData = nlResult.Data!;

        // Check if items were unmatched
        if (responseData.HasUnmatchedItems)
        {
            var question = responseData.Question ?? "Alguns alimentos não foram reconhecidos no catálogo.";
            await _telegramClient.SendMessageAsync(chatId, $"❓ {question}", cancellationToken: cancellationToken);
            return Result.Ok();
        }

        // Successfully registered
        var updatedGap = responseData.NewDailyGap;
        var successMessage = $"✅ Refeição registrada: *{responseData.MealName}*\n" +
                             $"{responseData.Summary}\n\n" +
                             $"📊 Nutrientes desta refeição:\n" +
                             $"• Calorias: {responseData.TotalCalories:0.#} kcal\n" +
                             $"• Proteína: {responseData.TotalProtein:0.#} g\n" +
                             $"• Carboidratos: {responseData.TotalCarbohydrates:0.#} g\n" +
                             $"• Gordura: {responseData.TotalFat:0.#} g\n\n" +
                             $"🎯 Status de Hoje:\n" +
                             $"• Meta do dia: {updatedGap?.CalorieTarget ?? 0:0.#} kcal | {updatedGap?.ProteinTarget ?? 0:0.#} g proteína\n" +
                             $"• Refeitório (Almoço + Janta): {updatedGap?.CafeteriaCalories ?? 0:0.#} kcal | {updatedGap?.CafeteriaProtein ?? 0:0.#} g proteína\n" +
                             $"• Consumido fora/em casa: {updatedGap?.ConsumedCalories ?? 0:0.#} kcal | {updatedGap?.ConsumedProtein ?? 0:0.#} g proteína\n\n" +
                             $"🔥 Gap Restante:\n" +
                             $"• Calorias: {updatedGap?.CalorieGap ?? 0:0.#} kcal\n" +
                             $"• Proteína: {updatedGap?.ProteinGap ?? 0:0.#} g";

        TelegramInlineKeyboardMarkupModel? replyMarkup = null;
        if (responseData.MealLogUuid.HasValue)
        {
            replyMarkup = TelegramInlineKeyboardMarkupModel.SingleButton("Desfazer ↩️", $"undo:{responseData.MealLogUuid.Value}");
        }

        await _telegramClient.SendMessageAsync(chatId, successMessage, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
        return Result.Ok();
    }
}

