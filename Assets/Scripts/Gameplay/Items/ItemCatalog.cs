using System;
using System.Collections.Generic;
using UnityEngine;

namespace REmind.Gameplay.Items
{
    /// <summary>Game-owned item definitions; no player quantities.</summary>
    [CreateAssetMenu(fileName = "ItemCatalog",
        menuName = "REmind/Gameplay/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinitionAsset[] entries =
            Array.Empty<ItemDefinitionAsset>();

        public IReadOnlyList<ItemDefinitionAsset> Entries =>
            Array.AsReadOnly(entries ?? Array.Empty<ItemDefinitionAsset>());

        public void ValidateForUse()
        {
            if (entries == null || entries.Length == 0)
                throw new InvalidOperationException("Item catalog has no definitions.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemDefinitionAsset entry in entries)
            {
                if (!entry)
                    throw new InvalidOperationException(
                        "Item catalog contains a missing definition.");
                entry.ValidateForUse();
                if (!ids.Add(entry.ItemId))
                    throw new InvalidOperationException(
                        $"Duplicate item ID '{entry.ItemId}'.");
            }
        }

        public bool TryGetEntry(string itemId, out ItemDefinitionAsset entry)
        {
            ValidateForUse();
            foreach (ItemDefinitionAsset candidate in entries)
                if (string.Equals(candidate.ItemId, itemId,
                    StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            entry = null;
            return false;
        }
    }
}
