using FluentValidation;
using SmartBulk.Application.Models;
using SmartBulk.Domain.Constants;

namespace SmartBulk.Application.Validations;

public class MealTemplateCreateModelValidator : AbstractValidator<MealTemplateCreateModel>
{
    public MealTemplateCreateModelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do template é obrigatório.")
            .MaximumLength(100).WithMessage("O nome do template deve ter no máximo 100 caracteres.");

        RuleFor(x => x.MealType)
            .NotEmpty().WithMessage("O tipo de refeição é obrigatório.")
            .Must(MealTemplateTypes.IsValid).WithMessage("Tipo de refeição inválido. Tipos suportados: Breakfast, Shake, FullMeal.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("O template deve conter pelo menos um alimento.");

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

