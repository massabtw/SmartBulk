using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class CafeteriaMenuIngestModelValidator : AbstractValidator<CafeteriaMenuIngestModel>
{
    public CafeteriaMenuIngestModelValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Text) || !string.IsNullOrWhiteSpace(x.ImageBase64))
            .WithMessage("É necessário fornecer o texto ou a imagem do cardápio.");
    }
}
