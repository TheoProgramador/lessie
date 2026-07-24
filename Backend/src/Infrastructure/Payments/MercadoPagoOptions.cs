namespace Lessie.Infrastructure.Payments;

public sealed class MercadoPagoOptions
{
    public string PublicKey { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string NotificationUrl { get; set; } = string.Empty;
}
