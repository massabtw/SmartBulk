using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class CafeteriaMealPlateProfileByDateUpdateModelValidator : AbstractValidator<CafeteriaMealPlateProfileByDateUpdateModel>
{
    public CafeteriaMealPlateProfileByDateUpdateModelValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("A data da refeição é obrigatória.");

        RuleFor(x => x.MealType)
            .NotEmpty()
            .WithMessage("O tipo da refeição é obrigatório.");

        RuleFor(x => x.PlateProfile)
            .NotEmpty()
            .WithMessage("O perfil do prato é obrigatório.");
    }
}

