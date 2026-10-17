using ECommerce.Order.Application;

namespace ECommerce.Order.Api.Common;

public sealed class OrderRecoveryWorker(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<OrderRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("OrderRecovery:Enabled", true))
        {
            return;
        }

        var interval = Math.Clamp(configuration.GetValue("OrderRecovery:IntervalSeconds", 30), 5, 3600);

        var holdMinutes = Math.Clamp(configuration.GetValue("OrderRecovery:HoldMinutes", 30), 1, 10080);

        var batchSize = Math.Clamp(configuration.GetValue("OrderRecovery:BatchSize", 50), 1, 100);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-holdMinutes);

                IReadOnlyList<Guid> ids;

                using (var scope = scopes.CreateScope())
                {
                    ids = await scope.ServiceProvider.GetRequiredService<IOrderService>().GetRecoveryIdsAsync(
                        cutoff,
                        batchSize,
                        stoppingToken);

                }

                foreach (var id in ids)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();

                        await scope.ServiceProvider.GetRequiredService<IOrderService>().RecoverAsync(
                            id,
                            cutoff,
                            stoppingToken);

                    }

                    catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }

                    catch (Exception exception)
                    {
                        logger.LogWarning(exception, "Order {OrderId} remains pending recovery.", id);

                    }

                }

            }

            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            catch (Exception exception)
            {
                logger.LogWarning(exception, "Order recovery pass could not finish.");

            }

        }

    }

}
