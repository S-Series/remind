using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using REmind.Gameplay.Demo;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class ResultScenePresenterTests
    {
        [Test]
        public void Present_UsesTheSameActualStatusForBothCaptionsAndFourJudgementRows()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(((Array)fixture.Field("judgementCounts")).Length, Is.EqualTo(4));
                Assert.That(((Array)fixture.Field("judgementPercents")).Length, Is.EqualTo(4));
                fixture.Bind();
                var cases = new[]
                {
                    (Auto: true, AllPerfect: true, Failed: false, Cleared: true, Caption: "AUTO PLAY"),
                    (Auto: false, AllPerfect: true, Failed: false, Cleared: true, Caption: "ALL PERFECT"),
                    (Auto: false, AllPerfect: false, Failed: true, Cleared: false, Caption: "FAILED"),
                    (Auto: false, AllPerfect: false, Failed: false, Cleared: true, Caption: "CLEAR"),
                    (Auto: false, AllPerfect: false, Failed: false, Cleared: false, Caption: "FINISHED")
                };
                foreach (var sample in cases)
                {
                    fixture.Presenter.Present(new GameResultSnapshot("song", "hard", 750000, 1000000,
                        RankGrade.B, sample.AllPerfect ? 5 : 1, sample.AllPerfect ? 5 : 2,
                        sample.AllPerfect ? 0 : 3, sample.AllPerfect ? 0 : 4,
                        7, 10, sample.Cleared, sample.Failed, sample.Auto));
                    Assert.That(fixture.Text("rankCaption"), Is.EqualTo(sample.Caption));
                    Assert.That(fixture.Text("clearTypeValue"), Is.EqualTo(sample.Caption));
                }
                Assert.That(fixture.RowTexts("judgementCounts"), Is.EqualTo(new[] { "1", "2", "3", "4" }));
                Assert.That(fixture.RowTexts("judgementPercents"), Is.EqualTo(new[] { "10.0%", "20.0%", "30.0%", "40.0%" }));
                Assert.That(fixture.Text("scoreValue"), Is.EqualTo("750,000"));
                Assert.That(fixture.Text("comboValue"), Is.EqualTo("7 / 10"));
                Assert.That(fixture.Text("accuracyValue"), Is.EqualTo("75.0%"));
                foreach (var selection in new[]
                {
                    (Id: "normal", Slot: 1, Text: "NORMAL   6"),
                    (Id: "extra", Slot: 3, Text: "LUMINARY   15"),
                    (Id: "chaos", Slot: 3, Text: "CHAOS   12"),
                    (Id: "easy", Slot: 0, Text: "EASY   3"),
                    (Id: "hard", Slot: 2, Text: "HARD   9")
                })
                {
                    fixture.Presenter.Present(new GameResultSnapshot("song", selection.Id,
                        750000, 1000000, RankGrade.B, 1, 2, 3, 4, 7, 10, true, false));
                    fixture.AssertSelectedDifficulty(selection.Slot, selection.Text);
                }
            }
        }

        [Test]
        public void MissingResult_ClearsTheClearTypeAndAllFourJudgementRows()
        {
            using (var fixture = new Fixture())
            {
                fixture.Bind();
                fixture.Presenter.Present(new GameResultSnapshot("song", "extra", 1000000, 1000000,
                    RankGrade.S, 5, 5, 0, 0, 10, 10, true, false));
                typeof(ResultScenePresenter).GetMethod("PresentMissingResult", Fixture.Hidden)
                    .Invoke(fixture.Presenter, null);
                Assert.That(fixture.Text("clearTypeValue"), Is.EqualTo("—"));
                Assert.That(fixture.Text("rankCaption"), Is.Empty);
                Assert.That(fixture.RowTexts("judgementCounts"), Is.EqualTo(new[] { "—", "—", "—", "—" }));
                Assert.That(fixture.RowTexts("judgementPercents"), Is.EqualTo(new[] { "—", "—", "—", "—" }));
                Assert.That(fixture.Text("scoreValue"), Is.EqualTo("—"));
                Assert.That(fixture.Text("comboValue"), Is.EqualTo("—"));
                Assert.That(fixture.Text("accuracyValue"), Is.EqualTo("—"));
                Assert.That(((Array)fixture.Field("difficultyLabels")).Cast<Component>()
                    .All(label => !label.gameObject.activeSelf), Is.True);
                Assert.That(((Component)fixture.Field("selectedDifficulty")).gameObject.activeSelf, Is.False);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            private static readonly Type LabelType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
            private static readonly Type TextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
            private readonly GameObject root = new GameObject("Result Presenter Test");
            private MusicCatalog catalog;
            public ResultScenePresenter Presenter { get; }

            public Fixture()
            {
                root.SetActive(false);
                Presenter = root.AddComponent<ResultScenePresenter>();
            }

            public object Field(string name) => typeof(ResultScenePresenter).GetField(name, Hidden).GetValue(Presenter);

            public void Bind()
            {
                foreach (string name in new[] { "rankCaption", "clearTypeValue", "scoreValue", "comboValue", "accuracyValue" })
                    typeof(ResultScenePresenter).GetField(name, Hidden).SetValue(Presenter, NewLabel(name));
                foreach (string name in new[] { "judgementCounts", "judgementPercents" })
                {
                    Array labels = Array.CreateInstance(TextType, 4);
                    for (int i = 0; i < labels.Length; i++) labels.SetValue(NewLabel(name + i), i);
                    typeof(ResultScenePresenter).GetField(name, Hidden).SetValue(Presenter, labels);
                }
                Array badges = Array.CreateInstance(TextType, 4);
                for (int i = 0; i < badges.Length; i++)
                {
                    Component badge = NewLabel("Difficulty" + i);
                    ((RectTransform)badge.transform).anchoredPosition = new Vector2(20 + i * 10, 30 + i * 5);
                    badges.SetValue(badge, i);
                }
                typeof(ResultScenePresenter).GetField("difficultyLabels", Hidden).SetValue(Presenter, badges);
                var marker = new GameObject("Selected Difficulty", typeof(RectTransform));
                marker.transform.SetParent(root.transform, false);
                Component image = marker.AddComponent(Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true));
                typeof(ResultScenePresenter).GetField("selectedDifficulty", Hidden).SetValue(Presenter, image);
                string[] ids = { "easy", "normal", "hard", "chaos", "extra" };
                var difficulties = new MusicDifficultyEntry[ids.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    difficulties[i] = new MusicDifficultyEntry();
                    typeof(MusicDifficultyEntry).GetField("difficultyId", Hidden).SetValue(difficulties[i], ids[i]);
                    typeof(MusicDifficultyEntry).GetField("level", Hidden).SetValue(difficulties[i], (i + 1) * 3);
                    typeof(MusicDifficultyEntry).GetField("extraName", Hidden).SetValue(difficulties[i], i == 4 ? "LUMINARY" : null);
                }
                var entry = new MusicCatalogEntry();
                typeof(MusicCatalogEntry).GetField("musicId", Hidden).SetValue(entry, "song");
                typeof(MusicCatalogEntry).GetField("difficulties", Hidden).SetValue(entry, difficulties);
                catalog = ScriptableObject.CreateInstance<MusicCatalog>();
                typeof(MusicCatalog).GetField("songs", Hidden).SetValue(catalog, new[] { entry });
                typeof(ResultScenePresenter).GetField("catalog", Hidden).SetValue(Presenter, catalog);
            }

            private Component NewLabel(string name)
            {
                var obj = new GameObject(name, typeof(RectTransform));
                obj.transform.SetParent(root.transform, false);
                return obj.AddComponent(LabelType);
            }

            public string Text(string name) => (string)TextType.GetProperty("text").GetValue(Field(name));
            public string[] RowTexts(string name) => ((Array)Field(name)).Cast<object>()
                .Select(label => (string)TextType.GetProperty("text").GetValue(label)).ToArray();
            public void AssertSelectedDifficulty(int slot, string text)
            {
                Component[] labels = ((Array)Field("difficultyLabels")).Cast<Component>().ToArray();
                Assert.That(labels.Select(label => label.gameObject.activeSelf),
                    Is.EqualTo(Enumerable.Range(0, labels.Length).Select(index => index == slot)));
                Assert.That(TextType.GetProperty("text").GetValue(labels[slot]), Is.EqualTo(text));
                Component marker = (Component)Field("selectedDifficulty");
                Assert.That(marker.gameObject.activeSelf, Is.True);
                Vector2 expected = ((RectTransform)labels[slot].transform).anchoredPosition - new Vector2(14, 0);
                Assert.That(((RectTransform)marker.transform).anchoredPosition, Is.EqualTo(expected));
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (catalog) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }
}
