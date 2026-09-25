using System;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicEditorContext
    {
        private static SavicEditorContext instance;

        private SavicEditorContext()
        {
            Layout = SavicStorageLayout.ForCurrentProject();
            Layout.EnsureInfrastructure();
            Manifests = new SavicManifestRepository(Layout);
            Jobs = new SavicJobStore(Layout);
            Intake = new SavicIntakeService(Layout, Manifests, Jobs);
            SourceProcessing =
                new SavicSourceProcessingService(Layout, Manifests);
            ProjectInventory =
                new SavicProjectInventoryService(Layout, Manifests);
            LegacyAdoption =
                new SavicLegacyAdoptionService(
                    Layout,
                    Manifests,
                    ProjectInventory);
        }

        internal static SavicEditorContext Instance =>
            instance ??= new SavicEditorContext();

        internal SavicStorageLayout Layout { get; }
        internal SavicManifestRepository Manifests { get; }
        internal SavicJobStore Jobs { get; }
        internal SavicIntakeService Intake { get; }
        internal SavicSourceProcessingService SourceProcessing { get; }
        internal SavicProjectInventoryService ProjectInventory { get; }
        internal SavicLegacyAdoptionService LegacyAdoption { get; }

        internal static void ResetForDomainDiagnostics()
        {
            instance = null;
        }
    }
}
