using ECommerce.Payment.Application;
namespace ECommerce.Payment.Api.Common;
public sealed class PaymentRecoveryWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<PaymentRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("PaymentRecovery:Enabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(config.GetValue("PaymentRecovery:IntervalSeconds", 30), 5, 3600)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                IReadOnlyList<Guid> ids;
                using (var scope = scopes.CreateScope()) ids = await scope.ServiceProvider.GetRequiredService<IPaymentStore>().GetRecoveryIdsAsync(50, stoppingToken);
                foreach (var id in ids)
                {
                    try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<PaymentService>().RecoverAsync(id, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception error) { logger.LogWarning(error, "Payment {PaymentId} remains pending recovery.", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "Payment recovery pass could not finish."); }
        }
    }
}
