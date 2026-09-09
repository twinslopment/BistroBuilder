using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Restaurant Edit Document Save Provider")]
public sealed class BistroBuilderEditDocumentSaveSectionProvider : MonoBehaviour,
    IBistroBuilderSaveSectionProvider
{
    public const string StableSectionId = "restaurant.edit_architecture";
    public const int StableSectionVersion = 1;

    [SerializeField] private BistroBuilderEditDocumentRuntimeService runtimeService;
    [SerializeField] private BistroBuilderArchitectureRuntimeMaterializer materializer;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 80;
    public bool IsRequired => false;
    public System.Type StateType => typeof(BistroBuilderEditDocument);
    public string SerializerId => BistroBuilderJsonSaveSerializer.StableSerializerId;

    private void Awake()
    {
        CacheDependencies();
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        CacheDependencies();
        if (runtimeService == null)
        {
            context.Fail("Falta BistroBuilderEditDocumentRuntimeService.");
            yield break;
        }
        context.Complete(runtimeService.GetCommittedSnapshot());
        yield break;
    }

    public bool ValidateState(object state, out string error)
    {
        error = string.Empty;
        if (!(state is BistroBuilderEditDocument document))
        {
            error = "El estado arquitectónico tiene un tipo incorrecto.";
            return false;
        }
        if (document.schemaVersion != 1)
        {
            error = "Versión de documento arquitectónico no compatible.";
            return false;
        }
        var validation = new BistroBuilderEditValidationOrchestrator();
        validation.Register(new BistroBuilderIntrinsicEditValidationProvider());
        List<BistroBuilderEditDiagnostic> diagnostics = validation.Validate(document, document.revision);
        for (int i = 0; i < diagnostics.Count; i++)
        {
            if (diagnostics[i].severity != BistroBuilderEditDiagnosticSeverity.Blocking) continue;
            error = string.IsNullOrWhiteSpace(diagnostics[i].message)
                ? "El documento arquitectónico contiene errores bloqueantes."
                : diagnostics[i].message;
            return false;
        }
        return true;
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        yield break;
    }
    public IEnumerator ApplyState(object state, BistroBuilderSaveLoadContext context)
    {
        CacheDependencies();
        if (runtimeService == null)
        {
            context.Fail("Falta BistroBuilderEditDocumentRuntimeService durante la carga.");
            yield break;
        }
        if (!(state is BistroBuilderEditDocument document))
        {
            context.Fail("El estado arquitectónico tiene un tipo incorrecto.");
            yield break;
        }
        if (!ValidateState(document, out string validationError))
        {
            context.Fail(validationError);
            yield break;
        }
        if (!runtimeService.ReplaceCommittedForLoad(document, out string replaceError))
        {
            context.Fail(replaceError);
            yield break;
        }
        if (materializer != null) materializer.Rebuild(document);
        yield break;
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
    }

    private void CacheDependencies()
    {
        if (runtimeService == null) runtimeService = FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>();
        if (materializer == null) materializer = FindFirstObjectByType<BistroBuilderArchitectureRuntimeMaterializer>();
    }
}
