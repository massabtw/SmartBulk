using System.Globalization;
using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class UserSettingsModelValidator : AbstractValidator<UserSettingsModel>
{
    private static readonly string[] AllowedTimeFormats = [@"hh\:mm", @"h\:mm", @"hh\:mm\:ss", @"h\:mm\:ss"];

    public UserSettingsModelValidator()
    {
        RuleFor(x => x.CalorieTargetKcal)
            .GreaterThan(0)
            .WithMessage("A meta calórica deve ser maior que zero.");

        RuleFor(x => x.ProteinTargetGPerKg)
            .GreaterThan(0)
            .WithMessage("A meta de proteína deve ser maior que zero.");

        RuleFor(x => x.TimeZoneId)
            .NotEmpty()
            .WithMessage("O fuso horário é obrigatório.")
            .Must(BeAValidTimeZone)
            .WithMessage("Fuso horário inválido.");

        RuleFor(x => x.LunchReminderTime)
            .NotEmpty()
            .WithMessage("O horário do lembrete de almoço é obrigatório.")
            .Must(BeAValidTime)
            .WithMessage("Horário de almoço inválido. Use o formato HH:mm.");

        RuleFor(x => x.DinnerReminderTime)
            .NotEmpty()
            .WithMessage("O horário do lembrete de jantar é obrigatório.")
            .Must(BeAValidTime)
            .WithMessage("Horário de jantar inválido. Use o formato HH:mm.");
    }

    private static bool BeAValidTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!TimeSpan.TryParseExact(value.Trim(), AllowedTimeFormats, CultureInfo.InvariantCulture, out var ts))
            return false;

        return ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1);
    }

    private static bool BeAValidTimeZone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.GetSystemTimeZones()
                .Any(z => z.Id.Equals(value, StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
