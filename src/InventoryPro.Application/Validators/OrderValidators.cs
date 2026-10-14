using FluentValidation;
using InventoryPro.Application.DTOs;
using InventoryPro.Domain.Entities;

namespace InventoryPro.Application.Validators;

public class CreateOrderValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderValidator()
    {
        RuleFor(o => o.CustomerId).GreaterThan(0);
        RuleFor(o => o.Items).NotEmpty().WithMessage("An order must contain at least one item.")
            .Must(items => items is null || items.Count <= 100)
            .WithMessage("An order cannot contain more than 100 line items.");
        RuleForEach(o => o.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 1_000_000);
        });
    }
}

public class UpdateOrderStatusValidator : AbstractValidator<UpdateOrderStatusDto>
{
    public UpdateOrderStatusValidator()
    {
        RuleFor(o => o.Status).IsInEnum();
        RuleFor(o => o.Status).Must(status => status != OrderStatus.Pending)
            .WithMessage("An order cannot be moved back to Pending.");
    }
}
