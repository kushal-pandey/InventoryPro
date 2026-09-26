using FluentValidation;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Validators;

public class CreateCategoryValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
    }
}
