using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using TMPro;

[DisallowMultipleComponent]
public sealed class GuideGenerate : MonoBehaviour
{
    public static GuideGenerate Instance { get; private set; }
    public static float ReferenceY { get; private set; }
    public static event Action<float> ReferenceYChanged;

    private const float IndexEpsilon = 0.001f;

    [Header("References")]
    [SerializeField] private GameObject guidePrefab;
    [SerializeField] private Transform guideField;

    [Header("Chart")]
    [SerializeField, Min(1)] private int measureCount =
        ChartHolder.MeasureCount;
    [SerializeField, Min(1)] private int guidesPerMeasure = 4;
    [SerializeField, Min(1f)] private float measureHeight =
        ChartHolder.WorldUnitsPerMeasure;
    [SerializeField, Min(1f)] private float sectionHeight =
        ChartHolder.WorldUnitsPerMeasure * 10f;

    [Header("Visible Range")]
    [SerializeField, Min(1f)] private float scrollMeasureHeight = 960f;
    [SerializeField, Min(0f)] private float visibleBefore = 40f;
    [SerializeField, Min(0f)] private float visibleAfter = 120f;

    [Header("Appearance")]
    [SerializeField] private Color guideColor = Color.white;
    [SerializeField] private Color measureGuideColor =
        new Color(0.35f, 0.8f, 1f, 1f);

    [Header("Pool")]
    [SerializeField, Min(1)] private int initialPoolSize = 64;
    [SerializeField, Min(1)] private int maxPoolSize = 128;

    private readonly Dictionary<int, GameObject> visibleGuides =
        new Dictionary<int, GameObject>();
    private readonly List<int> releaseIndices = new List<int>();

    private int currentFirstIndex = -1;
    private int currentLastIndex = -1;
    private bool guidesVisible = true;

    public ObjectPool<GameObject> Pool { get; private set; }
    public int TotalGuideCount => measureCount * guidesPerMeasure;
    public int VisibleGuideCount => visibleGuides.Count;
    public float MeasureHeight => measureHeight;
    public float GuideSpacing => measureHeight / guidesPerMeasure;
    public float ScrollToChartRatio => measureHeight / scrollMeasureHeight;
    public bool GuidesVisible => guidesVisible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        ReferenceY = 0f;
        ChartPlacementController.SetYGuideCount(guidesPerMeasure);

        if (!guidePrefab || !guideField)
        {
            Debug.LogError(
                "GuideGenerate requires a guide prefab and guide field.",
                this);
            enabled = false;
            return;
        }

        if (guidePrefab.transform.IsChildOf(guideField))
        {
            guidePrefab.SetActive(false);
        }

