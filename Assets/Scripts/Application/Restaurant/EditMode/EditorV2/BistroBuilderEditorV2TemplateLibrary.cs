using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderEditorV2TemplateMember
{
    public string itemId;
    public Vector3 relativeAnchor;
    public Quaternion relativeRotation;
}

[Serializable]
public sealed class BistroBuilderEditorV2Template
{
    public string id;
    public string name;
    public List<BistroBuilderEditorV2TemplateMember> members =
        new List<BistroBuilderEditorV2TemplateMember>();

    public BistroBuilderEditorV2Template DeepCopy()
        => JsonUtility.FromJson<BistroBuilderEditorV2Template>(
            JsonUtility.ToJson(this));
}

[Serializable]
public sealed class BistroBuilderEditorV2TemplateDatabase
{
    public int schema = 1;
    public List<BistroBuilderEditorV2Template> templates =
        new List<BistroBuilderEditorV2Template>();
}

/// <summary>
/// B12 library. Stores only catalog IDs and relative anchor poses, never
/// prefabs, meshes or objects. Library is independent of save slots; saved
/// arrangements remain usable after a new game or scene reload.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditorV2TemplateLibrary : MonoBehaviour
{
    private const string FileName = "editor_v2_templates_v1.json";
    private const int MaxTemplates = 100;
    private const int MaxMembers = 64;

    [SerializeField] private RestaurantEditModeService editMode;
    [SerializeField] private BistroBuilderEditorV2GroupOperationService groups;
    [SerializeField] private RestaurantPlaceableCatalogService catalog;
    [SerializeField] private RestaurantPlaceableCreationService creation;

    private BistroBuilderEditorV2TemplateDatabase database;
#if UNITY_EDITOR
    private string testStoragePath;
    /// <summary>QA only: isolate test writes from the real player library.</summary>
    public void UseTemporaryStorageForTest(string path)
    {
        testStoragePath = path;
        database = null;
        loadingError = string.Empty;
        EnsureLoaded();
    }
#endif
    private string loadingError = string.Empty;
    private readonly List<RestaurantPlaceableObject> selected =
        new List<RestaurantPlaceableObject>(64);

    public int TemplateCount => database?.templates?.Count ?? 0;
    public string StoragePath =>
#if UNITY_EDITOR
        !string.IsNullOrEmpty(testStoragePath) ? testStoragePath :
#endif
        Path.Combine(Application.persistentDataPath, "EditorV2", FileName);
    public string LoadingError => loadingError;

    public void Configure(RestaurantEditModeService mode,
        BistroBuilderEditorV2GroupOperationService groupService,
        RestaurantPlaceableCatalogService itemCatalog,
        RestaurantPlaceableCreationService creator)
    {
        editMode = mode;
        groups = groupService;
        catalog = itemCatalog;
        creation = creator;
        EnsureLoaded();
    }

    private void Awake() => EnsureLoaded();

    public bool TryReload(out string error)
    {
        database = null;
        loadingError = string.Empty;
        EnsureLoaded();
        error = loadingError;
        return string.IsNullOrEmpty(error);
    }

    public IReadOnlyList<BistroBuilderEditorV2Template> CopyTemplates()
    {
        EnsureLoaded();
        var snapshots = new List<BistroBuilderEditorV2Template>(
            database.templates.Count);
        foreach (var item in database.templates)
            snapshots.Add(item.DeepCopy());
        return snapshots;
    }

    public bool TryFind(string id, out BistroBuilderEditorV2Template template)
    {
        EnsureLoaded();
        template = null;
        foreach (var entry in database.templates)
        {
            if (entry.id != id) continue;
            template = entry.DeepCopy();
            return true;
        }
        return false;
    }

    public bool TryCaptureSelection(string title, out string templateId,
        out string error)
    {
        templateId = null;
        if (!CanEdit(out error)) return false;
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 72)
        {
            error = "El nombre debe tener entre 1 y 72 caracteres.";
            return false;
        }
        if (groups == null || groups.CopySelectedPlaceables(selected) == 0 ||
            selected.Count > MaxMembers)
        {
            error = "Selecciona entre 1 y 64 muebles válidos.";
            return false;
        }
        selected.Sort((a, b) => string.CompareOrdinal(a.InstanceId, b.InstanceId));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Vector3 pivot = Vector3.zero;
        foreach (var placeable in selected)
        {
            if (placeable == null || !placeable.HasInstanceId ||
                placeable.ItemDefinition == null ||
                !ids.Add(placeable.InstanceId) ||
                catalog == null ||
                !catalog.TryGetItem(placeable.ItemDefinition.ItemId,
                    out RestaurantPlaceableItemDefinition canonical) ||
                canonical == null || !canonical.HasValidPrefab)
            {
                error = "La selección incluye un artículo sin catálogo canónico.";
                return false;
            }
            pivot += placeable.PlacementAnchor.position;
        }
        pivot /= selected.Count;
        var template = new BistroBuilderEditorV2Template
        {
            id = Guid.NewGuid().ToString("N"),
            name = title.Trim()
        };
        foreach (var placeable in selected)
        {
            template.members.Add(new BistroBuilderEditorV2TemplateMember
            {
                itemId = placeable.ItemDefinition.ItemId,
                relativeAnchor = placeable.PlacementAnchor.position - pivot,
                relativeRotation = placeable.transform.rotation
            });
        }
        if (database.templates.Count >= MaxTemplates)
        {
            error = "Límite de 100 plantillas alcanzado.";
            return false;
        }
        var candidate = CloneDatabase(database);
        candidate.templates.Add(template);
        if (!TryPersist(candidate, out error)) return false;
        database = candidate;
        templateId = template.id;
        return true;
    }

    public bool TryRemove(string id, out string error)
    {
        if (!CanEdit(out error)) return false;
        var candidate = CloneDatabase(database);
        int removed = candidate.templates.RemoveAll(
            x => x != null && x.id == id);
        if (removed != 1)
        {
            error = "No existe esa plantilla.";
            return false;
        }
        if (!TryPersist(candidate, out error)) return false;
        database = candidate;
        return true;
    }

    public bool TryQuote(string id, out long totalCents, out string error)
    {
        totalCents = 0L;
        EnsureLoaded();
        if (!string.IsNullOrEmpty(loadingError))
        {
            error = loadingError;
            return false;
        }
        if (!TryFind(id, out BistroBuilderEditorV2Template template) ||
            template.members == null || template.members.Count == 0 ||
            template.members.Count > MaxMembers || catalog == null)
        {
            error = "Plantilla o catálogo no disponible.";
            return false;
        }
        foreach (var item in template.members)
        {
            if (item == null || !catalog.TryGetItem(item.itemId,
                    out RestaurantPlaceableItemDefinition definition) ||
                definition == null || !definition.HasValidPrefab)
            {
                error = "La plantilla contiene un artículo retirado del catálogo: " +
                    item?.itemId;
                totalCents = 0;
                return false;
            }
            totalCents = checked(totalCents + definition.PurchasePriceCents);
        }
        error = string.Empty;
        return true;
    }

    public bool TryPlace(string id, Vector3 center, Quaternion orientation,
        out IReadOnlyList<RestaurantPlaceableObject> created, out string error)
    {
        created = Array.Empty<RestaurantPlaceableObject>();
        if (!CanEdit(out error)) return false;
        if (!TryQuote(id, out _, out error)) return false;
        if (!TryFind(id, out BistroBuilderEditorV2Template template))
        {
            error = "No existe esa plantilla.";
            return false;
        }
        if (creation == null)
        {
            error = "No se encuentra el pipeline de colocación.";
            return false;
        }
        var slots = new List<BistroBuilderEditorV2TemplateSpawn>(
            template.members.Count);
        foreach (var item in template.members)
        {
            if (!catalog.TryGetItem(item.itemId,
                    out RestaurantPlaceableItemDefinition definition))
            {
                error = "Artículo no encontrado: " + item.itemId;
                return false;
            }
            slots.Add(new BistroBuilderEditorV2TemplateSpawn(
                definition, center + orientation * item.relativeAnchor,
                orientation * item.relativeRotation));
        }
        bool ok = creation.TryCreateTemplateBatch(slots, out created,
            out RestaurantPlaceableBatchCreationResult result);
        error = ok ? string.Empty : result.Message;
        return ok;
    }

    private bool CanEdit(out string error)
    {
        EnsureLoaded();
        if (!string.IsNullOrEmpty(loadingError))
        {
            error = loadingError;
            return false;
        }
        if (editMode == null || !editMode.IsEditModeActive)
        {
            error = "Las plantillas solo se gestionan en modo edición.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void EnsureLoaded()
    {
        if (database != null) return;
        database = new BistroBuilderEditorV2TemplateDatabase();
        try
        {
            string path = StoragePath;
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);
            var parsed = JsonUtility.FromJson<BistroBuilderEditorV2TemplateDatabase>(json);
            if (parsed == null || parsed.schema != 1 || parsed.templates == null ||
                parsed.templates.Count > MaxTemplates)
                throw new InvalidDataException("Esquema o número de plantillas inválido.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in parsed.templates)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) ||
                    !ids.Add(item.id) ||
                    item.members == null || item.members.Count == 0 ||
                    item.members.Count > MaxMembers)
                    throw new InvalidDataException("Contenido de plantillas inválido.");
            }
            database = parsed;
        }
        catch (Exception ex)
        {
            // Fail closed. Never overwrite a broken or unsupported user library.
            loadingError = "No se pudo leer la biblioteca. Archivo conservado: " +
                ex.Message;
        }
    }

    private bool TryPersist(BistroBuilderEditorV2TemplateDatabase candidate,
        out string error)
    {
        string path = StoragePath;
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temp, JsonUtility.ToJson(candidate, true));
            if (File.Exists(path))
                File.Replace(temp, path, path + ".bak");
            else
                File.Move(temp, path);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = "No se guardó la plantilla: " + ex.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (IOException) { }
        }
    }

    private static BistroBuilderEditorV2TemplateDatabase CloneDatabase(
        BistroBuilderEditorV2TemplateDatabase source)
        => JsonUtility.FromJson<BistroBuilderEditorV2TemplateDatabase>(
            JsonUtility.ToJson(source));
}
