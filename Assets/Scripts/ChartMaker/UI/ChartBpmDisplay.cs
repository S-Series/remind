using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class ChartBpmDisplay : MonoBehaviour
{
    private static readonly CultureInfo Invariant =
        CultureInfo.InvariantCulture;

    [SerializeField] private ChartCore chartCore;
    [SerializeField] private TMP_Text bpmText;

    private void OnEnable()
    {
        ResolveReferences();

        if (!chartCore || !bpmText)
        {
            Debug.LogError(
                "ChartBpmDisplay requires ChartCore and TMP_Text.",
                this);
            return;
        }

        chartCore.BpmChanged += HandleBpmChanged;
        Refresh(chartCore.Bpm);
    }

    private void OnDisable()
    {
        if (chartCore)
        {
            chartCore.BpmChanged -= HandleBpmChanged;
        }
    }

    private void OnValidate()
    {
        if (!bpmText)
        {
            bpmText = GetComponent<TMP_Text>();
        }
    }

    private void ResolveReferences()
    {
        if (!bpmText)
        {
            bpmText = GetComponent<TMP_Text>();
        }

        if (!chartCore)
        {
            chartCore = ChartCore.Instance != null
                ? ChartCore.Instance
                : FindFirstObjectByType<ChartCore>();
        }
    }

    /// <summary>Updates the chart header whenever the base BPM changes.</summary>
    private void HandleBpmChanged(double value)
    {
        Refresh(value);
    }

    private void Refresh(double value)
    {
        bpmText.text =
            $"Start Bpm\n<size=320>{value.ToString("0.###", Invariant)}</size>";
    }
}