        maxPoolSize = Math.Max(initialPoolSize, maxPoolSize);
        Pool = new ObjectPool<GameObject>(
            CreateGuide,
            OnTakeFromPool,
            OnReturnedToPool,
            OnDestroyPooledGuide,
            true,
            initialPoolSize,
            maxPoolSize);
    }

    private void Start()
    {
        RefreshVisibleGuides(true);
    }

    private void OnDestroy()
    {
        if (Pool != null)
        {
            DestroyVisibleGuides();
            Pool.Clear();
            Pool = null;
        }

        if (Instance == this)
        {
            Instance = null;
            ReferenceY = 0f;
            ReferenceYChanged = null;
        }
    }

    private void OnValidate()
    {
        maxPoolSize = Math.Max(initialPoolSize, maxPoolSize);
    }

    [ContextMenu("Refresh Visible Guides")]
    private void RefreshVisibleGuidesFromContextMenu()
    {
        RefreshVisibleGuides(true);
    }

    /// <summary>UI 입력 문자열을 가이드 분할 수로 해석해 다시 생성합니다.</summary>
    public void ReGenerate(string input)
    {
        if (!int.TryParse(input, out int newGuidesPerMeasure) ||
            newGuidesPerMeasure < 1)
        {
            Debug.LogWarning(
                $"Guide division must be a positive integer. Input: {input}",
                this);
            return;
        }

        ReGenerate(newGuidesPerMeasure);
    }

    /// <summary>
    /// 한 마디의 가이드 개수를 변경하고 현재 보이는 범위만 풀에서 다시 배치합니다.
    /// </summary>
    public void ReGenerate(int newGuidesPerMeasure)
    {
        guidesPerMeasure = Mathf.Max(1, newGuidesPerMeasure);
        ChartPlacementController.SetYGuideCount(guidesPerMeasure);
        currentFirstIndex = -1;
        currentLastIndex = -1;

        if (Pool == null)
        {
            return;
        }

        ReleaseAllGuides();
        RefreshVisibleGuides(true);
    }

    /// <summary>
    /// 기준 Y 주변의 가이드만 활성화하고 범위를 벗어난 가이드는 풀로 돌려보냅니다.
    /// </summary>
    public void RefreshVisibleGuides(bool force)
    {
        if (Pool == null)
        {
            return;
        }

        float spacing = GuideSpacing;
        float rangeMin = ReferenceY - visibleBefore;
        float rangeMax = ReferenceY + visibleAfter;
        int firstIndex = GetFirstVisibleIndex(rangeMin, spacing);
        int lastIndex = GetLastVisibleIndex(rangeMax, spacing);

        if (firstIndex > lastIndex)
        {
            ReleaseAllGuides();
            currentFirstIndex = -1;
            currentLastIndex = -1;
            return;
        }

        if (!force &&
            firstIndex == currentFirstIndex &&
            lastIndex == currentLastIndex)
        {
            return;
        }

        ReleaseOutsideRange(firstIndex, lastIndex);
        ShowRange(firstIndex, lastIndex, spacing);
        currentFirstIndex = firstIndex;
        currentLastIndex = lastIndex;
    }

    /// <summary>가이드 데이터와 풀은 유지한 채 화면 표시 여부만 전환합니다.</summary>
    public void SetGuidesVisible(bool visible)
    {
        guidesVisible = visible;

        if (guideField)
        {
            guideField.gameObject.SetActive(visible);
        }

        if (visible)
        {
            RefreshVisibleGuides(true);
        }
    }

    public void ToggleGuidesVisible()
    {
        SetGuidesVisible(!guidesVisible);
    }

    private int GetFirstVisibleIndex(float rangeMin, float spacing)
    {
        // guide index 1 is placed at Y=0, so range indices need a +1 offset.
        return Mathf.Max(
            1,
            Mathf.CeilToInt((rangeMin - IndexEpsilon) / spacing) + 1);
    }

    private int GetLastVisibleIndex(float rangeMax, float spacing)
    {
        return Mathf.Min(
            TotalGuideCount,
            Mathf.FloorToInt((rangeMax + IndexEpsilon) / spacing) + 1);
    }

    private void ReleaseOutsideRange(int firstIndex, int lastIndex)
    {
        releaseIndices.Clear();

        foreach (KeyValuePair<int, GameObject> pair in visibleGuides)
        {
            if (pair.Key < firstIndex || pair.Key > lastIndex)
            {
                releaseIndices.Add(pair.Key);
            }
        }

        for (int i = 0; i < releaseIndices.Count; i++)
        {
            int index = releaseIndices[i];
            GameObject guide = visibleGuides[index];
            if (guide)
            {
                Pool.Release(guide);
            }

            visibleGuides.Remove(index);
        }
    }

    private void ShowRange(int firstIndex, int lastIndex, float spacing)
    {
        Vector3 templatePosition = guidePrefab.transform.localPosition;

        for (int index = firstIndex; index <= lastIndex; index++)
        {
            if (visibleGuides.ContainsKey(index))
            {
                continue;
            }

            GameObject guide = Pool.Get();
            int sectionIndex = GetSectionIndex(index, spacing);
            int indexInSection = GetIndexInSection(index, spacing);
            guide.name = $"Guide Line ({sectionIndex}:{indexInSection})";
            guide.transform.localPosition = new Vector3(
                templatePosition.x,
                GetGuidePositionY(sectionIndex, indexInSection, spacing),
                templatePosition.z);

            string format = string.Format(
                "{0:D3}\n{1}/{2}",
                (indexInSection / guidesPerMeasure),
                (indexInSection % guidesPerMeasure),
                (guidesPerMeasure)
            );
            guide.transform.GetChild(0)
                .GetComponent<TextMeshPro>().text = format;

            ApplyGuideColor(guide, index);
            visibleGuides.Add(index, guide);
        }
    }

    private void ApplyGuideColor(GameObject guide, int guideIndex)
    {
        if (!guide.TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            return;
        }

        bool isMeasureGuide = (guideIndex - 1) % guidesPerMeasure == 0;
        spriteRenderer.color = isMeasureGuide
            ? measureGuideColor
            : guideColor;
    }

    private int GetSectionIndex(int guideIndex, float spacing)
    {
        return (guideIndex - 1) / GetGuidesPerSection(spacing);
    }

    private int GetIndexInSection(int guideIndex, float spacing)
    {
        return (guideIndex - 1) % GetGuidesPerSection(spacing);
    }

    private int GetGuidesPerSection(float spacing)
    {
        return Mathf.Max(1, Mathf.RoundToInt(sectionHeight / spacing));
    }

    private float GetGuidePositionY(
        int sectionIndex,
        int indexInSection,
        float spacing)
    {
        float sectionOffsetY = sectionIndex * sectionHeight;
        float positionInSectionY = indexInSection * spacing;
        return sectionOffsetY + positionInSectionY;
    }

    private void ReleaseAllGuides()
    {
        foreach (GameObject guide in visibleGuides.Values)
        {
            if (guide)
            {
                Pool.Release(guide);
            }
        }

        visibleGuides.Clear();
    }

    private void DestroyVisibleGuides()
    {
        foreach (GameObject guide in visibleGuides.Values)
        {
            if (guide)
            {
                Destroy(guide);
            }
        }

        visibleGuides.Clear();
    }

    private GameObject CreateGuide()
    {
        GameObject guide = Instantiate(guidePrefab, guideField, false);
        return guide;
    }

    private static void OnTakeFromPool(GameObject guide)
    {
        if (guide)
        {
            guide.SetActive(true);
        }
    }

    private static void OnReturnedToPool(GameObject guide)
    {
        if (guide)
        {
            guide.SetActive(false);
        }
    }

    private static void OnDestroyPooledGuide(GameObject guide)
    {
        if (guide)
        {
            Destroy(guide);
        }
    }

    /// <summary>외부 시스템이 사용할 현재 채보 기준 Y를 설정합니다.</summary>
    public static void SetReferenceY(float value)
    {
        if (Instance == null || float.IsNaN(value) || float.IsInfinity(value))
        {
            return;
        }

        if (Mathf.Approximately(ReferenceY, value))
        {
            return;
        }

        ReferenceY = value;
        Instance.RefreshVisibleGuides(false);
        ReferenceYChanged?.Invoke(value);
    }

    /// <summary>ScrollRect의 Y 좌표를 채보 좌표계로 환산해 기준 Y에 적용합니다.</summary>
    public static void SetReferenceFromScrollY(float scrollY)
    {
        if (Instance == null)
        {
            return;
        }

        SetReferenceY(-scrollY * Instance.ScrollToChartRatio);
    }
}
