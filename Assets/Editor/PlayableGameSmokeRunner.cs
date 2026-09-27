using System;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Input.Judgement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch PlayMode smoke for the actual bundled Game scene.</summary>
[InitializeOnLoad]
public static class PlayableGameSmokeRunner
{
    private const string ActiveKey = "REmind.GamePlaySmoke.Active";
    private static int frames;
    private static bool checkedScene;

    static PlayableGameSmokeRunner()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
        frames = 0;
        checkedScene = false;
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.update += Tick;
        Debug.Log("GAME_PLAYMODE_SMOKE_STARTED");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (++frames > 300)
                throw new TimeoutException(
                    "Game scene was not ready after 300 PlayMode frames.");
            DemoPlayController presenter = UnityEngine.Object
                .FindFirstObjectByType<DemoPlayController>();
            if (!presenter || !presenter.IsReady) return;
            if (checkedScene) return;
            checkedScene = true;

            GameplayChartSessionController charts = UnityEngine.Object
                .FindFirstObjectByType<GameplayChartSessionController>();
            NoteJudgementSystem judgement = UnityEngine.Object
                .FindFirstObjectByType<NoteJudgementSystem>();
            GameManager manager = UnityEngine.Object
                .FindFirstObjectByType<GameManager>();
            if (!charts || !charts.IsPrepared || !judgement || !manager ||
                charts.CurrentChart.Snapshot.Notes.Count != 302)
                throw new InvalidOperationException(
                    "The full chart was not prepared in PlayMode.");
            foreach (var note in charts.CurrentChart.Snapshot.Notes)
                if (!judgement.TryGetRegisteredNoteView(note.Id, out _))
                    throw new InvalidOperationException(
                        "Missing note view: " + note.Id);
            double end = 0d;
            foreach (var note in charts.CurrentChart.Snapshot.Notes)
                end = Math.Max(end, note.StartTimeMs +
                    charts.CurrentChart.ChartOffsetMs);
            if (manager.GamePlay.SongDurationMs <= end)
                throw new InvalidOperationException(
                    "Playback ends before the final chart note.");
            if (!manager.StartGame() || !manager.PauseGame() ||
                !manager.ResumeGame() || !manager.RestartGame())
                throw new InvalidOperationException(
                    "Play/Pause/Resume/Restart transition failed.");
            manager.StopGame();
            Debug.Log("GAME_PLAYMODE_SMOKE_PASSED");
            Complete(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Complete(1);
        }
    }

    private static void Complete(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.Exit(code);
    }
}
