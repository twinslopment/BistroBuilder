using System;
using System.Collections.Generic;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicProjectInventorySnapshot
    {
        public int schemaVersion = 1;
        public string generatedUtc = string.Empty;
        public string scannerVersion = string.Empty;
        public int totalItems;
        public int managedBySavic;
        public int legacyPendingAdoption;
        public int unmanaged;
        public int issueCount;
        public List<SavicProjectInventoryItemRecord> items =
            new List<SavicProjectInventoryItemRecord>();
        public List<SavicProjectInventoryIssueRecord> issues =
            new List<SavicProjectInventoryIssueRecord>();
    }

    [Serializable]
    internal sealed class SavicProjectInventoryItemRecord
    {
        public string assetGuid = string.Empty;
        public string itemAssetPath = string.Empty;
        public string itemId = string.Empty;
        public string displayName = string.Empty;
        public string category = string.Empty;
        public string placementScope = string.Empty;
        public string prefabAssetPath = string.Empty;
        public string prefabGuid = string.Empty;
        public string catalogIconAssetPath = string.Empty;
        public string inspectorPreviewAssetPath = string.Empty;
        public string dependencyHash = string.Empty;
        public bool inMainCatalog;
        public bool managedBySavic;
        public string adoptionState = string.Empty;
        public string savicId = string.Empty;
    }

    [Serializable]
    internal sealed class SavicProjectInventoryIssueRecord
    {
        public string code = string.Empty;
        public string severity = string.Empty;
        public string itemId = string.Empty;
        public string assetPath = string.Empty;
        public string message = string.Empty;
    }
}
