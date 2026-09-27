using System;
using System.Collections.Generic;
using REmind.Charting;
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
        [SerializeField, Min(0.01f)] private double speedMultiplier = 1d;

        [Header("Demo Notes")]
        [SerializeField] private GameObject tapNotePrefab;
        [SerializeField] private GameObject scratchNotePrefab;
        [SerializeField] private GameObject longTapNotePrefab;
        [SerializeField] private GameObject longScratchNotePrefab;
        [SerializeField] private GameObject airNotePrefab;
        [SerializeField] private Transform noteField;
        [SerializeField] private bool playOnReady = true;

        private Transform playCanvasTransform;
        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;
        private Vector3 initialPlayCanvasScale;
        private PreparedGameplayChart preparedChart;
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }

        private double EffectiveSpeedMultiplier => speedMultiplier * 0.5d;

        private void Start()
        {
            if (!ValidateReferences())
            {
                LastError = "Gameplay scene references are incomplete.";
                enabled = false;
                return;
            }

            initialCameraPosition = cameraTransform.position;
            initialCameraRotation = cameraTransform.rotation;
            initialPlayCanvasScale = playCanvasTransform.localScale;
            ApplySpeedMultiplier();

            bool chartReady = false;
            if (chartSession)
            {
                chartReady = chartSession.TryPrepareConfiguredChart();
                if (chartReady)
                {
                    preparedChart = chartSession.CurrentChart;
                    chartReady = BuildPreparedChartViews();
                }
            }

            if (!chartReady)
            {
                LastError = chartSession ? chartSession.LastError :
                    "Could not create the demo chart.";
                enabled = false;
                return;
            }

            IsReady = true;

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

        private bool ValidateReferences()
        {
            if (!gameManager ||
                !judgementSystem ||
                !chartSession ||
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

        /// <summary>Build one view per compiled note and preserve every point.</summary>
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
                if (!ChartLaneLayout.IsValid(note.Lane))
                {
                    continue;
                }
                var noteObject = new GameObject($"{note.Kind} {note.Id}");
                noteObject.transform.SetParent(noteField, false);
                GameObject prefab = PrefabFor(note.Kind);
                for (int pointIndex = 0; pointIndex < note.Points.Count;
                     pointIndex++)
                {
                    PlayableNotePoint point = note.Points[pointIndex];
                    GameObject marker = Instantiate(prefab,
                        noteObject.transform, false);
                    marker.name = point.Kind.ToString();
                    marker.transform.localPosition = new Vector3(
                        LaneX(note.Lane), (float)point.FloorPosition,
                        note.Lane >= 4 && note.Lane < 8 ? -2f : 0f);
                    Vector3 markerScale = marker.transform.localScale;
                    markerScale.y /= (float)EffectiveSpeedMultiplier;
                    marker.transform.localScale = markerScale;
                }
                for (int segmentIndex = 0;
                     segmentIndex + 1 < note.Points.Count; segmentIndex++)
                {
                    PlayableNotePoint start = note.Points[segmentIndex];
                    PlayableNotePoint end = note.Points[segmentIndex + 1];
                    GameObject ribbon = Instantiate(prefab,
                        noteObject.transform, false);
                    ribbon.name = $"Segment {segmentIndex + 1}";
                    ribbon.transform.localPosition = new Vector3(
                        LaneX(note.Lane),
                        (float)((start.FloorPosition +
                            end.FloorPosition) * 0.5d),
                        note.Lane >= 4 && note.Lane < 8 ? -2f : 0f);
                    SpriteRenderer sprite = ribbon.GetComponent<SpriteRenderer>();
                    if (sprite && sprite.sprite)
                    {
                        Color color = sprite.color;
                        color.a *= 0.4f;
                        sprite.color = color;
                        sprite.sortingOrder--;
                        Vector3 scale = ribbon.transform.localScale;
                        scale.y = (float)Math.Max(0.01d,
                            Math.Abs(end.FloorPosition -
                                start.FloorPosition) /
                            sprite.sprite.bounds.size.y /
                            EffectiveSpeedMultiplier);
                        ribbon.transform.localScale = scale;
                    }
                }

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

        private GameObject PrefabFor(ChartNoteKind kind)
        {
            switch (kind)
            {
                case ChartNoteKind.Scratch:
                    return scratchNotePrefab ? scratchNotePrefab : tapNotePrefab;
                case ChartNoteKind.Hold:
                    return longTapNotePrefab ? longTapNotePrefab : tapNotePrefab;
                case ChartNoteKind.LongScratch:
                    return longScratchNotePrefab ? longScratchNotePrefab : tapNotePrefab;
                case ChartNoteKind.Air:
                    return airNotePrefab ? airNotePrefab : tapNotePrefab;
                default: return tapNotePrefab;
            }
        }

        private static float LaneX(int lane)
        {
            if (lane < 4) return LineXPositions[lane];
            if (lane < 8) return LineXPositions[lane - 4];
            return lane == 8 ? -15f : 15f;
        }
    }
}
