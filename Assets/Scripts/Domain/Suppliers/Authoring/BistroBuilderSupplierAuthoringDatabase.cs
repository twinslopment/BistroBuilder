using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base de datos maestra de autoría de proveedores.
///
/// No representa el estado dinámico de una partida. Su misión es conservar
/// contenido de diseño estable: identidad, clasificación, condiciones y
/// perfiles que los sistemas 2.3B+ consumirán/publicarán.
/// </summary>
[CreateAssetMenu(
    fileName = "BistroBuilderSupplierAuthoringDatabase",
    menuName = "Bistro Builder/Proveedores/Base de datos de autoría"
)]
public sealed class BistroBuilderSupplierAuthoringDatabase : ScriptableObject
{
    public const string CurrentSchemaId = "supplier.authoring";
    public const int CurrentSchemaVersion = 2;

    [SerializeField]
    private string schemaId = CurrentSchemaId;

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private int contentRevision = 1;

    [SerializeField]
    private List<BistroBuilderSupplierAuthoringRecord> suppliers =
        new List<BistroBuilderSupplierAuthoringRecord>();

    public string SchemaId => schemaId;
    public int SchemaVersion => schemaVersion;
    public int ContentRevision => contentRevision;
    public IReadOnlyList<BistroBuilderSupplierAuthoringRecord> Suppliers => suppliers.AsReadOnly();

    public bool TryGetSupplier(string supplierId, out BistroBuilderSupplierAuthoringRecord supplier)
    {
        supplier = null;
        string normalized = NormalizeLookupId(supplierId);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord candidate = suppliers[index];
            if (candidate != null && string.Equals(candidate.SupplierId, normalized, StringComparison.Ordinal))
            {
                supplier = candidate;
                return true;
            }
        }

        return false;
    }

    public int CopySuppliers(List<BistroBuilderSupplierAuthoringRecord> buffer, bool activeOnly = false)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        buffer.Clear();

        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null || (activeOnly && !supplier.isActive))
            {
                continue;
            }

            buffer.Add(supplier);
        }

        return buffer.Count;
    }

#if UNITY_EDITOR
    public List<BistroBuilderSupplierAuthoringRecord> EditorSuppliers => suppliers;

    public void EditorEnsureSchema()
    {
        schemaId = CurrentSchemaId;
        schemaVersion = CurrentSchemaVersion;
        contentRevision = Mathf.Max(1, contentRevision);
    }

    public void EditorTouchRevision()
    {
        contentRevision = Mathf.Max(1, contentRevision + 1);
    }
#endif

    private static string NormalizeLookupId(string supplierId)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
        {
            return string.Empty;
        }

        string value = supplierId.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return value.StartsWith("supplier_", StringComparison.Ordinal) ? value : "supplier_" + value;
    }
}
