using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class CafeteriaDayOverrideUpsertModelValidator : AbstractValidator<CafeteriaDayOverrideUpsertModel>
{
    public CafeteriaDayOverrideUpsertModelValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("A data do refeitório é obrigatória.");
    }
}
