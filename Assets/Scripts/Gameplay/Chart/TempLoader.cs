using System;
using UnityEngine;

namespace REmind.Gameplay.Chart
{
    public sealed class TempLoader : MonoBehaviour
    {
        private static readonly float[] LineXPositions =
        {
            -11.25f,
            -3.75f,
            3.75f,
            11.25f,
        };

        // Temp JSON stores the original 1600-units-per-measure coordinates.
        private const double LegacyNotePositionScale = 0.1d;

        [SerializeField] private GameObject TapNotePrefab;
        [SerializeField] private Transform NoteField;
        [SerializeField] private TextAsset chartFile;

        public TempChartData Chart { get; private set; }

        private void Awake()
        {
            if (chartFile == null)
            {
                Debug.LogError("Temp chart TextAsset is not assigned.", this);
                enabled = false;
                return;
            }

            try
            {
                Chart = JsonUtility.FromJson<TempChartData>(chartFile.text);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"Failed to parse '{chartFile.name}': {exception.Message}", this);
                enabled = false;
                return;
            }

            if (Chart == null)
            {
                Debug.LogError($"Failed to load '{chartFile.name}'.", this);
                enabled = false;
                return;
            }

            int noteCount = Chart.NoteMs?.Length ?? 0;
            Debug.Log(
                $"Temp chart loaded: {chartFile.name}, " +
                $"Version: {Chart.Version}, BPM: {Chart.bpm}, Notes: {noteCount}",
                this);

            ValidateParallelNoteArrays(noteCount);
            PlaceNotes();
        }

        private void ValidateParallelNoteArrays(int noteCount)
        {
            if ((Chart.NoteLegnth?.Length ?? 0) == noteCount &&
                (Chart.NotePos?.Length ?? 0) == noteCount &&
                (Chart.NoteLine?.Length ?? 0) == noteCount &&
                (Chart.NotePowered?.Length ?? 0) == noteCount)
            {
                return;
            }

            Debug.LogWarning(
                $"Parallel note array lengths do not match in '{chartFile.name}'.",
                this);
        }

        private void PlaceNotes()
        {
            if (TapNotePrefab == null)
            {
                Debug.LogError("Tap Note prefab is not assigned.", this);
                return;
            }

            if (NoteField == null)
            {
                GameObject noteFieldObject = GameObject.Find("NoteField");
                NoteField = noteFieldObject != null ? noteFieldObject.transform : null;
            }

            if (NoteField == null)
            {
                Debug.LogError("NoteField was not found.", this);
                return;
            }

            if (Chart.NotePos == null || Chart.NoteLine == null)
            {
                Debug.LogError("NotePos or NoteLine is missing from the temp chart.", this);
                return;
            }

            for (int i = NoteField.childCount - 1; i >= 0; i--)
            {
                Destroy(NoteField.GetChild(i).gameObject);
            }

            int noteCount = Math.Min(Chart.NotePos.Length, Chart.NoteLine.Length);
            int placedCount = 0;
            int skippedCount = 0;

            for (int i = 0; i < noteCount; i++)
            {
                int lineIndex = Chart.NoteLine[i] - 1;
                if (lineIndex < 0 || lineIndex >= LineXPositions.Length)
                {
                    skippedCount++;
                    continue;
                }

                var localPosition = new Vector3(
                    LineXPositions[lineIndex],
                    (float)(Chart.NotePos[i] * LegacyNotePositionScale),
                    0f);

                GameObject noteObject = Instantiate(TapNotePrefab, NoteField, false);
                noteObject.name = $"Tap Note {i:000}";
                noteObject.transform.localPosition = localPosition;

                placedCount++;
            }

            Debug.Log($"Placed {placedCount} notes under NoteField.", this);

            if (skippedCount > 0)
            {
                Debug.LogWarning(
                    $"Skipped {skippedCount} notes because only lines 1-4 have X positions.",
                    this);
            }
        }

        [Serializable]
        public sealed class TempChartData
        {
            public string Version;
            public double bpm;
            public int startDelayMs;
            public int[] NoteLegnth;
            public double[] NoteMs;
            public double[] NotePos;
            public int[] NoteLine;
            public bool[] NotePowered;
            public double[] EffectMs;
            public double[] EffectPos;
            public double[] EffectForce;
            public bool[] EffectIsPause;
            public double[] SpeedMs;
            public double[] SpeedPos;
            public double[] SpeedBpm;
            public int[] SpeedNum;
        }
    }
}
