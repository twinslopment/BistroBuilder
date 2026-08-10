using UnityEngine;

/// <summary>
/// Bootstrap no destructivo de 2.3A1.
///
/// Solo considera suficiente un servicio de escena que esté realmente activo
/// y habilitado. Un componente inactivo no debe impedir que exista autoridad
/// runtime; si más tarde se activa, el propio Singleton lo descartará frente
/// a la instancia ya válida.
/// </summary>
public static class BistroBuilderSupplierCatalogBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureServiceExists()
    {
        BistroBuilderSupplierCatalogService current =
            BistroBuilderSupplierCatalogService.Instance;
        if (current != null && current.gameObject != null &&
            current.gameObject.activeInHierarchy && current.enabled)
        {
            return;
        }

        BistroBuilderSupplierCatalogService[] existing =
            Resources.FindObjectsOfTypeAll<BistroBuilderSupplierCatalogService>();

        for (int i = 0; i < existing.Length; i++)
        {
            BistroBuilderSupplierCatalogService candidate = existing[i];
            if (candidate != null &&
                candidate.gameObject != null &&
                candidate.gameObject.scene.IsValid() &&
                candidate.gameObject.activeInHierarchy &&
                candidate.enabled)
            {
                /*
                 * Awake debería haber registrado Instance. Si por orden de
                 * callbacks aún no lo ha hecho, no creamos una segunda raíz.
                 */
                return;
            }
        }

        GameObject root = new GameObject("BistroBuilderSupplierCatalogService");
        root.AddComponent<BistroBuilderSupplierCatalogService>();
    }
}
