using B2B.Contracts;
using FluentValidation;

namespace B2B.Api.Validation;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.DealerPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MsrpPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);

        RuleForEach(x => x.Images).ChildRules(img =>
        {
            img.RuleFor(i => i.Url).NotEmpty().MaximumLength(2048);
            img.RuleFor(i => i.SortOrder).GreaterThanOrEqualTo(0);
        });

        RuleForEach(x => x.Specs).ChildRules(spec =>
        {
            spec.RuleFor(s => s.Key).NotEmpty().MaximumLength(100);
            spec.RuleFor(s => s.Value).NotEmpty().MaximumLength(500);
            spec.RuleFor(s => s.SortOrder).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.DealerPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MsrpPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);

        RuleForEach(x => x.Images).ChildRules(img =>
        {
            img.RuleFor(i => i.Url).NotEmpty().MaximumLength(2048);
            img.RuleFor(i => i.SortOrder).GreaterThanOrEqualTo(0);
        });

        RuleForEach(x => x.Specs).ChildRules(spec =>
        {
            spec.RuleFor(s => s.Key).NotEmpty().MaximumLength(100);
            spec.RuleFor(s => s.Value).NotEmpty().MaximumLength(500);
            spec.RuleFor(s => s.SortOrder).GreaterThanOrEqualTo(0);
        });
    }
}

