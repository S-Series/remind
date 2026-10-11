using System;
using UnityEngine;

namespace REmind.Gameplay.Characters
{
    /// <summary>Game-only authored source for one character.</summary>
    [CreateAssetMenu(fileName = "CharacterDefinition",
        menuName = "REmind/Gameplay/Character Definition")]
    public sealed class CharacterDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string characterId;
        [SerializeField] private int definitionVersion = 1;
        [SerializeField] private string displayName;
        [SerializeField] private string englishName;
        [SerializeField, TextArea] private string profileText;
        [SerializeField] private Texture2D portraitTexture;
        [SerializeField] private Rect portraitUv = new Rect(0f, 0f, 1f, 1f);
        [SerializeField] private GameRuleConfig gaugeRule;
        [SerializeField] private int worldUnlock;
        [SerializeField] private int regularCurrency;
        [SerializeField] private int hiddenUnlock;

        public string CharacterId => characterId;
        public int DefinitionVersion => definitionVersion;
        public string DisplayName => displayName;
        public string EnglishName => englishName;
        public string ProfileText => profileText;
        public Texture2D PortraitTexture => portraitTexture;
        public Rect PortraitUv => portraitUv;
        public GameRuleConfig GaugeRule => gaugeRule;
        public int WorldUnlock => worldUnlock;
        public int RegularCurrency => regularCurrency;
        public int HiddenUnlock => hiddenUnlock;

        public void ValidateForUse()
        {
            if (string.IsNullOrWhiteSpace(characterId) ||
                characterId != characterId.Trim())
                throw new InvalidOperationException(
                    "Character ID must be stable and nonempty.");
            if (definitionVersion <= 0 ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(englishName))
                throw new InvalidOperationException(
                    $"Character '{characterId}' has incomplete identity data.");
            if (!gaugeRule)
                throw new InvalidOperationException(
                    $"Character '{characterId}' needs a gauge rule asset.");
            gaugeRule.ValidateForSession();
            if (worldUnlock < 0 || regularCurrency < 0 || hiddenUnlock < 0)
                throw new InvalidOperationException(
                    $"Character '{characterId}' has a negative stat.");
            if (float.IsNaN(portraitUv.x) ||
                float.IsNaN(portraitUv.y) ||
                float.IsNaN(portraitUv.width) ||
                float.IsNaN(portraitUv.height) ||
                float.IsInfinity(portraitUv.x) ||
                float.IsInfinity(portraitUv.y) ||
                float.IsInfinity(portraitUv.width) ||
                float.IsInfinity(portraitUv.height) ||
                portraitUv.width <= 0f || portraitUv.height <= 0f ||
                portraitUv.x < 0f || portraitUv.y < 0f ||
                portraitUv.xMax > 1f || portraitUv.yMax > 1f)
                throw new InvalidOperationException(
                    $"Character '{characterId}' has an invalid portrait UV.");
        }

        internal CharacterAbilityDefinition CreateDefinition(int statBasis)
        {
            ValidateForUse();
            return new CharacterAbilityDefinition(characterId,
                definitionVersion, displayName, gaugeRule.GaugeType,
                new CharacterAbilityStats(worldUnlock, regularCurrency,
                    hiddenUnlock, statBasis));
        }
    }
}
