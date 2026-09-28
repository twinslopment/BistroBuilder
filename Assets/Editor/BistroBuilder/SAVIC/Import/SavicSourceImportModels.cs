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
            string message,
            bool pending = false)
        {
            Succeeded = succeeded;
            Changed = changed;
            AssetPath = assetPath ?? string.Empty;
            MainObject = mainObject;
            Message = message ?? string.Empty;
            Pending = pending;
        }

        internal bool Succeeded { get; }
        internal bool Changed { get; }
        internal bool Pending { get; }
        internal string AssetPath { get; }
        internal Object MainObject { get; }
        internal string Message { get; }
    }

    internal interface ISavicSourceImportAdapter
    {
        bool CanImport(SavicManifest manifest);
        SavicSourceImportResult Materialize(SavicManifest manifest);
        SavicSourceImportResult RequestImport(SavicManifest manifest);
        SavicSourceImportResult ImportPrepared(SavicManifest manifest);
        SavicSourceImportResult Import(SavicManifest manifest);
    }
}
