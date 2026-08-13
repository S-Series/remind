using System;
using System.Threading;
using System.Threading.Tasks;
using REmind.Data;
using REmind.Gameplay.Chart.Loading.Providers;

namespace REmind.Gameplay.Chart.Loading
{
    /// <summary>
    /// 채보 공급 위치와 JSON 검증을 조합합니다. 공급자만 교체해 로컬 캐시와 원격 API를 공유합니다.
    /// </summary>
    public sealed class ChartLoadService
    {
        private readonly IChartContentProvider contentProvider;

        public ChartLoadService(IChartContentProvider contentProvider)
        {
            this.contentProvider = contentProvider ??
                throw new ArgumentNullException(nameof(contentProvider));
        }

        public async Task<ChartData> LoadAsync(
            string chartId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(chartId))
            {
                throw new ArgumentException(
                    "A chart ID is required.",
                    nameof(chartId));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ChartContent content = await contentProvider.GetChartContentAsync(
                chartId,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string sourceName = string.IsNullOrWhiteSpace(content.SourceName)
                ? chartId
                : content.SourceName;
            ChartData chart = ChartLoader.Parse(content.Json, sourceName);

            if (!string.Equals(
                    chart.ChartId,
                    chartId,
                    StringComparison.Ordinal))
            {
                throw new ChartLoadException(
                    $"{sourceName}: Requested chart '{chartId}' but received " +
                    $"'{chart.ChartId}'.");
            }

            return chart;
        }
    }
}
