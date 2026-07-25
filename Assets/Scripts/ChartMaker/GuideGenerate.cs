using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public sealed class GuideGenerate : MonoBehaviour
{
    private const float IndexEpsilon = 0.001f;

    [Header("References")]
    [SerializeField] private GameObject guidePrefab;
    [SerializeField] private Transform guideField;
    [SerializeField] private ChartScroll chartScroll;

    [Header("Chart")]
    [SerializeField, Min(1)] private int measureCount = 1000;
    [SerializeField, Min(1)] private int guidesPerMeasure = 4;
    [SerializeField, Min(1f)] private float measureHeight = 160f;
    [SerializeField, Min(1f)] private float sectionHeight = 1600f;

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

    public ObjectPool<GameObject> Pool { get; private set; }
    public int TotalGuideCount => measureCount * guidesPerMeasure;
    public int VisibleGuideCount => visibleGuides.Count;
    public float GuideSpacing => measureHeight / guidesPerMeasure;
    public float ScrollToChartRatio => measureHeight / scrollMeasureHeight;
    public float ReferenceY => chartScroll != null
        ? -chartScroll.ScrollY * ScrollToChartRatio
        : 0f;

    private void Awake()
    {
        if (!guidePrefab || !guideField || !chartScroll)
        {
            Debug.LogError(
                "GuideGenerate requires a guide prefab, guide field, and ChartScroll.",
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

    private void OnEnable()
    {
        if (chartScroll != null)
        {
            chartScroll.ScrollYChanged += HandleScrollYChanged;
        }
    }

    private void Start()
    {
        RefreshVisibleGuides(true);
    }

    private void OnDisable()
    {
        if (chartScroll != null)
        {
            chartScroll.ScrollYChanged -= HandleScrollYChanged;
        }
    }

    private void OnDestroy()
    {
        Pool?.Clear();
    }

    private void OnValidate()
    {
        maxPoolSize = Math.Max(initialPoolSize, maxPoolSize);
    }

    private void HandleScrollYChanged(float _)
    {
        RefreshVisibleGuides(false);
    }

    [ContextMenu("Refresh Visible Guides")]
    private void RefreshVisibleGuidesFromContextMenu()
    {
        RefreshVisibleGuides(true);
    }

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

    public void ReGenerate(int newGuidesPerMeasure)
    {
        guidesPerMeasure = Mathf.Max(1, newGuidesPerMeasure);
        currentFirstIndex = -1;
        currentLastIndex = -1;

        if (Pool == null)
        {
            return;
        }

        ReleaseAllGuides();
        RefreshVisibleGuides(true);
    }

    public void RefreshVisibleGuides(bool force)
    {
        if (Pool == null)
        {
            return;
        }

        float spacing = GuideSpacing;
        float rangeMin = ReferenceY - visibleBefore;
        float rangeMax = ReferenceY + visibleAfter;
        int firstIndex = Mathf.Max(
            1,
            Mathf.CeilToInt((rangeMin - IndexEpsilon) / spacing));
        int lastIndex = Mathf.Min(
            TotalGuideCount,
            Mathf.FloorToInt((rangeMax + IndexEpsilon) / spacing));

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
            Pool.Release(visibleGuides[index]);
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

        bool isMeasureGuide = guideIndex % guidesPerMeasure == 0;
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
        float positionInSectionY = (indexInSection + 1) * spacing;
        return sectionOffsetY + positionInSectionY;
    }

    private void ReleaseAllGuides()
    {
        foreach (GameObject guide in visibleGuides.Values)
        {
            Pool.Release(guide);
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
        guide.SetActive(true);
    }

    private static void OnReturnedToPool(GameObject guide)
    {
        guide.SetActive(false);
    }

    private static void OnDestroyPooledGuide(GameObject guide)
    {
        if (guide)
        {
            Destroy(guide);
        }
    }
}
