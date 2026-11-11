using FluentValidation;
using InventoryPro.Application.Common;

namespace InventoryPro.Application.Validators;

public class ReorderQueryValidator : AbstractValidator<ReorderQuery>
{
    public ReorderQueryValidator()
    {
        RuleFor(q => q.LookbackDays).InclusiveBetween(7, 180).When(q => q.LookbackDays.HasValue);
        RuleFor(q => q.LeadTimeDays).InclusiveBetween(0, 60).When(q => q.LeadTimeDays.HasValue);
        RuleFor(q => q.SafetyDays).InclusiveBetween(0, 60).When(q => q.SafetyDays.HasValue);
        RuleFor(q => q.Limit).InclusiveBetween(1, 200);
    }
}

public class SalesTrendQueryValidator : AbstractValidator<SalesTrendQuery>
{
    public SalesTrendQueryValidator()
    {
        RuleFor(query => query.Days).InclusiveBetween(1, 365);
    }
}
