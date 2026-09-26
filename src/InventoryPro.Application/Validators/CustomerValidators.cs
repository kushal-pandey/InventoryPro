using FluentValidation;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Validators;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Phone).MaximumLength(20);
    }
}
