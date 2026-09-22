using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicSourceImportResult
    {
        internal SavicSourceImportResult(
            bool succeeded,
            bool changed,
            string assetPath,
            Object mainObject,
            string message)
        {
            Succeeded = succeeded;
            Changed = changed;
            AssetPath = assetPath ?? string.Empty;
            MainObject = mainObject;
            Message = message ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal bool Changed { get; }
        internal string AssetPath { get; }
        internal Object MainObject { get; }
        internal string Message { get; }
    }

    internal interface ISavicSourceImportAdapter
    {
        bool CanImport(SavicManifest manifest);
        SavicSourceImportResult Import(SavicManifest manifest);
    }
}
