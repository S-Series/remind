using System;
using System.Collections.Generic;
using UnityEngine;

namespace REmind.Gameplay.Characters
{
    /// <summary>
    /// Game-owned authored roster. It owns no player progress or session state.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterCatalog",
        menuName = "REmind/Gameplay/Character Catalog")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        [SerializeField] private string defaultCharacterId;
        [SerializeField] private int statBasis = 50;
        [SerializeField] private CharacterDefinitionAsset[] entries =
            Array.Empty<CharacterDefinitionAsset>();

        public string DefaultCharacterId => defaultCharacterId;
        public int StatBasis => statBasis;
        public IReadOnlyList<CharacterDefinitionAsset> Entries =>
            Array.AsReadOnly(entries ?? Array.Empty<CharacterDefinitionAsset>());

        public void ValidateForUse()
        {
            if (statBasis <= 0)
                throw new InvalidOperationException(
                    "Character stat basis must be positive.");
            if (string.IsNullOrWhiteSpace(defaultCharacterId))
                throw new InvalidOperationException(
                    "Character catalog needs a default character ID.");
            if (entries == null || entries.Length == 0)
                throw new InvalidOperationException(
                    "Character catalog has no definitions.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            bool hasDefault = false;
            foreach (CharacterDefinitionAsset entry in entries)
            {
                if (!entry)
                    throw new InvalidOperationException(
                        "Character catalog contains a missing definition.");
                entry.ValidateForUse();
                if (!ids.Add(entry.CharacterId))
                    throw new InvalidOperationException(
                        $"Duplicate character ID '{entry.CharacterId}'.");
                hasDefault |= string.Equals(entry.CharacterId,
                    defaultCharacterId, StringComparison.Ordinal);
            }
            if (!hasDefault)
                throw new InvalidOperationException(
                    $"Default character '{defaultCharacterId}' is missing.");
        }

        public bool TryGetEntry(string characterId,
            out CharacterDefinitionAsset entry)
        {
            ValidateForUse();
            foreach (CharacterDefinitionAsset candidate in entries)
                if (string.Equals(candidate.CharacterId, characterId,
                    StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            entry = null;
            return false;
        }

        public bool TryResolve(string characterId,
            out CharacterAbilityDefinition definition,
            out GameRuleConfig gaugeRule)
        {
            if (TryGetEntry(characterId, out CharacterDefinitionAsset entry))
            {
                definition = entry.CreateDefinition(statBasis);
                gaugeRule = entry.GaugeRule;
                return true;
            }
            definition = null;
            gaugeRule = null;
            return false;
        }
    }
}
