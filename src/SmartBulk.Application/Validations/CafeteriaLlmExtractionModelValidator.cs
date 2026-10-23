using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class CafeteriaLlmExtractionModelValidator : AbstractValidator<CafeteriaLlmExtractionModel>
{
    public CafeteriaLlmExtractionModelValidator()
    {
        RuleFor(x => x.Meals)
            .NotEmpty()
            .WithMessage("A extração deve conter pelo menos uma refeição.");

        RuleForEach(x => x.Meals).ChildRules(meal =>
        {
            meal.RuleFor(m => m.Date).NotEmpty().WithMessage("A data da refeição é obrigatória.");
            meal.RuleFor(m => m.MealType).NotEmpty().WithMessage("O tipo da refeição é obrigatório.");
            meal.RuleFor(m => m.Items).NotEmpty().WithMessage("A refeição deve conter componentes.");

            meal.RuleForEach(m => m.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.Name).NotEmpty().WithMessage("O nome do componente é obrigatório.");
                item.RuleFor(i => i.Grams).GreaterThan(0).WithMessage("A porção em gramas deve ser maior que zero.");
            });
        });
    }
}
