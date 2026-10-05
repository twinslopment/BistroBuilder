using System;
using System.IO;
using System.Linq;

namespace BistroBuilder.Editor.Savic
{
    // Editor actions delegate mutations to the existing source, queue and publication authorities.
    internal sealed class SavicEditorActionService
    {
        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;
        private readonly SavicJobStore jobs;
        internal SavicEditorActionService(SavicStorageLayout layout, SavicManifestRepository manifests, SavicJobStore jobs)
        { this.layout = layout; this.manifests = manifests; this.jobs = jobs; }

        internal bool CanProcess(string id, out string reason)
        {
            reason = string.Empty;
            if (!manifests.TryGetBySavicId(id, out var m) || m.source == null)
            { reason = "No hay una ficha de fuente disponible."; return false; }
            if (m.source.sourceKind != SavicSourceKind.Model3D.ToString())
            { reason = "Esta acción procesa modelos 3D."; return false; }
            if (jobs.Jobs.Any(j => j != null && j.manifestSavicId == id && j.batchEligible &&
                (j.state == SavicJobState.Processing.ToString() || j.state == SavicJobState.Ingested.ToString())))
            { reason = "El asset ya está en la cola."; return false; }
            string path = layout.FromProjectRelativePath(m.source.archivedRelativePath);
            if (!File.Exists(path)) { reason = "Falta el original archivado. Adjunta el archivo original."; return false; }
            if (!SavicRuntimeVerificationSession.CanStartEditorAction(out reason)) return false;
            return true;
        }


        internal void ImportFolder(string folder)
        {
            if (!SavicRuntimeVerificationSession.CanStartEditorAction(out string reason))
                throw new InvalidOperationException(reason);
            var files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(p => Path.GetExtension(p).Equals(".glb", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) throw new InvalidOperationException("La carpeta no contiene archivos GLB.");
            foreach (string path in files)
            {
                var result = SavicEditorContext.Instance.Intake.IngestExternalCopySynchronously(path);
                if (!result.Succeeded) throw new InvalidOperationException(result.Message);
            }
        }

        internal void Reprocess(string id)
        {
            if (!CanProcess(id, out string reason)) throw new InvalidOperationException(reason);
            manifests.TryGetBySavicId(id, out var m);
            VerifySource(m);
            if (SavicFunctionalRuntimeAcceptance.Required(m) && !SavicFunctionalRuntimeAcceptance.Matches(m, layout) &&
                m.status == "NEEDS_REVIEW" && LatestJob(id)?.reasonCode?.EndsWith("_RUNTIME_ACCEPTANCE_PENDING", StringComparison.Ordinal) == true)
                throw new InvalidOperationException("El candidato necesita verificar su funcionamiento antes de publicar. Usa «Verificar funcionamiento».");
            jobs.RequeueVerifiedAsset(m);
        }

        internal void VerifySource(SavicManifest m)
        {
            string path = layout.FromProjectRelativePath(m.source.archivedRelativePath);
            if (!File.Exists(path) || !string.Equals(SavicHashService.ComputeSha256(path), m.source.sourceHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El original archivado no coincide con su huella. No se modifica el asset.");
        }

        internal SavicJobRecord LatestJob(string id) => jobs.Jobs.Where(j => j != null && j.manifestSavicId == id &&
                j.state != SavicJobState.DuplicateExact.ToString())
            .OrderByDescending(j => j.updatedUtc, StringComparer.Ordinal).ThenByDescending(j => j.createdUtc, StringComparer.Ordinal).FirstOrDefault();
    }
}
