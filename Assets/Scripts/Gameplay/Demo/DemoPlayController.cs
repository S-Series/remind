using System;
using System.Collections.Generic;
using REmind.Charting;
using REmind.Data;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;

namespace REmind.Gameplay.Demo
{
    [DisallowMultipleComponent]
    public sealed class DemoPlayController : MonoBehaviour
    {
        private static readonly float[] LineXPositions =
        {
            -11.25f,
            -3.75f,
            3.75f,
            11.25f
        };

        [Header("Systems")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [SerializeField] private GameplayChartSessionController chartSession;

        [Header("Movement")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(1f)] private float bpm = 225f;
        [SerializeField, Min(0.01f)] private double speedMultiplier = 1d;

        [Header("Demo Notes")]
        [SerializeField] private GameObject tapNotePrefab;
        [SerializeField] private Transform noteField;
        [SerializeField] private double noteCorrectionMs;
        [SerializeField] private bool playOnReady = true;

        private Transform playCanvasTransform;
        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;
        private Vector3 initialPlayCanvasScale;
        private PreparedGameplayChart preparedChart;

        private double QuarterNoteMs => 60000d / bpm;
        private double EffectiveSpeedMultiplier => speedMultiplier * 0.5d;

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            initialCameraPosition = cameraTransform.position;
            initialCameraRotation = cameraTransform.rotation;
            initialPlayCanvasScale = playCanvasTransform.localScale;
            ApplySpeedMultiplier();

            bool chartReady;
            if (chartSession)
            {
                chartReady = chartSession.TryPrepareConfiguredChart();
                if (chartReady)
                {
                    preparedChart = chartSession.CurrentChart;
                    chartReady = BuildPreparedChartViews();
                }
            }
            else
            {
                chartReady = BuildDemoChart();
            }

            if (!chartReady)
            {
                enabled = false;
                return;
            }

            if (playOnReady)
            {
                if (!gameManager.StartGame())
                {
                    Debug.LogError(
                        "DemoPlay prepared its chart, but automatic playback " +
                        "start was rejected. Review the preceding gameplay " +
                        "error or use Play to retry.",
                        this);
                }
            }
        }

        private void Update()
        {
            if (!gameManager)
            {
                return;
            }

            Vector3 cameraPosition = initialCameraPosition;
            Quaternion cameraRotation = initialCameraRotation;

            if (preparedChart != null)
            {
                double chartTimeMs = gameManager.CorePlayMs -
                    preparedChart.ChartOffsetMs;
                cameraPosition.z += (float)(
                    preparedChart.Snapshot.ScrollMap.FloorPositionAtTime(
                        chartTimeMs) * EffectiveSpeedMultiplier);

                CameraMotionState cameraState = preparedChart.Snapshot
                    .CameraMotionMap.EvaluateAtTime(chartTimeMs);
                if (cameraState.HasReference)
                {
                    cameraPosition.x += (float)
                        cameraState.ReferenceOffsetX;
                }

                double scratchTilt = preparedChart.Snapshot
                    .ScratchCameraTiltMap.EvaluateAtTime(chartTimeMs);
                cameraRotation = initialCameraRotation * Quaternion.Euler(
                    0f,
                    0f,
                    (float)(cameraState.SpinDegrees + scratchTilt));
            }
            else
            {
                cameraPosition.z += (float)(
                    MsToPosition(Math.Max(0d, gameManager.CorePlayMs)) *
                    EffectiveSpeedMultiplier);
            }

            cameraTransform.position = cameraPosition;
            cameraTransform.rotation = cameraRotation;
        }

        private void OnDestroy()
        {
            if (judgementSystem)
            {
                judgementSystem.SetAutoPlayEnabled(false);
            }
        }

        public void ResetView()
        {
            if (cameraTransform)
            {
                cameraTransform.position = initialCameraPosition;
                cameraTransform.rotation = initialCameraRotation;
            }
        }

        public void ResetJudgements()
        {
            judgementSystem?.ResetJudgements();
        }

        public void SetAutoPlayEnabled(bool value)
        {
            judgementSystem?.SetAutoPlayEnabled(value);
        }

        private double MsToPosition(double ms)
        {
            return bpm * ms / 1500d;
        }

        private bool ValidateReferences()
        {
            if (!gameManager ||
                !judgementSystem ||
                !cameraTransform ||
                !tapNotePrefab ||
                !noteField)
            {
                Debug.LogError(
                    "DemoPlayController requires all system, movement, and note references.",
                    this);
                return false;
            }

            playCanvasTransform = noteField.parent;

            if (!playCanvasTransform ||
                double.IsNaN(speedMultiplier) ||
                double.IsInfinity(speedMultiplier) ||
                speedMultiplier <= 0d)
            {
                Debug.LogError(
                    "DemoPlayController has an invalid Play Canvas or speed multiplier.",
                    this);
                return false;
            }

            return true;
        }

