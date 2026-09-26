using FluentValidation;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Validators;

public class CreateOrderValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderValidator()
    {
        RuleFor(o => o.CustomerId).GreaterThan(0);
        RuleFor(o => o.Items).NotEmpty().WithMessage("An order must contain at least one item.");
        RuleForEach(o => o.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
