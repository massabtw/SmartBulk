using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class FoodModelValidator : AbstractValidator<FoodModel>
{
    public FoodModelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("O nome do alimento é obrigatório.")
            .MaximumLength(200)
            .WithMessage("O nome do alimento deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Calories)
            .GreaterThanOrEqualTo(0)
            .WithMessage("As calorias não podem ser negativas.");

        RuleFor(x => x.Protein)
            .GreaterThanOrEqualTo(0)
            .WithMessage("A quantidade de proteína não pode ser negativa.");

        RuleFor(x => x.Carbohydrates)
            .GreaterThanOrEqualTo(0)
            .WithMessage("A quantidade de carboidratos não pode ser negativa.");

        RuleFor(x => x.Fat)
            .GreaterThanOrEqualTo(0)
            .WithMessage("A quantidade de gordura não pode ser negativa.");

        When(x => x.RawYieldFactor.HasValue, () =>
        {
            RuleFor(x => x.RawYieldFactor)
                .GreaterThan(0)
                .WithMessage("O fator de conversão cru para pronto deve ser maior que zero.");
        });
    }
}
