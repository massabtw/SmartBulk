using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class CafeteriaMealPlateProfileUpdateModelValidator : AbstractValidator<CafeteriaMealPlateProfileUpdateModel>
{
    public CafeteriaMealPlateProfileUpdateModelValidator()
    {
        RuleFor(x => x.PlateProfile)
            .NotEmpty().WithMessage("O perfil do prato é obrigatório.");
    }
}
