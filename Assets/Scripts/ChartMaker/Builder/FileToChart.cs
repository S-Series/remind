using System;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FileToChart : MonoBehaviour
{
    [SerializeField] private ChartPlacementController placementController;
    [SerializeField] private ChartToFile chartToFile;

    private void Awake()
    {
        if (!placementController)
        {
            placementController = FindFirstObjectByType<ChartPlacementController>();
        }

        if (!chartToFile)
        {
            chartToFile = GetComponent<ChartToFile>();
        }
    }

    /// <summary>텍스트를 검증하고 현재 ChartManager와 노트 뷰에 적용합니다.</summary>
    public ChartFile LoadText(string text)
    {
        ChartFile chartFile = ChartFileCodec.Parse(text);
        ChartManager.ReplaceChartData(chartFile.chartDatas);

        if (placementController)
        {
            placementController.RebuildChartViews();
        }
        else
        {
            Debug.LogWarning(
                "Chart data loaded, but no ChartPlacementController was found.",
                this);
        }

        return chartFile;
    }

    /// <summary>지정한 UTF-8 채보 파일을 읽어 현재 채보에 적용합니다.</summary>
    public ChartFile LoadFromPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "A chart file path is required.",
                nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);
        string text = File.ReadAllText(
            fullPath,
            Encoding.UTF8);
        ChartFile chartFile = LoadText(text);

        if (chartToFile)
        {
            chartToFile.SetSavePath(fullPath);
            chartToFile.MarkCurrentStateAsSaved();
        }

        return chartFile;
    }

    public bool TryLoadText(
        string text,
        out ChartFile chartFile,
        out string error)
    {
        try
        {
            chartFile = LoadText(text);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            chartFile = null;
            error = exception.Message;
            return false;
        }
    }

    public bool TryLoadFromPath(
        string filePath,
        out ChartFile chartFile,
        out string error)
    {
        try
        {
            chartFile = LoadFromPath(filePath);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            chartFile = null;
            error = exception.Message;
            return false;
        }
    }
}
