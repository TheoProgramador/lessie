using Lessie.Application.Payments;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Error;
using MercadoPago.Webhook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MercadoPaymentStatus = MercadoPago.Resource.Payment.PaymentStatus;

namespace Lessie.Infrastructure.Payments;

public sealed class MercadoPagoPaymentService(
    LessieDbContext dbContext,
    IOptions<MercadoPagoOptions> mercadoPagoOptions) : IPaymentService
{
    public async Task<IReadOnlyList<CreditPlanDto>> GetActiveCreditPlansAsync(CancellationToken cancellationToken)
        => await dbContext.CreditPlans
            .AsNoTracking()
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.SortOrder)
            .Select(plan => new CreditPlanDto(
                plan.Code,
                plan.Name,
                plan.Description,
                plan.Credits,
                plan.Price,
                plan.CurrencyId,
                plan.Badge))
            .ToListAsync(cancellationToken);

    public async Task<CreatePaymentPreferenceResponse> CreatePreferenceAsync(
        Guid userId,
        CreatePaymentPreferenceRequest request,
        string returnBaseUrl,
        string notificationBaseUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(MercadoPagoConfig.AccessToken))
        {
            throw new InvalidOperationException("Configure MercadoPago:AccessToken antes de criar pagamentos.");
        }

        var planCode = NormalizeCode(request.PlanCode);
        var plan = await dbContext.CreditPlans
            .FirstOrDefaultAsync(item => item.Code == planCode && item.IsActive, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException("Pacote de creditos nao encontrado ou inativo.");
        }

        var promotion = await FindActivePromotionAsync(plan.Id, request.PromotionCode, cancellationToken);
        var discountAmount = CalculateDiscount(plan.Price, promotion);
        var finalAmount = decimal.Round(plan.Price - discountAmount, 2, MidpointRounding.AwayFromZero);
        if (finalAmount <= 0)
        {
            throw new InvalidOperationException("Promocao resultou em valor invalido para checkout.");
        }

        var order = new PaymentOrder
        {
            UserId = userId,
            CreditPlanId = plan.Id,
            CreditPromotionId = promotion?.Id,
            OriginalAmount = plan.Price,
            DiscountAmount = discountAmount,
            FinalAmount = finalAmount,
            CurrencyId = plan.CurrencyId,
            Credits = plan.Credits,
            BonusCredits = promotion?.BonusCredits ?? 0,
            PromotionCode = promotion?.Code ?? string.Empty,
            ExternalReference = $"lessie:{Guid.NewGuid():N}"
        };

        var notificationUrl = BuildNotificationUrl(mercadoPagoOptions.Value.NotificationUrl, notificationBaseUrl);

        var preference = await CreateMercadoPagoPreferenceAsync(plan, promotion, order, returnBaseUrl, notificationUrl, cancellationToken);

        order.PreferenceId = preference.Id ?? string.Empty;
        order.InitPoint = preference.InitPoint ?? string.Empty;
        order.SandboxInitPoint = preference.SandboxInitPoint ?? string.Empty;

        dbContext.PaymentOrders.Add(order);
        if (promotion is not null)
        {
            promotion.RedemptionCount += 1;
            promotion.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var checkoutUrl = ResolveCheckoutUrl(order, mercadoPagoOptions.Value.AccessToken);

        return new CreatePaymentPreferenceResponse(
            order.Id,
            order.PreferenceId,
            checkoutUrl,
            order.InitPoint,
            order.SandboxInitPoint,
            mercadoPagoOptions.Value.PublicKey,
            order.OriginalAmount,
            order.DiscountAmount,
            order.FinalAmount,
            order.Credits,
            order.BonusCredits,
            order.CurrencyId);
    }

    public async Task<PaymentNotificationProcessResponse> ProcessMercadoPagoPaymentNotificationAsync(
        MercadoPagoPaymentNotificationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateWebhookSignature(request);

        if (!long.TryParse(request.PaymentId, out var paymentId))
        {
            throw new InvalidOperationException("Identificador de pagamento invalido.");
        }

        var client = new PaymentClient();
        var payment = await client.GetAsync(paymentId, cancellationToken: cancellationToken);
        var status = payment.Status ?? string.Empty;
        var statusDetail = payment.StatusDetail ?? string.Empty;
        var externalReference = payment.ExternalReference ?? string.Empty;

        if (string.IsNullOrWhiteSpace(externalReference))
        {
            throw new InvalidOperationException("Pagamento sem referencia externa para conciliacao.");
        }

        var order = await dbContext.PaymentOrders
            .Include(item => item.User)
                .ThenInclude(user => user!.Subscription)
            .FirstOrDefaultAsync(item => item.ExternalReference == externalReference, cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException("Pedido de pagamento nao encontrado para esta notificacao.");
        }

        var wasAlreadyApproved = string.Equals(order.Status, MercadoPaymentStatus.Approved, StringComparison.OrdinalIgnoreCase);
        order.MercadoPagoPaymentId = request.PaymentId;
        order.Status = status;
        order.StatusDetail = statusDetail;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        var creditsGranted = false;
        if (!wasAlreadyApproved && string.Equals(status, MercadoPaymentStatus.Approved, StringComparison.OrdinalIgnoreCase))
        {
            GrantCredits(order);
            creditsGranted = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PaymentNotificationProcessResponse(
            order.Id,
            request.PaymentId,
            order.Status,
            order.StatusDetail,
            creditsGranted,
            order.Credits,
            order.BonusCredits);
    }

    private async Task<CreditPromotion?> FindActivePromotionAsync(Guid planId, string? promotionCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promotionCode))
        {
            return null;
        }

        var code = NormalizeCode(promotionCode);
        var now = DateTimeOffset.UtcNow;

        return await dbContext.CreditPromotions
            .Where(promotion =>
                promotion.Code == code
                && promotion.IsActive
                && (promotion.CreditPlanId == null || promotion.CreditPlanId == planId)
                && (promotion.StartsAt == null || promotion.StartsAt <= now)
                && (promotion.EndsAt == null || promotion.EndsAt >= now)
                && (promotion.MaxRedemptions == null || promotion.RedemptionCount < promotion.MaxRedemptions))
            .OrderByDescending(promotion => promotion.CreditPlanId == planId)
            .ThenByDescending(promotion => promotion.DiscountPercent ?? 0)
            .ThenByDescending(promotion => promotion.DiscountAmount ?? 0)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static decimal CalculateDiscount(decimal price, CreditPromotion? promotion)
    {
        if (promotion is null)
        {
            return 0m;
        }

        var discount = 0m;
        if (promotion.DiscountPercent is > 0)
        {
            discount += price * promotion.DiscountPercent.Value / 100m;
        }

        if (promotion.DiscountAmount is > 0)
        {
            discount += promotion.DiscountAmount.Value;
        }

        return decimal.Round(Math.Min(discount, price), 2, MidpointRounding.AwayFromZero);
    }

    private static async Task<MercadoPago.Resource.Preference.Preference> CreateMercadoPagoPreferenceAsync(
        CreditPlan plan,
        CreditPromotion? promotion,
        PaymentOrder order,
        string returnBaseUrl,
        string? notificationUrl,
        CancellationToken cancellationToken)
    {
        var title = $"Creditos Lessie - {plan.Name}";
        var request = new PreferenceRequest
        {
            Items =
            [
                new PreferenceItemRequest
                {
                    Id = plan.Code,
                    Title = title,
                    Description = promotion is null
                        ? plan.Description
                        : $"{plan.Description} Promocao: {promotion.Name}.",
                    Quantity = 1,
                    CurrencyId = plan.CurrencyId,
                    UnitPrice = order.FinalAmount
                }
            ],
            ExternalReference = order.ExternalReference,
            StatementDescriptor = "LESSIE",
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = $"{returnBaseUrl}/credits?payment=success",
                Pending = $"{returnBaseUrl}/credits?payment=pending",
                Failure = $"{returnBaseUrl}/credits?payment=failure"
            }
        };

        if (returnBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            request.AutoReturn = "approved";
        }

        if (!string.IsNullOrWhiteSpace(notificationUrl))
        {
            request.NotificationUrl = notificationUrl;
        }

        var client = new PreferenceClient();
        return await client.CreateAsync(request, cancellationToken: cancellationToken);
    }

    private static string? BuildNotificationUrl(string configuredNotificationUrl, string notificationBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredNotificationUrl))
        {
            return configuredNotificationUrl.Trim();
        }

        if (notificationBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return $"{notificationBaseUrl.TrimEnd('/')}/api/payments/mercado-pago/webhook";
        }

        return null;
    }

    private static string ResolveCheckoutUrl(PaymentOrder order, string configuredAccessToken)
    {
        var shouldUseSandbox = configuredAccessToken.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);
        if (shouldUseSandbox && !string.IsNullOrWhiteSpace(order.SandboxInitPoint))
        {
            return order.SandboxInitPoint;
        }

        return string.IsNullOrWhiteSpace(order.InitPoint) ? order.SandboxInitPoint : order.InitPoint;
    }

    private void ValidateWebhookSignature(MercadoPagoPaymentNotificationRequest request)
    {
        var secret = mercadoPagoOptions.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        try
        {
            WebhookSignatureValidator.Validate(
                request.XSignature,
                request.XRequestId,
                request.PaymentId,
                secret,
                TimeSpan.FromMinutes(10),
                null);
        }
        catch (InvalidWebhookSignatureException exception)
        {
            throw new UnauthorizedAccessException("Assinatura do webhook Mercado Pago invalida.", exception);
        }
    }

    private static void GrantCredits(PaymentOrder order)
    {
        var now = DateTimeOffset.UtcNow;
        if (order.User is null)
        {
            throw new InvalidOperationException("Usuario do pedido nao encontrado.");
        }

        order.User.Subscription ??= new UserSubscription
        {
            UserId = order.UserId,
            CreatedAt = now
        };

        var subscription = order.User.Subscription;
        var credits = order.Credits + order.BonusCredits;

        subscription.IsPaid = true;
        subscription.PaidUntil = subscription.PaidUntil is null || subscription.PaidUntil < now.AddYears(1)
            ? now.AddYears(1)
            : subscription.PaidUntil;
        subscription.LastPaymentAt = now;
        subscription.PaymentProvider = order.Provider;
        subscription.ExternalReference = order.ExternalReference;
        subscription.Notes = $"Creditos comprados via Mercado Pago. Pedido: {order.Id}.";
        subscription.CreditBalance += credits;
        subscription.TotalCreditsPurchased += credits;
        subscription.ResumeAnalysisLimit += credits;
        subscription.ChatConversationLimit += credits;
        subscription.InterviewAnalysisLimit += Math.Max(1, credits / 10);
        subscription.UpdatedAt = now;

        order.PaidAt = now;
    }

    private static string NormalizeCode(string value) => value.Trim().ToLowerInvariant();
}