        private void ApplySpeedMultiplier()
        {
            Vector3 playCanvasScale = initialPlayCanvasScale;
            playCanvasScale.y *= (float)EffectiveSpeedMultiplier;
            playCanvasTransform.localScale = playCanvasScale;
        }

        /// <summary>테스트 곡 길이에 맞춰 1/4박 데모 노트와 판정 큐를 구성합니다.</summary>
        private bool BuildDemoChart()
        {
            for (int i = noteField.childCount - 1; i >= 0; i--)
            {
                Destroy(noteField.GetChild(i).gameObject);
            }

            double songDurationMs = gameManager.GamePlay.SongDurationMs;
            int noteCount = (int)Math.Ceiling(songDurationMs / QuarterNoteMs);
            List<NoteData> notes = new List<NoteData>(noteCount);

            judgementSystem.ClearRegisteredNoteViews();

            for (int noteIndex = 0; noteIndex < noteCount; noteIndex++)
            {
                int lineIndex = noteIndex % LineXPositions.Length;
                double hitTimeMs = noteIndex * QuarterNoteMs;
                double correctedHitTimeMs = hitTimeMs + noteCorrectionMs;
                string noteId = $"test-quarter-{noteIndex:0000}";

                GameObject noteObject = Instantiate(
                    tapNotePrefab,
                    noteField,
                    false);
                noteObject.name =
                    $"Quarter Note {noteIndex:0000} - Line {lineIndex + 1}";
                noteObject.transform.localPosition = new Vector3(
                    LineXPositions[lineIndex],
                    (float)MsToPosition(correctedHitTimeMs),
                    0f);

                Vector3 noteScale = noteObject.transform.localScale;
                noteScale.y /= (float)EffectiveSpeedMultiplier;
                noteObject.transform.localScale = noteScale;

                long storedHitTimeMs = checked((long)Math.Round(
                    hitTimeMs,
                    MidpointRounding.AwayFromZero));
                notes.Add(new NoteData(
                    noteId,
                    NoteType.Tap,
                    lineIndex,
                    storedHitTimeMs,
                    0L));

                if (!judgementSystem.RegisterNoteView(noteId, noteObject))
                {
                    Debug.LogError(
                        $"Could not register demo note view '{noteId}'.",
                        this);
                    return false;
                }
            }

            return judgementSystem.Initialize(
                LineXPositions.Length,
                notes,
                noteCorrectionMs);
        }

        /// <summary>
        /// DemoPlay currently owns only the four main-lane Tap visuals.  All note
        /// families still enter judgement through the shared prepared Snapshot;
        /// their dedicated presenters remain outside this Effect connection.
        /// </summary>
        private bool BuildPreparedChartViews()
        {
            if (preparedChart?.Snapshot == null)
            {
                Debug.LogError(
                    "DemoPlay did not receive a prepared gameplay Snapshot.",
                    this);
                return false;
            }

            for (int index = noteField.childCount - 1; index >= 0; index--)
            {
                Destroy(noteField.GetChild(index).gameObject);
            }

            judgementSystem.ClearRegisteredNoteViews();

            IReadOnlyList<PlayableNoteSnapshot> notes =
                preparedChart.Snapshot.Notes;
            for (int noteIndex = 0; noteIndex < notes.Count; noteIndex++)
            {
                PlayableNoteSnapshot note = notes[noteIndex];
                if (note.Kind != ChartNoteKind.Tap ||
                    note.Lane < 0 || note.Lane >= LineXPositions.Length)
                {
                    continue;
                }

                GameObject noteObject = Instantiate(
                    tapNotePrefab,
                    noteField,
                    false);
                noteObject.name =
                    $"Chart Tap {note.Id} - Line {note.Lane + 1}";
                noteObject.transform.localPosition = new Vector3(
                    LineXPositions[note.Lane],
                    (float)note.StartFloorPosition,
                    0f);

                Vector3 noteScale = noteObject.transform.localScale;
                noteScale.y /= (float)EffectiveSpeedMultiplier;
                noteObject.transform.localScale = noteScale;

                if (!judgementSystem.RegisterNoteView(
                        note.Id,
                        noteObject))
                {
                    Debug.LogError(
                        $"Could not register chart note view '{note.Id}'.",
                        this);
                    return false;
                }
            }

            return true;
        }
    }
}
