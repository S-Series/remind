using System;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartToFile : MonoBehaviour
{
    private const string DefaultDirectoryName = "Charts";
    private const string FallbackFileName = "chart.txt";

    [SerializeField] private string defaultFileName = FallbackFileName;
    [SerializeField] private string currentFilePath;

    private string savedChartText = string.Empty;

    public string CurrentFilePath => currentFilePath;
    public bool HasSavePath => !string.IsNullOrWhiteSpace(currentFilePath);
    public bool HasUnsavedChanges
    {
        get
        {
            try
            {
                return !string.Equals(
                    BuildText(),
                    savedChartText,
                    StringComparison.Ordinal);
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
        MarkCurrentStateAsSaved();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(defaultFileName))
        {
            defaultFileName = FallbackFileName;
        }
    }

    /// <summary>현재 ChartManager 데이터를 파일 포맷 문자열로 만듭니다.</summary>
    public string BuildText()
    {
        return ChartFileCodec.Serialize(ChartManager.ChartHolders);
    }

    /// <summary>현재 채보를 지정한 경로에 UTF-8(BOM 없음)로 저장합니다.</summary>
    public void SaveToPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "A chart file path is required.",
                nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string chartText = BuildText();

        WriteAtomically(fullPath, chartText);

        currentFilePath = fullPath;
        savedChartText = chartText;
        ChartSaved?.Invoke(currentFilePath);
    }

    /// <summary>현재 경로 또는 기본 사용자 데이터 경로에 채보를 저장합니다.</summary>
    public void Save()
    {
        SaveToPath(GetSavePath());
    }

    public void SetSavePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            currentFilePath = null;
            return;
        }

        currentFilePath = Path.GetFullPath(filePath);
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
