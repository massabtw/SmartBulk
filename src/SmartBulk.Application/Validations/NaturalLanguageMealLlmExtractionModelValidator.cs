using FluentValidation;
using SmartBulk.Domain.Http.Llm.Models;

namespace SmartBulk.Application.Validations;

public class NaturalLanguageMealLlmExtractionModelValidator : AbstractValidator<NaturalLanguageMealLlmExtractionModel>
{
    public NaturalLanguageMealLlmExtractionModelValidator()
    {
        RuleFor(x => x.MealName)
            .NotEmpty().WithMessage("O nome da refeição extraída é obrigatório.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("A refeição deve conter pelo menos um alimento extraído.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Name)
                .NotEmpty().WithMessage("O nome do alimento extraído não pode ser vazio.");

            item.RuleFor(i => i.Grams)
                .GreaterThan(0).WithMessage("A porção em gramas deve ser maior que zero.")
                .LessThanOrEqualTo(5000).WithMessage("A porção em gramas deve ser no máximo 5000g.");
        });
    }
}

