using FluentValidation;
using SmartBulk.Application.Models;

namespace SmartBulk.Application.Validations;

public class NaturalLanguageMealLogRequestModelValidator : AbstractValidator<NaturalLanguageMealLogRequestModel>
{
    public NaturalLanguageMealLogRequestModelValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Text) || !string.IsNullOrWhiteSpace(x.ImageBase64))
            .WithMessage("Informe um texto ou envie uma imagem da refeição.");

        When(x => !string.IsNullOrWhiteSpace(x.ImageBase64), () =>
        {
            RuleFor(x => x.ImageMimeType)
                .NotEmpty()
                .WithMessage("O tipo MIME da imagem é obrigatório quando a imagem é enviada.");
        });
    }
}

