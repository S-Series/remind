using System;
using System.Collections.Generic;
using System.Globalization;
using REmind.Gameplay.Chart.Data;
using REmind.Gameplay.Input.Judgement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Test : MonoBehaviour
{
    private static readonly float[] LineXPositions =
    {
        -11.25f,
        -3.75f,
        3.75f,
        11.25f,
    };

    [Header("Display")]
    [SerializeField] private TextMeshPro TestTMpro;
    [SerializeField] private Button TestButton;

    [Header("Movement")]
    [SerializeField] private Transform CameraTransform;
    [SerializeField, Min(1f)] private float Bpm = 225f;
    [SerializeField, Min(0.01f)] private double SpeedMultiplier = 1d;

    [Header("Notes")]
    [SerializeField] private GameObject TapNotePrefab;
    [SerializeField] private Transform NoteField;
    [SerializeField] private double NoteCorrectionMs;

    [Header("Judgement")]
    [SerializeField] private NoteJudgementSystem NoteJudgementSystem;

    private GameManager gameManager;
    private Transform playCanvasTransform;
    private Vector3 initialCameraPosition;
    private Vector3 initialPlayCanvasScale;

    private double QuarterNoteMs => 60000d / Bpm;
    private double EffectiveSpeedMultiplier => SpeedMultiplier * 0.5d;

    private void Start()
    {
        gameManager = GameManager.Instance;

        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        TestButton.onClick.AddListener(TogglePlayback);
        initialCameraPosition = CameraTransform.position;
        initialPlayCanvasScale = playCanvasTransform.localScale;
        ApplySpeedMultiplier();

        if (!PlaceQuarterNotes())
        {
            enabled = false;
            return;
        }

        gameManager.StopGame();
        SetDisplayedSongTime(0d);
    }

    private void Update()
    {
        double currentMs = gameManager.CorePlayMs;
        SetDisplayedSongTime(currentMs);

        Vector3 cameraPosition = initialCameraPosition;
        cameraPosition.z += (float)(MsToPosition(Math.Max(0d, currentMs)) * EffectiveSpeedMultiplier);
        CameraTransform.position = cameraPosition;
    }

    private void OnDestroy()
    {
        if (TestButton != null)
        {
            TestButton.onClick.RemoveListener(TogglePlayback);
        }
    }

    public void TogglePlayback()
    {
        bool succeeded;

        switch (gameManager.PlaybackState)
        {
            case PlaybackState.Playing:
                succeeded = gameManager.PauseGame();
                break;
            case PlaybackState.Paused:
                succeeded = gameManager.ResumeGame();
                break;
            case PlaybackState.Ready:
                succeeded = gameManager.StartGame();
                break;
            case PlaybackState.Finished:
                NoteJudgementSystem.ResetJudgements();
                succeeded = gameManager.RestartGame();
                break;
            default:
                succeeded = false;
                break;
        }

        if (!succeeded)
        {
            Debug.LogWarning(
                $"Playback toggle was ignored in state {gameManager.PlaybackState}.",
                this);
        }
    }

    public double MsToPosition(double ms)
    {
        return Bpm * ms / 1500d;
    }

    public double PositionToMs(double position)
    {
        return 1500d * position / Bpm;
    }

    public void ChangeText(string value)
    {
        TestTMpro.SetText(value);
    }

    private void SetDisplayedSongTime(double valueMs)
    {
        TestTMpro.SetText(
            valueMs.ToString("F6", CultureInfo.InvariantCulture) + " ms");
    }

    private bool ValidateReferences()
    {
        FindTestButtonIfNeeded();

        if (TestTMpro == null)
        {
            Debug.LogError("Test TextMeshPro is not assigned.", this);
            return false;
        }

        if (gameManager == null)
        {
            TestTMpro.SetText("GameManager not found");
            Debug.LogError("GameManager was not found by Test.", this);
            return false;
        }

        if (TestButton == null)
        {
            TestTMpro.SetText("TestButton not found");
            Debug.LogError("TestButton is not assigned and could not be found by name.", this);
            return false;
        }

        if (CameraTransform == null)
        {
            TestTMpro.SetText("Camera not found");
            Debug.LogError("Camera Transform is not assigned to Test.", this);
            return false;
        }

        if (TapNotePrefab == null || NoteField == null)
        {
            TestTMpro.SetText("Note setup missing");
            Debug.LogError("Tap Note prefab or NoteField is not assigned to Test.", this);
            return false;
        }

        if (NoteJudgementSystem == null)
        {
            NoteJudgementSystem = FindFirstObjectByType<NoteJudgementSystem>();
        }

        if (NoteJudgementSystem == null)
        {
            TestTMpro.SetText("Judgement system not found");
            Debug.LogError("NoteJudgementSystem was not found by Test.", this);
            return false;
        }

        playCanvasTransform = NoteField.parent;
        if (playCanvasTransform == null)
        {
            TestTMpro.SetText("Play Canvas not found");
            Debug.LogError("NoteField must be a child of Play Canvas.", this);
            return false;
        }

        if (double.IsNaN(SpeedMultiplier) || double.IsInfinity(SpeedMultiplier) || SpeedMultiplier <= 0d)
        {
            TestTMpro.SetText("Invalid speed multiplier");
            Debug.LogError("Speed Multiplier must be greater than zero.", this);
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

    private void FindTestButtonIfNeeded()
    {
        if (TestButton != null)
        {
            return;
        }

        GameObject buttonObject = GameObject.Find("TestButton");
        if (buttonObject == null)
        {
            buttonObject = GameObject.Find("Test Button");
        }

        if (buttonObject != null)
        {
            TestButton = buttonObject.GetComponent<Button>();
        }
    }

    private bool PlaceQuarterNotes()
    {
        for (int i = NoteField.childCount - 1; i >= 0; i--)
        {
            Destroy(NoteField.GetChild(i).gameObject);
        }

        double songDurationMs = gameManager.GamePlay.SongDurationMs;
        int noteCount = (int)Math.Ceiling(songDurationMs / QuarterNoteMs);
        var notes = new List<NoteData>(noteCount);

        NoteJudgementSystem.ClearRegisteredNoteViews();

        for (int noteIndex = 0; noteIndex < noteCount; noteIndex++)
        {
            int lineIndex = noteIndex % LineXPositions.Length;
            double hitTimeMs = noteIndex * QuarterNoteMs;
            double correctedHitTimeMs = hitTimeMs + NoteCorrectionMs;
            string noteId = $"test-quarter-{noteIndex:0000}";

            GameObject noteObject = Instantiate(TapNotePrefab, NoteField, false);
            noteObject.name = $"Quarter Note {noteIndex:0000} - Line {lineIndex + 1}";
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
            var noteData = new NoteData(
                noteId,
                NoteType.Tap,
                lineIndex,
                storedHitTimeMs,
                0L);

            notes.Add(noteData);
            NoteJudgementSystem.RegisterNoteView(noteId, noteObject);
        }

        if (!NoteJudgementSystem.Initialize(
                LineXPositions.Length,
                notes,
                NoteCorrectionMs))
        {
            TestTMpro.SetText("Judgement setup failed");
            Debug.LogError("Test notes could not initialize NoteJudgementSystem.", this);
            return false;
        }

        Debug.Log(
            $"Placed {noteCount} quarter notes at {Bpm:0.###} BPM " +
            $"with the line pattern 1, 2, 3, 4, {NoteCorrectionMs:0.###} ms correction, " +
            $"and {EffectiveSpeedMultiplier:0.###}x effective speed " +
            $"from a {SpeedMultiplier:0.###} multiplier.",
            this);

        return true;
    }
}
