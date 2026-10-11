using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using REmind.Gameplay.Characters;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class CharacterSelectionIntegrationTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void EquippedCharacterSelectionKeepsSongAndStartConditions()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "remind-character-selection-" + Guid.NewGuid().ToString("N") +
                ".json");
            var rootObject = new GameObject("Character selection test AppRoot");
            rootObject.SetActive(false);
            var app = rootObject.AddComponent<AppRoot>();
            var rule = ScriptableObject.CreateInstance<GameRuleConfig>();
            var definition = ScriptableObject.CreateInstance<CharacterDefinitionAsset>();
            var catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
            AppRoot previous = AppRoot.Current;
            try
            {
                SetField(definition, "characterId", "non");
                SetField(definition, "definitionVersion", 7);
                SetField(definition, "displayName", "논");
                SetField(definition, "englishName", "NON");
                SetField(definition, "gaugeRule", rule);
                SetField(definition, "worldUnlock", 50);
                SetField(definition, "regularCurrency", 50);
                SetField(definition, "hiddenUnlock", 50);
                SetField(catalog, "defaultCharacterId", "non");
                SetField(catalog, "statBasis", 50);
                SetField(catalog, "entries", new[] { definition });
                catalog.ValidateForUse();

                LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
                store.EnsureStarterCharacter("non", id =>
                    catalog.TryResolve(id, out _, out _));
                SetField(app, "playerData", store);
                SetField(app, "characterCatalog", catalog);
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, app);

                Assert.IsTrue(app.TrySelectSongWithEquippedCharacter(
                    "song", "hard", out string error), error);
                Assert.IsTrue(app.TryGetSelectedSong(out AppRoot.SongSelection song));
                Assert.AreEqual("song", song.MusicId);
                Assert.AreEqual("hard", song.DifficultyId);
                Assert.IsTrue(app.TryGetSelectedPlayConditions(
                    out CharacterAbilityDefinition ability,
                    out GameRuleConfig selectedRule));
                Assert.AreSame(rule, selectedRule);
                CharacterAbilityStartSnapshot frozen =
                    ability.FreezeForStart(selectedRule);
                Assert.AreEqual("non", frozen.CharacterId);
                Assert.AreEqual(7, frozen.DefinitionVersion);
                Assert.AreEqual(50, frozen.Stats.WorldUnlock);
                Assert.AreEqual(50, frozen.StatBasis);
                Assert.AreEqual(1m, frozen.WorldUnlockMultiplier);
                SetField(definition, "worldUnlock", 80);
                Assert.AreEqual(50, frozen.Stats.WorldUnlock,
                    "The start snapshot keeps authored values after the asset changes.");

                Assert.AreEqual("non", LocalPlayerDataStore.Load(path)
                    .EquippedCharacterId);
            }
            finally
            {
                typeof(AppRoot).GetProperty(nameof(AppRoot.Current))
                    .SetValue(null, previous);
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(rule);
                foreach (string candidate in new[]
                    { path, path + ".bak", path + ".tmp" })
                    if (File.Exists(candidate)) File.Delete(candidate);
            }
        }

        private static void SetField(object target, string name,
            object value) => target.GetType().GetField(name,
            PrivateInstance).SetValue(target, value);
    }
}
