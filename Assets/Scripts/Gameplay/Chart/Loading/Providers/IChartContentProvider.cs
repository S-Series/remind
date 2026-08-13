using System.Threading;
using System.Threading.Tasks;

namespace REmind.Gameplay.Chart.Loading.Providers
{
    /// <summary>로컬 캐시나 원격 API가 반환한 원본 채보 내용입니다.</summary>
    public readonly struct ChartContent
    {
        public string Json { get; }
        public string SourceName { get; }
        public string VersionTag { get; }

        public ChartContent(
            string json,
            string sourceName,
            string versionTag = null)
        {
            Json = json;
            SourceName = sourceName;
            VersionTag = versionTag;
        }
    }

    /// <summary>
    /// 채보의 저장 위치를 게임 로직에서 분리합니다. ChartMaker는 이 계약을 사용하지 않습니다.
    /// </summary>
    public interface IChartContentProvider
    {
        Task<ChartContent> GetChartContentAsync(
            string chartId,
            CancellationToken cancellationToken);
    }
}
