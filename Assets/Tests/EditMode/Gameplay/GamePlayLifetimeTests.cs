using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class GamePlayLifetimeTests
    {
        [Test]
        public void StopAfterAudioSourceIsDestroyedClearsPlaybackOnce()
        {
            var root = new GameObject("GamePlay lifetime test");
            root.SetActive(false);
            var source = root.AddComponent<AudioSource>();
            var play = root.AddComponent<GamePlay>();
            AudioClip song = AudioClip.Create("lifetime test", 44100,
                1, 44100, false);
            try
            {
                typeof(GamePlay).GetField("audioSource",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(play, source);
                typeof(GamePlay).GetField("cameraTransform",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(play, root.transform);
                root.SetActive(true);

                Assert.IsTrue(play.PrepareSong(song));
                Assert.AreEqual(PlaybackState.Ready, play.State);
                int emptyTransitions = 0;
                play.PlaybackStateChanged += state =>
                {
                    if (state == PlaybackState.Empty) emptyTransitions++;
                };

                Object.DestroyImmediate(source);
                Assert.DoesNotThrow(play.Stop);
                Assert.AreEqual(PlaybackState.Empty, play.State);
                Assert.DoesNotThrow(play.Stop);
                Assert.AreEqual(1, emptyTransitions);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(song);
            }
        }
    }
}
