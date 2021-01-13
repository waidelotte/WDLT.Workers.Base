using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WDLT.Workers.Base
{
    public abstract class BaseBackgroundService : BackgroundService
    {
        protected ILogger Logger { get; set; }
        protected DateTimeOffset ExecuteTimestamp { get; private set; }
        protected TimeSpan StartAfter { get; set; }
        protected TimeSpan Timeout { get; set; }

        private CancellationTokenSource _cts;

        protected BaseBackgroundService()
        {
            Logger?.LogDebug("Init Background Service");

            StartAfter = TimeSpan.FromSeconds(5);
            Timeout = TimeSpan.FromSeconds(5);
        }

        protected virtual bool CanExecute()
        {
            return true;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => Logger?.LogInformation("Stopping Background Service"));
            var stopWatch = new System.Diagnostics.Stopwatch();

            await Task.Delay(StartAfter, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!CanExecute())
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                stopWatch.Start();
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                Logger?.LogDebug("Background Service starting work.");

                try
                {
                    ExecuteTimestamp = DateTimeOffset.Now;
                    await DoWorkAsync(_cts.Token);
                }
                catch (Exception e)
                {
                    Logger?.LogError(e, "Error while execute Background Service.");
                }

                stopWatch.Stop();

                var timeout = TimeSpan.FromMilliseconds(Math.Max(Timeout.TotalMilliseconds - stopWatch.ElapsedMilliseconds, 1000));

                Logger?.LogDebug($"Background Service end of work [{stopWatch.Elapsed.TotalSeconds:F2}s.]. Next execute after {timeout.Seconds}s.");

                stopWatch.Reset();

                await Task.Delay(timeout, stoppingToken);
            }

            Logger?.LogDebug("Stopping Background Service");
        }

        protected abstract Task DoWorkAsync(CancellationToken ct);
    }
}