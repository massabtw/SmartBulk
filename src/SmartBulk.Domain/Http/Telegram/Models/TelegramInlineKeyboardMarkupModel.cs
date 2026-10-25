using System.Text.Json.Serialization;

namespace SmartBulk.Domain.Http.Telegram.Models;

public class TelegramInlineKeyboardMarkupModel
{
    [JsonPropertyName("inline_keyboard")]
    public List<List<TelegramInlineKeyboardButtonModel>> InlineKeyboard { get; set; } = new();

    public static TelegramInlineKeyboardMarkupModel SingleButton(string text, string callbackData)
    {
        return new TelegramInlineKeyboardMarkupModel
        {
            InlineKeyboard = new List<List<TelegramInlineKeyboardButtonModel>>
            {
                new()
                {
                    new TelegramInlineKeyboardButtonModel
                    {
                        Text = text,
                        CallbackData = callbackData
                    }
                }
            }
        };
    }
}

