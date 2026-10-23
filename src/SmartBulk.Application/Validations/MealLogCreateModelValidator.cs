using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class MealLogCreateModelValidator : AbstractValidator<MealLogCreateModel>
{
    public MealLogCreateModelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da refeição é obrigatório.")
            .MaximumLength(100).WithMessage("O nome da refeição deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("A refeição deve conter pelo menos um alimento.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.FoodUuid)
                .NotEmpty().WithMessage("O alimento deve ser informado.");

            item.RuleFor(i => i.ServingGrams)
                .GreaterThan(0).WithMessage("A porção em gramas deve ser maior que zero.")
                .LessThanOrEqualTo(5000).WithMessage("A porção em gramas deve ser no máximo 5000g.");
        });
    }
}
