using FluentValidation;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Constants;

namespace SmartBulk.Application.Validations;

public class HomeMealPlanUpsertModelValidator : AbstractValidator<HomeMealPlanUpsertModel>
{
    public HomeMealPlanUpsertModelValidator()
    {
        RuleFor(x => x.WeekStartDate)
            .NotEmpty().WithMessage("A data de início da semana é obrigatória.")
            .Must(d => d.DayOfWeek == DayOfWeek.Monday)
            .WithMessage("A data de início da semana deve ser uma segunda-feira.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Date)
                .NotEmpty().WithMessage("A data do item é obrigatória.");

            item.RuleFor(i => i.MealSlot)
                .NotEmpty().WithMessage("O slot de refeição é obrigatório.")
                .Must(HomeMealSlots.IsValid)
                .WithMessage("Slot de refeição inválido. Valores aceitos: Breakfast, Lunch, Dinner, Shake.");

            item.RuleFor(i => i.MealTemplateUuid)
                .NotEmpty().WithMessage("O template de refeição é obrigatório.");
        });

        RuleFor(x => x)
            .Must(HaveItemsWithinWeek)
            .WithMessage("Todas as refeições devem pertencer à semana informada (segunda a domingo).")
            .Must(NotHaveDuplicateSlotsOnSameDate)
            .WithMessage("Não é permitido repetir a mesma refeição (slot) na mesma data.");
    }

    private static bool HaveItemsWithinWeek(HomeMealPlanUpsertModel model)
    {
        if (model.Items == null || model.Items.Count == 0) return true;
        var endOfWeek = model.WeekStartDate.AddDays(6);
        return model.Items.All(i => i.Date >= model.WeekStartDate && i.Date <= endOfWeek);
    }

    private static bool NotHaveDuplicateSlotsOnSameDate(HomeMealPlanUpsertModel model)
    {
        if (model.Items == null || model.Items.Count == 0) return true;
        var groups = model.Items
            .GroupBy(i => new { i.Date, Slot = HomeMealSlots.Normalize(i.MealSlot) });
        return groups.All(g => g.Count() == 1);
    }
}

