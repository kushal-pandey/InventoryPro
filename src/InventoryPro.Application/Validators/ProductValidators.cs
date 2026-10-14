using FluentValidation;
using InventoryPro.Application.DTOs;

namespace InventoryPro.Application.Validators;

public class CreateProductValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductValidator()
    {
        RuleFor(p => p.Name).NotEmpty().MaximumLength(150);
        RuleFor(p => p.Sku).NotEmpty().MaximumLength(50);
        RuleFor(p => p.Price).GreaterThan(0);
        RuleFor(p => p.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(p => p.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(p => p.CategoryId).GreaterThan(0);
    }
}

public class UpdateProductValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductValidator()
    {
        RuleFor(p => p.Name).NotEmpty().MaximumLength(150);
        RuleFor(p => p.Price).GreaterThan(0);
        RuleFor(p => p.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(p => p.CategoryId).GreaterThan(0);
    }
}

public class AdjustStockValidator : AbstractValidator<AdjustStockDto>
{
    public AdjustStockValidator()
    {
        RuleFor(p => p.QuantityChange).NotEqual(0).WithMessage("Quantity change cannot be zero.");
        RuleFor(p => p.QuantityChange).InclusiveBetween(-1_000_000, 1_000_000);
        RuleFor(p => p.Reason).NotEmpty().MaximumLength(200);
    }
}
