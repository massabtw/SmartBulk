using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class WeightLogModelValidator : AbstractValidator<WeightLogModel>
{
    public WeightLogModelValidator()
    {
        RuleFor(x => x.WeightKg)
            .InclusiveBetween(30.0m, 350.0m)
            .WithMessage("O peso deve estar em uma faixa plausível (entre 30 kg e 350 kg).");

        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("A data do registro de peso é obrigatória.");
    }
}
