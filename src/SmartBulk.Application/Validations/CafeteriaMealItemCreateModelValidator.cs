using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class CafeteriaMealItemCreateModelValidator : AbstractValidator<CafeteriaMealItemCreateModel>
{
    public CafeteriaMealItemCreateModelValidator()
    {
        RuleFor(x => x.RawName)
            .NotEmpty()
            .WithMessage("O nome do componente é obrigatório.");

        RuleFor(x => x.ServingGrams)
            .GreaterThan(0)
            .WithMessage("A porção em gramas deve ser maior que zero.");
    }
}

