namespace Lessie.Application.Payments;

public sealed record CreditPlanDto(
    string Code,
    string Name,
    string Description,
    int Credits,
    decimal Price,
    string CurrencyId,
    string Badge);
