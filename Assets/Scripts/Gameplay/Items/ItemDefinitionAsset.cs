using System;
using UnityEngine;

namespace REmind.Gameplay.Items
{
    /// <summary>Authored Game item data; player quantities live in the save.</summary>
    [CreateAssetMenu(fileName = "ItemDefinition",
        menuName = "REmind/Gameplay/Item Definition")]
    public sealed class ItemDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private string kindId;
        [SerializeField] private long maxQuantity = long.MaxValue;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string KindId => kindId;
        public long MaxQuantity => maxQuantity;

        public void ValidateForUse()
        {
            if (string.IsNullOrWhiteSpace(itemId) || itemId != itemId.Trim())
                throw new InvalidOperationException("Item ID must be stable and nonempty.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException(
                    $"Item '{itemId}' needs a display name.");
            if (string.IsNullOrWhiteSpace(kindId) || kindId != kindId.Trim())
                throw new InvalidOperationException(
                    $"Item '{itemId}' needs a stable kind ID.");
            if (maxQuantity <= 0)
                throw new InvalidOperationException(
                    $"Item '{itemId}' needs a positive quantity limit.");
        }
    }
}
