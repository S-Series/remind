using System;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartToFile : MonoBehaviour
{
    public const string ChartFileExtension = ".rd";
    public const string ChartFileExtensionWithoutDot = "rd";
    public const string DefaultChartFileName = "chart.rd";

    private const string DefaultDirectoryName = "Charts";

    [SerializeField] private string defaultFileName = DefaultChartFileName;
    [SerializeField] private string currentFilePath;
    [SerializeField] private ChartCore chartCore;
    [SerializeField] private ChartPlacementController placementController;

    private string savedChartText = string.Empty;
    private string savedParameterText = string.Empty;
    private bool recoveredStateRequiresSave;

    public string CurrentFilePath => currentFilePath;
    public bool HasSavePath => !string.IsNullOrWhiteSpace(currentFilePath);
    public bool HasUnsavedChanges
    {
        get
        {
            if (recoveredStateRequiresSave)
            {
                return true;
            }

            try
            {
                return !string.Equals(
                    BuildText(),
                    savedChartText,
                    StringComparison.Ordinal) || !string.Equals(
                        BuildParameterText(), savedParameterText, StringComparison.Ordinal);
            }
            catch
            {
                return true;
            }
        }
    }

    public event Action<string> ChartSaved;

    private void Awake()
    {
        NormalizeDefaultFileName();

        if (!string.IsNullOrWhiteSpace(currentFilePath) &&
            !HasChartFileExtension(currentFilePath))
        {
            currentFilePath = null;
        }

        ResolveChartCore();
        ResolvePlacementController();
        MarkCurrentStateAsSaved();
    }

    private void OnValidate()
    {
        NormalizeDefaultFileName();
    }

    /// <summary>현재 채보 데이터와 편집용 타이밍 설정을 JSON 문자열로 만듭니다.</summary>
    public string BuildText()
    {
        ResolveChartCore();

        if (!chartCore)
        {
            throw new InvalidOperationException(
                "ChartToFile requires ChartCore to save timing metadata.");
        }

        return ChartFileCodec.Serialize(
            ChartManager.ChartHolders,
            chartCore.Bpm,
            chartCore.StartCorrectionMs,
            ChartEffectDocumentState.Capture());
    }

    private static string BuildParameterText() => ChartEffectFileStore.SerializeParameters(
        ChartManager.ChartHolders, ChartEffectDocumentState.Capture());

    /// <summary>현재 채보를 지정한 경로에 UTF-8(BOM 없음)로 저장합니다.</summary>
    public void SaveToPath(string filePath)
    {
        ResolveChartCore();

        if (chartCore && chartCore.IsTestPlaying)
        {
            throw new InvalidOperationException(
                "Stop test playback before saving the chart.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "A chart file path is required.",
                nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);

        if (!HasChartFileExtension(fullPath))
        {
            throw new ArgumentException(
                $"Chart files can only be saved with the " +
                $"{ChartFileExtension} extension.",
                nameof(filePath));
        }

        string directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        ChartLongNoteSaveNormalizationPlan normalizationPlan =
            ChartLongNoteSaveNormalizer.CreatePlan(
                ChartManager.ChartHolders);
        ChartEditHistory.ChartEditTransaction editTransaction =
            normalizationPlan.HasChanges
                ? ChartEditHistory.BeginChange(
                    normalizationPlan.AffectedPositions)
                : default;
        if (!chartCore) throw new InvalidOperationException("ChartCore is required for saving.");
        var metadata = new ChartEffectDocumentState.Metadata(
            ChartEffectDocumentState.MusicId, ChartEffectDocumentState.DifficultyId,
            ChartEffectDocumentState.GimmickId, Guid.NewGuid().ToString("N"));
        string chartText = ChartFileCodec.Serialize(ChartManager.ChartHolders,
            chartCore.Bpm, chartCore.StartCorrectionMs, metadata);
        string parameterText = ChartEffectFileStore.SerializeParameters(ChartManager.ChartHolders, metadata);
        SongContentFileStore.ValidateChart(fullPath, ChartFileCodec.Parse(chartText));
        ChartEffectFileStore.Save(fullPath, chartText, parameterText);
        ChartEffectDocumentState.Restore(metadata);
        ApplySaveNormalization(
            normalizationPlan,
            editTransaction);

        currentFilePath = fullPath;
        savedChartText = chartText;
        savedParameterText = parameterText;
        recoveredStateRequiresSave = false;
        ChartMakerRecentFiles.RememberChartPath(currentFilePath);
        ChartSaved?.Invoke(currentFilePath);
    }

    /// <summary>현재 경로 또는 기본 사용자 데이터 경로에 채보를 저장합니다.</summary>
    public void Save()
    {
        SaveToPath(GetSavePath());
    }

    /// <summary>Exports the current validated document for a Game build.</summary>
    public void ExportRuntimePackageToPath(string filePath)
    {
        ResolveChartCore();
        if (chartCore && chartCore.IsTestPlaying)
            throw new InvalidOperationException(
                "Stop Preview before exporting a runtime package.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("An export path is required.",
                nameof(filePath));
        string fullPath = Path.GetFullPath(filePath);
        if (!string.Equals(Path.GetExtension(fullPath), ".json",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Runtime package must use .json.",
                nameof(filePath));

        // Build and validate the complete package before touching the output.
        string package = ChartMakerRuntimePackageExporter.Export(
            BuildText(), BuildParameterText());
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, package, new UTF8Encoding(false));
    }

    public void SetSavePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            currentFilePath = null;
            return;
        }

        currentFilePath = Path.GetFullPath(filePath);

        if (!HasChartFileExtension(currentFilePath))
        {
            currentFilePath = null;
        }
    }

    /// <summary>새 문서 상태로 전환하고 현재 빈 채보를 저장 기준으로 기록합니다.</summary>
    public void ResetDocument()
    {
        currentFilePath = null;
        MarkCurrentStateAsSaved();
    }

    /// <summary>현재 채보를 마지막 저장 상태로 기록합니다.</summary>
    public void MarkCurrentStateAsSaved()
    {
        savedChartText = BuildText();
        savedParameterText = BuildParameterText();
        recoveredStateRequiresSave = false;
    }

    /// <summary>
    /// A matching backup pair is valid in memory, but it is not the current pair on
    /// disk until the user saves it. Keep close/dirty prompts active until then.
    /// </summary>
    public void MarkRecoveredStateRequiresSave()
    {
        recoveredStateRequiresSave = true;
    }

    public bool TrySaveToPath(string filePath, out string error)
    {
        try
        {
            SaveToPath(filePath);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private string GetSavePath()
    {
        if (!string.IsNullOrWhiteSpace(currentFilePath))
        {
            return currentFilePath;
        }

        return Path.Combine(
            Application.persistentDataPath,
            DefaultDirectoryName,
            defaultFileName);
    }

    private void NormalizeDefaultFileName()
    {
        defaultFileName = string.IsNullOrWhiteSpace(defaultFileName)
            ? DefaultChartFileName
            : Path.ChangeExtension(
                Path.GetFileName(defaultFileName),
                ChartFileExtension);
    }

    public static bool HasChartFileExtension(string filePath)
    {
        return string.Equals(
            Path.GetExtension(filePath),
            ChartFileExtension,
            StringComparison.OrdinalIgnoreCase);
    }

    private void ResolveChartCore()
    {
        if (!chartCore)
        {
            chartCore = ChartCore.Instance != null
                ? ChartCore.Instance
                : FindFirstObjectByType<ChartCore>();
        }
    }

    private void ResolvePlacementController()
    {
        if (!placementController)
        {
            placementController =
                FindFirstObjectByType<ChartPlacementController>();
        }
    }

    private void ApplySaveNormalization(
        ChartLongNoteSaveNormalizationPlan plan,
        ChartEditHistory.ChartEditTransaction editTransaction)
    {
        if (plan == null || !plan.HasChanges)
        {
            return;
        }

        plan.Apply();
        ChartEditHistory.CommitChange(editTransaction);
        ResolvePlacementController();

        if (placementController)
        {
            placementController.RebuildChartViews(
                recreateExisting: true);
        }
        else
        {
            ChartManager.NotifyChartChanged();
        }

        Debug.LogWarning(
            $"Save normalized unfinished Long Notes: " +
            $"Long Tap {plan.ConvertedLongTapCount}, " +
            $"Long Scratch points " +
            $"{plan.ConvertedLongScratchPointCount}.",
            this);
    }

    private static void WriteAtomically(string fullPath, string chartText)
    {
        string temporaryPath = fullPath + ".tmp";

        try
        {
            File.WriteAllText(
                temporaryPath,
                chartText,
                new UTF8Encoding(false));

            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, null);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

}
