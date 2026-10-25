using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class CafeteriaMealItemUpdateModelValidator : AbstractValidator<CafeteriaMealItemUpdateModel>
{
    public CafeteriaMealItemUpdateModelValidator()
    {
        RuleFor(x => x.ServingGrams)
            .GreaterThan(0)
            .WithMessage("A porção em gramas deve ser maior que zero.");
    }
}

