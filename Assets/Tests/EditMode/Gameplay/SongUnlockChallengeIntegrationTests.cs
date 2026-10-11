using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using REmind.Charting;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Characters;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace REmind.Gameplay.Tests
{
    public sealed class SongUnlockChallengeIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void AuthoredChallengeAndBootstrapReferenceUseSpecialHard()
        {
            GameRuleConfig rule = AssetDatabase.LoadAssetAtPath<GameRuleConfig>(
                "Assets/Data/Music Unlock/designant/Rule.asset");
            SongUnlockChallengeDefinition challenge =
                AssetDatabase.LoadAssetAtPath<SongUnlockChallengeDefinition>(
                    "Assets/Data/Music Unlock/designant/Config.asset");
            MusicUnlockLibrary library = AssetDatabase.LoadAssetAtPath<MusicUnlockLibrary>(
                "Assets/Data/Music Unlock/MusicUnlockLibrary.asset");
            Assert.IsNotNull(rule);
            Assert.IsNotNull(challenge);
            Assert.IsNotNull(library);
            library.ValidateForUse();
            CollectionAssert.AreEqual(new[] { challenge }, library.Entries);
            Assert.AreSame(rule, challenge.SpecialHardRule);
            Assert.AreEqual(HealthGaugeType.Hard, rule.GaugeType);
            Assert.AreEqual(100, rule.MaxHealth);
            Assert.AreEqual(100, rule.InitialHealth);
            Assert.AreEqual(1, rule.ClearHealth);
            Assert.IsTrue(rule.FailImmediately);
            Assert.IsFalse(rule.ContinueAfterFail);
            Assert.AreEqual(1000, (int)GetField(rule.SpecialHardGauge,
                "rfReferenceCount"));
            Assert.That(rule.SpecialHardGauge.GetDelta(
                JudgeResult.PerfectPlus, 50d, 100, 1000),
                Is.EqualTo(0.25d).Within(0.00001d));
            Assert.That(rule.SpecialHardGauge.GetDelta(
                JudgeResult.Perfect, 50d, 100, 1000),
                Is.EqualTo(0.25d).Within(0.00001d));
            Assert.That(rule.SpecialHardGauge.GetDelta(
                JudgeResult.Great, 30d, 100, 1000),
                Is.EqualTo(-0.25d).Within(0.00001d));
            Assert.That(rule.SpecialHardGauge.GetDelta(
                JudgeResult.Miss, 30d, 100, 1000),
                Is.EqualTo(-3.75d).Within(0.00001d));
            foreach (string difficulty in new[]
            {
                "easy", "normal", "hard", "chaos", "extra"
            })
            {
                Assert.IsTrue(challenge.AppliesTo(
                    "lament_rain", difficulty, false), difficulty);
                Assert.IsTrue(library.TrySelectChallenge("lament_rain", difficulty,
                    (_, __) => false, out SongUnlockChallengeDefinition selected,
                    out string error), error);
                Assert.AreSame(challenge, selected, difficulty);
            }

            const string scenePath = "Assets/Scenes/Bootstrap.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForTest = !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(scenePath,
                    OpenSceneMode.Additive);
            try
            {
                AppRoot app = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.TryGetComponent(out AppRoot candidate))
                    {
                        app = candidate;
                        break;
                    }
                Assert.IsNotNull(app);
                Assert.AreSame(library, GetField(app,
                    "musicUnlockLibrary"));
            }
            finally
            {
                if (openedForTest)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void GameErrorButtonInvokesResultSaveRetry()
        {
            const string scenePath = "Assets/Scenes/Game.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForTest = !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(scenePath,
                    OpenSceneMode.Additive);
            try
            {
                Button button = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Button candidate in root.GetComponentsInChildren<Button>(true))
                        if (candidate.name == "ErrorSongSelectButton")
                            button = candidate;
                Assert.IsNotNull(button);
                Assert.AreEqual("ResolveError",
                    button.onClick.GetPersistentMethodName(0));
                Assert.IsNotNull(button.onClick.GetPersistentTarget(0));
            }
            finally
            {
                if (openedForTest)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("lament_rain", "hard", "designant")]
        [TestCase("second_source", "hard", "second_target")]
        [TestCase("second_source", "normal", null)]
        [TestCase("unrelated", "hard", null)]
        public void MultipleDefinitionsSelectOnlyMatchingSongAndDifficulty(
            string musicId, string difficultyId, string expectedTarget)
        {
            using (var fixture = new Fixture())
            {
                SetField(fixture.SecondChallenge, "difficultyId", "hard");
                SetField(fixture.Library, "entries",
                    new[] { fixture.SecondChallenge, fixture.Challenge });
                string libraryBefore = JsonUtility.ToJson(fixture.Library);
                string challengeBefore = JsonUtility.ToJson(fixture.SecondChallenge);

                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    musicId, difficultyId, out string error), error);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out CharacterAbilityDefinition ability,
                    out GameRuleConfig rule, out string target));
                Assert.AreEqual(expectedTarget, target);
                Assert.AreEqual(expectedTarget == null, ability != null);
                Assert.AreSame(expectedTarget == null ? fixture.NormalRule :
                    fixture.HardRule, rule);
                Assert.AreEqual(libraryBefore, JsonUtility.ToJson(fixture.Library));
                Assert.AreEqual(challengeBefore, JsonUtility.ToJson(fixture.SecondChallenge));
            }
        }

        [TestCase("hard", "designant")]
        [TestCase("normal", "second_target")]
        public void SameSourceWithDifferentDifficultiesSelectsOneDefinition(
            string difficultyId, string expectedTarget)
        {
            using (var fixture = new Fixture())
            {
                SetField(fixture.Challenge, "difficultyId", "hard");
                SetField(fixture.SecondChallenge, "sourceMusicId", "lament_rain");
                SetField(fixture.SecondChallenge, "difficultyId", "normal");
                SetField(fixture.Library, "entries",
                    new[] { fixture.SecondChallenge, fixture.Challenge });

                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", difficultyId, out string error), error);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out _, out _, out string target));
                Assert.AreEqual(expectedTarget, target);
            }
        }

        [Test]
        public void PartialUnlockExcludesOnlyTheSelectedDifficultyFromCandidates()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "normal", out string error), error);
                GameAttemptStartSnapshot attempt = fixture.Start(false);
                fixture.App.ActivateAttempt(attempt);
                fixture.App.PublishResult(Result(attempt, true, false, false));
                SetField(fixture.SecondChallenge, "sourceMusicId", "lament_rain");
                SetField(fixture.Library, "entries",
                    new[] { fixture.Challenge, fixture.SecondChallenge });

                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "normal", out error), error);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out _, out _, out string target));
                Assert.AreEqual("second_target", target);
                Assert.IsFalse(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out error));
                StringAssert.Contains("여러 해금 도전", error);
            }
        }

        [TestCase("designant", false)]
        [TestCase("designant", true)]
        [TestCase("second_target", false)]
        [TestCase("second_target", true)]
        public void OverlappingDefinitionsBlockStartRegardlessOfOrder(
            string secondTarget, bool reversed)
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "previous_song", "easy", out string error), error);
                SetField(fixture.SecondChallenge, "sourceMusicId", "lament_rain");
                SetField(fixture.SecondChallenge, "targetMusicId", secondTarget);
                SetField(fixture.Library, "entries", reversed
                    ? new[] { fixture.SecondChallenge, fixture.Challenge }
                    : new[] { fixture.Challenge, fixture.SecondChallenge });

                Assert.IsFalse(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out error));
                StringAssert.Contains("여러 해금 도전", error);
                Assert.IsTrue(fixture.App.TryGetSelectedSong(out AppRoot.SongSelection song));
                Assert.AreEqual("previous_song", song.MusicId);
                Assert.AreEqual("easy", song.DifficultyId);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out CharacterAbilityDefinition ability,
                    out GameRuleConfig rule, out string target));
                Assert.IsNotNull(ability);
                Assert.AreSame(fixture.NormalRule, rule);
                Assert.IsNull(target);
            }
        }

        [TestCase("null-list")]
        [TestCase("null-entry")]
        [TestCase("duplicate")]
        [TestCase("invalid-rule")]
        public void InvalidLibraryBlocksSelectionThroughExistingErrorPath(string invalid)
        {
            using (var fixture = new Fixture())
            {
                if (invalid == "null-list") SetField(fixture.Library, "entries", null);
                else if (invalid == "null-entry")
                    SetField(fixture.Library, "entries",
                        new[] { fixture.Challenge, null });
                else if (invalid == "duplicate")
                    SetField(fixture.Library, "entries",
                        new[] { fixture.Challenge, fixture.Challenge });
                else SetField(fixture.Challenge, "specialHardRule", fixture.NormalRule);

                LogAssert.Expect(LogType.Error, new Regex("Music unlock library is invalid"));
                Assert.IsFalse(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out string error));
                Assert.AreEqual("해금 도전 규칙을 확인할 수 없습니다.", error);
                Assert.IsFalse(fixture.App.TryGetSelectedSong(out _));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingOrEmptyLibraryKeepsOrdinaryCharacterSelection(bool disconnected)
        {
            using (var fixture = new Fixture())
            {
                if (disconnected) SetField(fixture.App, "musicUnlockLibrary", null);
                else SetField(fixture.Library, "entries",
                    Array.Empty<SongUnlockChallengeDefinition>());

                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out string error), error);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out CharacterAbilityDefinition ability,
                    out GameRuleConfig rule, out string target));
                Assert.IsNotNull(ability);
                Assert.AreSame(fixture.NormalRule, rule);
                Assert.IsNull(target);
            }
        }

        [Test]
        public void SecondTargetStaysLockedAndItsStartedResultKeepsFrozenTarget()
        {
            using (var fixture = new Fixture())
            {
                SetField(fixture.Library, "entries",
                    new[] { fixture.Challenge, fixture.SecondChallenge });
                Assert.IsFalse(fixture.App.TrySelectSongWithEquippedCharacter(
                    "second_target", "hard", out string error));
                Assert.AreEqual("아직 해금되지 않은 난이도입니다.", error);
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "second_source", "normal", out error), error);
                GameAttemptStartSnapshot attempt = fixture.Start(false);
                fixture.App.ActivateAttempt(attempt);
                SetField(fixture.SecondChallenge, "targetMusicId", "changed_target");
                SetField(fixture.Library, "entries", new[] { fixture.Challenge });
                fixture.App.PublishResult(Result(attempt, true, false, false));

                LocalPlayerDataStore saved = LocalPlayerDataStore.Load(fixture.SavePath);
                Assert.IsTrue(saved.IsSongDifficultyUnlocked("second_target", "normal"));
                Assert.IsFalse(saved.IsSongDifficultyUnlocked("second_target", "hard"));
                Assert.IsFalse(saved.HasAnyUnlockedDifficulty("designant"));
                Assert.IsFalse(saved.HasAnyUnlockedDifficulty("changed_target"));
                SetField(fixture.SecondChallenge, "targetMusicId", "second_target");
                SetField(fixture.Library, "entries",
                    new[] { fixture.Challenge, fixture.SecondChallenge });
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "second_target", "normal", out error), error);
                Assert.IsFalse(fixture.App.TrySelectSongWithEquippedCharacter(
                    "second_target", "hard", out error));
                Assert.AreEqual("아직 해금되지 않은 난이도입니다.", error);
            }
        }

        [Test]
        public void LockedTargetUsesSpecialHardAndClearPersistsUnlock()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out string error), error);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out CharacterAbilityDefinition ability,
                    out GameRuleConfig selectedRule, out string target));
                Assert.IsNull(ability);
                Assert.AreSame(fixture.HardRule, selectedRule);
                Assert.AreEqual("designant", target);

                GameAttemptStartSnapshot attempt = fixture.Start(false);
                Assert.AreEqual("designant", attempt.UnlockTargetMusicId);
                fixture.App.ActivateAttempt(attempt);
                GameResultSnapshot result = Result(attempt, true, false, false);
                fixture.App.PublishResult(result);
                LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(fixture.SavePath);
                Assert.IsFalse(reloaded.IsSongUnlocked("designant"));
                Assert.IsTrue(reloaded.IsSongDifficultyUnlocked("designant", "easy"));
                Assert.IsTrue(reloaded.IsSongDifficultyUnlocked("designant", "normal"));
                Assert.IsTrue(reloaded.IsSongDifficultyUnlocked("designant", "hard"));
                Assert.IsFalse(reloaded.IsSongDifficultyUnlocked("designant", "chaos"));
                Assert.IsFalse(reloaded.IsSongDifficultyUnlocked("designant", "extra"));
                Assert.IsTrue(reloaded.TryGetProgress("lament_rain", "hard",
                    out ChartProgressSnapshot progress));
                Assert.IsTrue(progress.Cleared);
                Assert.AreEqual(1, progress.PlayCount);
                Assert.Throws<InvalidOperationException>(() =>
                    fixture.App.PublishResult(result));

                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out error), error);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out ability, out selectedRule, out target));
                Assert.IsNotNull(ability);
                Assert.AreSame(fixture.NormalRule, selectedRule);
                Assert.IsNull(target);
            }
        }

        [Test]
        public void NormalClearUnlocksOnlyEasyAndNormalAndKeepsUpperChallenge()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsFalse(fixture.App.TrySelectSongWithEquippedCharacter(
                    "designant", "normal", out _));
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "normal", out string error), error);
                GameAttemptStartSnapshot attempt = fixture.Start(false);
                fixture.App.ActivateAttempt(attempt);
                fixture.App.PublishResult(Result(attempt, true, false, false));

                LocalPlayerDataStore saved = LocalPlayerDataStore.Load(fixture.SavePath);
                Assert.IsTrue(saved.HasAnyUnlockedDifficulty("designant"));
                Assert.IsTrue(saved.IsSongDifficultyUnlocked("designant", "easy"));
                Assert.IsTrue(saved.IsSongDifficultyUnlocked("designant", "normal"));
                Assert.IsFalse(saved.IsSongDifficultyUnlocked("designant", "hard"));
                Assert.IsFalse(saved.IsSongDifficultyUnlocked("designant", "chaos"));
                Assert.IsFalse(saved.IsSongDifficultyUnlocked("designant", "extra"));
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "designant", "normal", out error), error);
                Assert.IsFalse(fixture.App.TrySelectSongWithEquippedCharacter(
                    "designant", "hard", out _));
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out error), error);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out CharacterAbilityDefinition ability,
                    out _, out string target));
                Assert.IsNull(ability);
                Assert.AreEqual("designant", target);
            }
        }

        [Test]
        public void ExtraClearUnlocksEveryDifficulty()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "extra", out string error), error);
                GameAttemptStartSnapshot attempt = fixture.Start(false);
                fixture.App.ActivateAttempt(attempt);
                fixture.App.PublishResult(Result(attempt, true, false, false));

                LocalPlayerDataStore saved = LocalPlayerDataStore.Load(fixture.SavePath);
                Assert.IsTrue(saved.IsSongUnlocked("designant"));
                foreach (string difficulty in new[]
                {
                    "easy", "normal", "hard", "chaos", "extra"
                })
                    Assert.IsTrue(saved.IsSongDifficultyUnlocked(
                        "designant", difficulty), difficulty);
            }
        }

        [Test]
        public void LowerClearCannotRemoveAnEarlierHigherUnlock()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out string error), error);
                GameAttemptStartSnapshot hardAttempt = fixture.Start(false);
                fixture.App.ActivateAttempt(hardAttempt);
                fixture.App.PublishResult(Result(hardAttempt, true, false, false));

                LocalPlayerDataStore store = LocalPlayerDataStore.Load(fixture.SavePath);
                var normalAttempt = new GameAttemptStartSnapshot(Guid.NewGuid(),
                    "lament_rain", "normal", false,
                    CharacterGaugeRuleSnapshot.Capture(fixture.HardRule),
                    null, "designant");
                store.RecordAndSaveResult(Result(normalAttempt, true, false, false),
                    "designant", fixture.Items);
                store = LocalPlayerDataStore.Load(fixture.SavePath);
                Assert.IsTrue(store.IsSongDifficultyUnlocked("designant", "hard"));
                Assert.IsFalse(store.IsSongDifficultyUnlocked("designant", "chaos"));
            }
        }

        [Test]
        public void AutoPlayFailureAndWrongDifficultyDoNotUnlock()
        {
            using (var fixture = new Fixture())
            {
                SetField(fixture.Challenge, "difficultyId", "hard");
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "easy", out string error), error);
                Assert.IsTrue(fixture.App.TryGetSelectedPlayConditions(
                    out CharacterAbilityDefinition ability,
                    out GameRuleConfig rule, out string target));
                Assert.IsNotNull(ability);
                Assert.AreSame(fixture.NormalRule, rule);
                Assert.IsNull(target);

                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out error), error);
                GameAttemptStartSnapshot autoAttempt = fixture.Start(true);
                fixture.App.ActivateAttempt(autoAttempt);
                fixture.App.PublishResult(Result(autoAttempt, true, false, true));
                Assert.IsFalse(LocalPlayerDataStore.Load(fixture.SavePath)
                    .IsSongDifficultyUnlocked("designant", "hard"));

                GameAttemptStartSnapshot failedAttempt = fixture.Start(false);
                fixture.App.ActivateAttempt(failedAttempt);
                fixture.App.PublishResult(Result(failedAttempt, false, true, false));
                Assert.IsFalse(LocalPlayerDataStore.Load(fixture.SavePath)
                    .IsSongDifficultyUnlocked("designant", "hard"));
            }
        }

        [Test]
        public void FailedResultCannotGrantClearRewardsEvenWhenClearFlagIsSet()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out string error), error);
                GameAttemptStartSnapshot attempt = fixture.Start(false);
                fixture.App.ActivateAttempt(attempt);
                fixture.App.PublishResult(Result(attempt, true, true, false));

                LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(fixture.SavePath);
                Assert.IsFalse(reloaded.IsSongDifficultyUnlocked("designant", "hard"));
                Assert.IsTrue(reloaded.TryGetProgress("lament_rain", "hard",
                    out ChartProgressSnapshot progress));
                Assert.IsFalse(progress.Cleared);
                Assert.AreEqual(1, progress.PlayCount);
                Assert.AreEqual(0, reloaded.MemoryFragmentCount);
                Assert.IsFalse(fixture.App.LastResultWasFirstClear);
            }
        }

        [Test]
        public void FailedSaveKeepsAttemptAndCanRetryWithoutDuplicateRewards()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out string error), error);
                GameAttemptStartSnapshot attempt = fixture.Start(false);
                fixture.App.ActivateAttempt(attempt);
                GameResultSnapshot result = Result(attempt, true, false, false);
                string temporaryPath = fixture.SavePath + ".tmp";
                Directory.CreateDirectory(temporaryPath);
                try
                {
                    Assert.Throws<UnauthorizedAccessException>(() =>
                        fixture.App.PublishResult(result));
                    Assert.IsFalse(fixture.App.TryTakeResult(out _));
                    Assert.IsFalse(fixture.App.TryGetChartProgress(
                        "lament_rain", "hard", out _));
                    Assert.AreEqual(0, fixture.App.MemoryFragmentCount);
                    Assert.IsFalse(LocalPlayerDataStore.Load(fixture.SavePath)
                        .IsSongDifficultyUnlocked("designant", "hard"));
                }
                finally { Directory.Delete(temporaryPath); }

                fixture.App.PublishResult(result);
                LocalPlayerDataStore saved = LocalPlayerDataStore.Load(fixture.SavePath);
                Assert.IsTrue(saved.IsSongDifficultyUnlocked("designant", "hard"));
                Assert.IsTrue(saved.TryGetProgress("lament_rain", "hard",
                    out ChartProgressSnapshot progress));
                Assert.AreEqual(1, progress.PlayCount);
                Assert.AreEqual(1, saved.MemoryFragmentCount);
            }
        }

        [Test]
        public void GameErrorActionRetriesTheSameFinishedResult()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out string error), error);
                GameAttemptStartSnapshot attempt = fixture.Start(false);
                fixture.App.ActivateAttempt(attempt);
                var game = new GameObject("Save retry Game scene");
                game.SetActive(false);
                string temporaryPath = fixture.SavePath + ".tmp";
                try
                {
                    var playback = game.AddComponent<GamePlay>();
                    var rule = game.AddComponent<DefaultGameRule>();
                    typeof(GameRule).GetField("config", Private)
                        .SetValue(rule, fixture.HardRule);
                    var manager = game.AddComponent<GameManager>();
                    SetField(manager, "gamePlay", playback);
                    SetField(manager, "gameRule", rule);
                    var chart = game.AddComponent<GameplayChartSessionController>();
                    var prepared = (PreparedGameplayChart)
                        FormatterServices.GetUninitializedObject(
                            typeof(PreparedGameplayChart));
                    SetField(prepared, "<Metadata>k__BackingField",
                        new GameplayChartMetadata("lament_rain", "hard", "", ""));
                    typeof(GameplayChartSessionController)
                        .GetProperty(nameof(GameplayChartSessionController.CurrentChart))
                        .SetValue(chart, prepared);
                    var state = game.AddComponent<GameplaySessionState>();
                    SetField(state, "<IsCleared>k__BackingField", true);
                    SetField(state, "totalNoteCount", 1);
                    var judgement = game.AddComponent<REmind.Gameplay.Input.Judgement.NoteJudgementSystem>();
                    var flow = game.AddComponent<GameFlowController>();
                    SetField(flow, "gameManager", manager);
                    SetField(flow, "chartSession", chart);
                    SetField(flow, "sessionState", state);
                    SetField(flow, "judgementSystem", judgement);
                    SetField(flow, "activeAttempt", attempt);

                    Directory.CreateDirectory(temporaryPath);
                    LogAssert.Expect(LogType.Error,
                        new Regex("Could not save gameplay result"));
                    typeof(GameFlowController).GetMethod("FinishSong", Private)
                        .Invoke(flow, null);
                    Assert.AreEqual("Error", GetField(flow, "screen").ToString());
                    var pending = (GameResultSnapshot)GetField(flow,
                        "pendingSaveResult");
                    Assert.IsNotNull(pending);
                    Assert.AreSame(attempt, pending.AttemptStart);
                    Assert.IsFalse(fixture.App.TryTakeResult(out _));
                    Assert.AreEqual(0, fixture.App.MemoryFragmentCount);

                    LogAssert.Expect(LogType.Error,
                        new Regex("Could not retry gameplay result save"));
                    flow.ResolveError();
                    Assert.AreSame(pending, GetField(flow,
                        "pendingSaveResult"));
                    Directory.Delete(temporaryPath);

                    flow.ResolveError();
                    Assert.IsNull(GetField(flow, "pendingSaveResult"));
                    Assert.IsTrue(fixture.App.TryTakeResult(out GameResultSnapshot savedResult));
                    Assert.AreSame(pending, savedResult);
                    LocalPlayerDataStore saved = LocalPlayerDataStore.Load(
                        fixture.SavePath);
                    Assert.IsTrue(saved.IsSongDifficultyUnlocked(
                        "designant", "hard"));
                    Assert.IsTrue(saved.TryGetProgress("lament_rain", "hard",
                        out ChartProgressSnapshot progress));
                    Assert.AreEqual(1, progress.PlayCount);
                    Assert.AreEqual(1, saved.MemoryFragmentCount);
                    Assert.Throws<InvalidOperationException>(() =>
                        fixture.App.PublishResult(savedResult));
                }
                finally
                {
                    if (Directory.Exists(temporaryPath))
                        Directory.Delete(temporaryPath);
                    UnityEngine.Object.DestroyImmediate(game);
                }
            }
        }

        [Test]
        public void UnlockTokenRequiresHardWithoutCharacterAbility()
        {
            using (var fixture = new Fixture())
            {
                Assert.Throws<ArgumentException>(() =>
                    new GameAttemptStartSnapshot(Guid.NewGuid(), "lament_rain",
                        "hard", false,
                        CharacterGaugeRuleSnapshot.Capture(fixture.NormalRule),
                        null, "designant"));
                SetField(fixture.HardRule, "useSpecialHardGauge", false);
                Assert.Throws<ArgumentException>(() =>
                    new GameAttemptStartSnapshot(Guid.NewGuid(), "lament_rain",
                        "hard", false,
                        CharacterGaugeRuleSnapshot.Capture(fixture.HardRule),
                        null, "designant"));
                SetField(fixture.HardRule, "specialHardGauge",
                    new SpecialHardGaugeSettings());
                SetField(fixture.HardRule, "useSpecialHardGauge", true);
                Assert.Throws<ArgumentException>(() =>
                    new GameAttemptStartSnapshot(Guid.NewGuid(), "lament_rain",
                        "hard", false,
                        CharacterGaugeRuleSnapshot.Capture(fixture.HardRule),
                        null, "lament_rain"));
            }
        }

        [Test]
        public void GameStartCommitsSpecialHardAndFreezesUnlockTarget()
        {
            using (var fixture = new Fixture())
            {
                Assert.IsTrue(fixture.App.TrySelectSongWithEquippedCharacter(
                    "lament_rain", "hard", out string error), error);
                var game = new GameObject("Unlock challenge Game start");
                game.SetActive(false);
                try
                {
                    var playback = game.AddComponent<GamePlay>();
                    var rule = game.AddComponent<DefaultGameRule>();
                    typeof(GameRule).GetField("config", Private)
                        .SetValue(rule, fixture.NormalRule);
                    var manager = game.AddComponent<GameManager>();
                    SetField(manager, "gamePlay", playback);
                    SetField(manager, "gameRule", rule);
                    var chart = game.AddComponent<GameplayChartSessionController>();
                    var prepared = (PreparedGameplayChart)
                        FormatterServices.GetUninitializedObject(
                            typeof(PreparedGameplayChart));
                    SetField(prepared, "<Metadata>k__BackingField",
                        new GameplayChartMetadata("lament_rain", "hard", "", ""));
                    typeof(GameplayChartSessionController)
                        .GetProperty(nameof(GameplayChartSessionController.CurrentChart))
                        .SetValue(chart, prepared);
                    var flow = game.AddComponent<GameFlowController>();
                    SetField(flow, "gameManager", manager);
                    SetField(flow, "chartSession", chart);

                    Assert.IsTrue((bool)Invoke(flow, "HandlePlaybackStarting", 0d));
                    Assert.IsTrue((bool)Invoke(flow, "HandlePlaybackCommitting", 0d));
                    Invoke(flow, "HandlePlaybackStarted", 0d);
                    GameAttemptStartSnapshot attempt = flow.CurrentAttempt;
                    Assert.IsNotNull(attempt);
                    Assert.AreEqual(HealthGaugeType.Hard, rule.GaugeType);
                    Assert.AreEqual("designant", attempt.UnlockTargetMusicId);
                    Assert.IsNull(attempt.CharacterAbility);
                    Assert.AreEqual(fixture.HardRule.InitialHealth,
                        attempt.GaugeRule.InitialHealth);
                }
                finally { UnityEngine.Object.DestroyImmediate(game); }
            }
        }

        private static GameResultSnapshot Result(GameAttemptStartSnapshot attempt,
            bool cleared, bool failed, bool auto) =>
            new GameResultSnapshot(attempt.MusicId, attempt.DifficultyId,
                1000d, 1_000_000, RankGrade.D, 1, 0, 0, 0, 1, 1,
                cleared, failed, auto, attempt);

        [Test]
        public void SpecialHardUsesFractionalHealthAndRfReferenceCount()
        {
            var config = ScriptableObject.CreateInstance<GameRuleConfig>();
            var game = new GameObject("Special Hard health");
            game.SetActive(false);
            try
            {
                SetField(config, "gaugeType", HealthGaugeType.Hard);
                SetField(config, "useSpecialHardGauge", true);
                var special = new SpecialHardGaugeSettings();
                SetField(config, "specialHardGauge", special);
                var rule = game.AddComponent<DefaultGameRule>();
                typeof(GameRule).GetField("config", Private).SetValue(rule, config);

                double Apply(double health, JudgeResult result, int count) =>
                    rule.ApplyHealthDelta(health, result,
                        default(RuleContext), count);

                Assert.That(Apply(50d, JudgeResult.Perfect, 1000),
                    Is.EqualTo(50.25d).Within(0.00001d));
                Assert.That(Apply(30.001d, JudgeResult.Great, 1000),
                    Is.EqualTo(29.501d).Within(0.00001d));
                Assert.That(Apply(30d, JudgeResult.Great, 1000),
                    Is.EqualTo(29.75d).Within(0.00001d));
                Assert.That(Apply(30d, JudgeResult.Miss, 1000),
                    Is.EqualTo(26.25d).Within(0.00001d));
                Assert.That(Apply(50d, JudgeResult.Perfect, 100),
                    Is.EqualTo(50.3375d).Within(0.00001d));

                SetField(special, "rfReferenceCount", 400);
                Assert.That(Apply(50d, JudgeResult.Perfect, 400),
                    Is.EqualTo(50.25d).Within(0.00001d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(game);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);

        private static object GetField(object target, string name) =>
            target.GetType().GetField(name, Private).GetValue(target);

        private static object Invoke(object target, string name, double time) =>
            target.GetType().GetMethod(name, Private).Invoke(target,
                new object[] { time });

        private sealed class Fixture : IDisposable
        {
            public readonly string SavePath = Path.Combine(Path.GetTempPath(),
                "remind-song-unlock-" + Guid.NewGuid().ToString("N") + ".json");
            public readonly GameObject Root = new GameObject("Unlock challenge AppRoot");
            public readonly AppRoot App;
            public readonly GameRuleConfig NormalRule =
                ScriptableObject.CreateInstance<GameRuleConfig>();
            public readonly GameRuleConfig HardRule =
                ScriptableObject.CreateInstance<GameRuleConfig>();
            public readonly CharacterDefinitionAsset Character =
                ScriptableObject.CreateInstance<CharacterDefinitionAsset>();
            public readonly CharacterCatalog Catalog =
                ScriptableObject.CreateInstance<CharacterCatalog>();
            public readonly ItemCatalog Items =
                AssetDatabase.LoadAssetAtPath<ItemCatalog>(
                    "Assets/Data/Items/ItemCatalog.asset");
            public readonly SongUnlockChallengeDefinition Challenge =
                ScriptableObject.CreateInstance<SongUnlockChallengeDefinition>();
            public readonly SongUnlockChallengeDefinition SecondChallenge =
                ScriptableObject.CreateInstance<SongUnlockChallengeDefinition>();
            public readonly MusicUnlockLibrary Library =
                ScriptableObject.CreateInstance<MusicUnlockLibrary>();
            private readonly AppRoot previous;

            public Fixture()
            {
                Root.SetActive(false);
                App = Root.AddComponent<AppRoot>();
                previous = AppRoot.Current;
                SetField(HardRule, "gaugeType", HealthGaugeType.Hard);
                SetField(HardRule, "useSpecialHardGauge", true);
                SetField(HardRule, "specialHardGauge",
                    new SpecialHardGaugeSettings());
                SetField(Character, "characterId", "non");
                SetField(Character, "definitionVersion", 1);
                SetField(Character, "displayName", "NON");
                SetField(Character, "englishName", "NON");
                SetField(Character, "gaugeRule", NormalRule);
                SetField(Character, "worldUnlock", 50);
                SetField(Character, "regularCurrency", 50);
                SetField(Character, "hiddenUnlock", 50);
                SetField(Catalog, "defaultCharacterId", "non");
                SetField(Catalog, "statBasis", 50);
                SetField(Catalog, "entries", new[] { Character });
                SetField(Challenge, "sourceMusicId", "lament_rain");
                SetField(Challenge, "targetMusicId", "designant");
                SetField(Challenge, "specialHardRule", HardRule);
                SetField(SecondChallenge, "sourceMusicId", "second_source");
                SetField(SecondChallenge, "targetMusicId", "second_target");
                SetField(SecondChallenge, "specialHardRule", HardRule);
                SetField(Library, "entries", new[] { Challenge });
                var store = LocalPlayerDataStore.Load(SavePath);
                store.EnsureStarterCharacter("non", id =>
                    Catalog.TryResolve(id, out _, out _));
                SetField(App, "playerData", store);
                SetField(App, "characterCatalog", Catalog);
                SetField(App, "itemCatalog", Items);
                SetField(App, "musicUnlockLibrary", Library);
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, App);
            }

            public GameAttemptStartSnapshot Start(bool auto)
            {
                Assert.IsTrue(App.TryGetSelectedSong(out AppRoot.SongSelection song));
                App.TryGetSelectedPlayConditions(out _,
                    out GameRuleConfig rule, out string target);
                return new GameAttemptStartSnapshot(Guid.NewGuid(),
                    song.MusicId, song.DifficultyId, auto,
                    CharacterGaugeRuleSnapshot.Capture(rule), null, target);
            }

            public void Dispose()
            {
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, previous);
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Library);
                UnityEngine.Object.DestroyImmediate(SecondChallenge);
                UnityEngine.Object.DestroyImmediate(Challenge);
                UnityEngine.Object.DestroyImmediate(Catalog);
                UnityEngine.Object.DestroyImmediate(Character);
                UnityEngine.Object.DestroyImmediate(HardRule);
                UnityEngine.Object.DestroyImmediate(NormalRule);
                foreach (string path in new[] { SavePath, SavePath + ".bak",
                    SavePath + ".tmp" })
                    if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
