#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 2.3H4 — Auto-colocación física segura con exclusión UI y sanity espacial de los anclajes de entrega física.
/// Busca el acceso de suministros y el almacén a partir de la escena real y coloca
/// la ruta visual completa sin depender de coordenadas hardcodeadas.
/// </summary>
public static class BistroBuilderSuppliers23HAutoAnchorPlacement
{
    private const string AnchorsRootName = "BB_SupplierDeliveryAnchors";

    private static readonly string[] WarehouseStrong =
    {
        "warehouse", "almacen", "almacén", "stockroom", "stock room", "storage room",
        "deposito", "depósito", "store room", "inventory warehouse"
    };

    private static readonly string[] WarehouseWeak =
    {
        "storage", "inventory", "stock", "despensa", "bodega"
    };

    private static readonly string[] SupplyStrong =
    {
        "supply access", "supplies access", "supplier access", "supplier entrance",
        "receiving access", "receiving door", "loading dock", "loading bay", "service entrance",
        "acceso suministros", "acceso de suministros", "entrada suministros", "recepcion mercancias",
        "recepción mercancías", "muelle de carga", "goods entrance"
    };

    private static readonly string[] SupplyWeak =
    {
        "supply", "supplier", "receiving", "delivery", "loading", "dock", "suministro",
        "recepcion", "recepción", "mercancias", "mercancías", "service door", "back door"
    };

    private static readonly string[] CustomerPenalty =
    {
        "customer", "guest", "client", "cliente", "restaurantentrance", "restaurant entrance",
        "main entrance", "entrada principal", "waiting", "waiter", "exitpoint", "exit point"
    };

    private static readonly string[] FloorNames =
    {
        "floor", "suelo", "piso", "ground", "restaurantfloor", "floor_test"
    };

    // Contenedores lógicos: nunca deben considerarse una ubicación física del restaurante.
    private static readonly string[] LogicalContainerNames =
    {
        "gamesystems", "game systems", "systems", "system", "managers", "manager",
        "services", "service root", "bootstrap", "runtime root", "controllers", "controller root"
    };

    // Objetos de UI/presentación nunca representan espacio físico del restaurante.
    private static readonly string[] UiPresentationNames =
    {
        "ui", "runtimeview", "runtime view", "panel", "window", "canvas", "hud",
        "overlay", "screen", "scroll", "viewport", "content", "debug", "diagnostic",
        "inventorywarehouseui", "warehouseui", "inventory ui"
    };

    private static readonly string[] CustomerEntranceStrong =
    {
        "restaurantentrance", "restaurant entrance", "main entrance", "entrada principal",
        "customer entrance", "guest entrance", "acceso clientes", "entrada clientes"
    };

    private sealed class Candidate
    {
        public GameObject go;
        public int score;
        public string reason;
        public Bounds bounds;
        public bool hasBounds;
    }

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3H4 - Auto-colocar anclajes en escena real")]
    public static void AutoPlaceAnchors()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("2.3H4 debe ejecutarse fuera de Play Mode.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("2.3H4: no hay una escena activa cargada.");
            return;
        }

        BistroBuilderSupplierDeliverySceneAnchors anchors =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierDeliverySceneAnchors>();
        if (anchors == null || !anchors.IsComplete)
        {
            Debug.LogError(
                "2.3H4: faltan los anclajes base. Ejecuta antes '2.3H - Crear/actualizar anclajes de escena'.");
            return;
        }

        List<GameObject> sceneObjects = CollectSceneObjects(scene, anchors.gameObject);
        Candidate customerEntrance = FindCustomerEntrance(sceneObjects);
        Bounds restaurantBounds;
        bool hasRestaurantBounds = TryBuildRestaurantBounds(sceneObjects, out restaurantBounds);

        Candidate warehouseCandidate = FindBestCandidate(sceneObjects, true);
        Candidate supplyCandidate = FindBestCandidate(sceneObjects, false);

        // H4: además de excluir managers/UI, un candidato debe estar espacialmente asociado al restaurante.
        Candidate warehouse = IsUsableSpatialCandidate(warehouseCandidate, true, hasRestaurantBounds, restaurantBounds) ? warehouseCandidate : null;
        Candidate supply = IsUsableSpatialCandidate(supplyCandidate, false, hasRestaurantBounds, restaurantBounds) ? supplyCandidate : null;

        if (warehouse == null && !hasRestaurantBounds)
        {
            Debug.LogError(
                "2.3H4: no existe un almacén físico identificable y tampoco se han podido obtener bounds fiables del restaurante. " +
                "No se han movido los anclajes.");
            return;
        }

        float floorY = ResolveFloorY(sceneObjects, warehouse, supply);
        Vector3 restaurantCenter = hasRestaurantBounds
            ? FlattenY(restaurantBounds.center, floorY)
            : FlattenY(warehouse.go.transform.position, floorY);

        Vector3 entrancePoint = customerEntrance != null
            ? FlattenY(GetCandidateAccessPoint(customerEntrance, restaurantCenter), floorY)
            : restaurantCenter + Vector3.forward * 5f;

        // La zona de servicio por defecto está en la fachada opuesta a la entrada de clientes.
        Vector3 rearOutward = HorizontalDirection(restaurantCenter - entrancePoint, Vector3.back);

        Vector3 warehouseCenter;
        string warehouseSource;
        if (warehouse != null)
        {
            warehouseCenter = FlattenY(warehouse.hasBounds ? warehouse.bounds.center : warehouse.go.transform.position, floorY);
            warehouseSource = warehouse.go.name + " (score " + warehouse.score + ")";
        }
        else
        {
            Vector3 rearEdge = PointOnBoundsEdge(restaurantBounds, restaurantCenter, rearOutward, floorY);
            warehouseCenter = rearEdge - rearOutward * 2.2f;
            warehouseSource = "fallback físico: zona trasera opuesta a la entrada de clientes";
        }

        Vector3 supplyDoor;
        string supplySource;
        if (supply != null)
        {
            supplyDoor = FlattenY(GetCandidateAccessPoint(supply, warehouseCenter), floorY);
            supplySource = supply.go.name + " (score " + supply.score + ")";
        }
        else if (hasRestaurantBounds)
        {
            Vector3 preferredOutward = warehouse != null
                ? HorizontalDirection(warehouseCenter - restaurantCenter, rearOutward)
                : rearOutward;
            supplyDoor = PointOnBoundsEdge(restaurantBounds, warehouseCenter, preferredOutward, floorY);
            supplySource = "fallback geométrico en fachada trasera de servicio";
        }
        else
        {
            Vector3 preferredOutward = HorizontalDirection(warehouseCenter - restaurantCenter, rearOutward);
            supplyDoor = warehouseCenter + preferredOutward * 5f;
            supplySource = "fallback geométrico desde almacén físico";
        }

        Vector3 outward = HorizontalDirection(supplyDoor - warehouseCenter, warehouseCenter - restaurantCenter);
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.left;
        Vector3 inward = -outward;
        Vector3 side = Vector3.Cross(Vector3.up, inward).normalized;

        // Si el acceso detectado está dentro de los bounds globales, lo llevamos ligeramente fuera.
        Vector3 parking = supplyDoor + outward * 3.6f;
        if (hasRestaurantBounds && restaurantBounds.Contains(parking))
        {
            Vector3 edge = PointOnBoundsEdge(restaurantBounds, supplyDoor, outward, floorY);
            parking = edge + outward * 3.6f;
            supplyDoor = edge;
        }

        Vector3 entryWp = parking + outward * 6.0f;
        Vector3 entry = parking + outward * 12.0f;

        // Salida por el mismo acceso, desplazada lateralmente para que la trayectoria se lea y no colapse.
        Vector3 exitWp = parking + outward * 6.0f + side * 2.6f;
        Vector3 exit = parking + outward * 12.0f + side * 2.6f;

        Vector3 warehouseDoor = warehouse != null
            ? ResolveWarehouseDoor(warehouse, parking, floorY)
            : supplyDoor;
        Vector3 doorInward = HorizontalDirection(warehouseCenter - warehouseDoor, inward);
        Vector3 warehouseDropoff = warehouseDoor + doorInward * 1.8f;

        // Punto del repartidor junto a la zona trasera/lateral del vehículo.
        Vector3 driverExit = parking + outward * 1.7f + side * 1.25f;
        Vector3 driverWp = Vector3.Lerp(driverExit, warehouseDoor, 0.52f);

        // Ajuste final al nivel de suelo conocido. No usamos Physics.Raycast para evitar enganchar mobiliario/cajas.
        entry.y = entryWp.y = parking.y = exitWp.y = exit.y = floorY;
        driverExit.y = driverWp.y = warehouseDoor.y = warehouseDropoff.y = floorY;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Auto-colocar anclajes 2.3H4");

        Place(anchors.vehicleEntry, entry, parking - entry);
        PlaceListPoint(anchors.vehicleEntryWaypoints, 0, entryWp, parking - entryWp);
        Place(anchors.vehicleParking, parking, inward);
        PlaceListPoint(anchors.vehicleExitWaypoints, 0, exitWp, exit - exitWp);
        Place(anchors.vehicleExit, exit, exit - exitWp);
        Place(anchors.driverExitPoint, driverExit, warehouseDoor - driverExit);
        PlaceListPoint(anchors.driverToWarehouseWaypoints, 0, driverWp, warehouseDoor - driverWp);
        Place(anchors.warehouseDoor, warehouseDoor, doorInward);
        Place(anchors.warehouseDropoff, warehouseDropoff, doorInward);

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(anchors);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = anchors.gameObject;

        StringBuilder report = new StringBuilder(1200);
        report.AppendLine("2.3H4 — ANCLAJES AUTO-COLOCADOS (UI-SAFE + SANITY ESPACIAL)");
        report.AppendLine("Escena: " + scene.name);
        report.AppendLine("Almacén/área de descarga: " + warehouseSource + (warehouse != null ? " | " + warehouse.reason : string.Empty));
        report.AppendLine("Acceso de suministros: " + supplySource + (supply != null ? " | " + supply.reason : string.Empty));
        report.AppendLine("Entrada de clientes usada para orientar la zona trasera: " +
            (customerEntrance != null ? customerEntrance.go.name + " (score " + customerEntrance.score + ")" : "no detectada; fallback geométrico"));
        report.AppendLine("Bounds físicos del restaurante: " +
            (hasRestaurantBounds ? ("centro " + Format(restaurantBounds.center) + " | tamaño " + Format(restaurantBounds.size)) : "no disponibles"));
        if (warehouse == null && warehouseCandidate != null)
            report.AppendLine("Candidato de almacén descartado por seguridad: " + warehouseCandidate.go.name + " | " + warehouseCandidate.reason);
        if (supply == null && supplyCandidate != null)
            report.AppendLine("Candidato de acceso descartado por seguridad: " + supplyCandidate.go.name + " | " + supplyCandidate.reason);
        report.AppendLine("Nivel de suelo usado: " + floorY.ToString("0.###", CultureInfo.InvariantCulture));
        report.AppendLine("VehicleEntry: " + Format(entry));
        report.AppendLine("VehicleParking: " + Format(parking));
        report.AppendLine("VehicleExit: " + Format(exit));
        report.AppendLine("WarehouseDoor: " + Format(warehouseDoor));
        report.AppendLine("WarehouseDropoff: " + Format(warehouseDropoff));
        report.AppendLine("DriverExitPoint: " + Format(driverExit));
        report.AppendLine("Ruta creada: Entry -> Parking -> repartidor -> WarehouseDoor -> Dropoff -> Parking -> Exit.");
        report.AppendLine("La escena ha quedado Dirty para que puedas guardarla. Undo revierte toda la auto-colocación.");
        Debug.Log(report.ToString());
    }

    private static List<GameObject> CollectSceneObjects(Scene scene, GameObject anchorsRoot)
    {
        List<GameObject> result = new List<GameObject>(512);
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) CollectRecursive(roots[i].transform, anchorsRoot, result);
        return result;
    }

    private static void CollectRecursive(Transform t, GameObject anchorsRoot, List<GameObject> result)
    {
        if (t == null || t.gameObject == anchorsRoot || t.IsChildOf(anchorsRoot.transform)) return;
        GameObject go = t.gameObject;
        if ((go.hideFlags & HideFlags.HideInHierarchy) == 0) result.Add(go);
        for (int i = 0; i < t.childCount; i++) CollectRecursive(t.GetChild(i), anchorsRoot, result);
    }

    private static Candidate FindBestCandidate(List<GameObject> objects, bool warehouse)
    {
        Candidate best = null;
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null || !go.activeInHierarchy) continue;
            Candidate c = Score(go, warehouse);
            if (c.score <= 0) continue;
            if (best == null || c.score > best.score) best = c;
        }
        return best;
    }

    private static Candidate Score(GameObject go, bool warehouse)
    {
        string name = Normalize(go.name);
        if (IsLogicalContainer(go))
            return new Candidate { go = go, score = -10000, reason = "contenedor lógico excluido" };
        if (IsUiPresentationObject(go))
            return new Candidate { go = go, score = -10000, reason = "UI/presentación excluida" };

        int score = 0;
        StringBuilder why = new StringBuilder();

        string[] strong = warehouse ? WarehouseStrong : SupplyStrong;
        string[] weak = warehouse ? WarehouseWeak : SupplyWeak;
        int strongHits = CountHits(name, strong);
        int weakHits = CountHits(name, weak);
        if (strongHits > 0) { score += 80 + (strongHits - 1) * 10; why.Append("nombre fuerte; "); }
        if (weakHits > 0) { score += 28 + (weakHits - 1) * 5; why.Append("nombre relacionado; "); }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null) continue;
            string typeName = Normalize(component.GetType().Name);
            int sh = CountHits(typeName, strong);
            int wh = CountHits(typeName, weak);
            if (sh > 0) { score += 55; why.Append("componente " + component.GetType().Name + "; "); }
            else if (wh > 0) { score += 20; why.Append("componente relacionado; "); }
        }

        if (!warehouse && CountHits(name, CustomerPenalty) > 0)
        {
            score -= 70;
            why.Append("penalizado por acceso de clientes; ");
        }

        if (go.GetComponent<Collider>() != null) score += 2;
        if (go.GetComponent<Renderer>() != null) score += 2;

        Bounds bounds;
        bool hasBounds = TryGetWorldBounds(go, out bounds);
        if (hasBounds)
        {
            score += 12;
            why.Append("evidencia espacial; ");
        }
        else if (strongHits == 0)
        {
            // Servicios con nombres Inventory/Warehouse pero sin representación física no cuentan como almacén.
            score -= 80;
            why.Append("sin evidencia espacial; ");
        }

        return new Candidate { go = go, score = score, reason = why.ToString().Trim(), bounds = bounds, hasBounds = hasBounds };
    }

    private static Candidate FindCustomerEntrance(List<GameObject> objects)
    {
        Candidate best = null;
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null || !go.activeInHierarchy || IsLogicalContainer(go)) continue;
            string name = Normalize(go.name);
            int hits = CountHits(name, CustomerEntranceStrong);
            if (hits <= 0) continue;
            Bounds b;
            bool hasBounds = TryGetWorldBounds(go, out b);
            Candidate c = new Candidate
            {
                go = go,
                score = 100 + hits * 20 + (hasBounds ? 5 : 0),
                reason = "entrada de clientes",
                bounds = b,
                hasBounds = hasBounds
            };
            if (best == null || c.score > best.score) best = c;
        }
        return best;
    }

    private static bool IsUsableSpatialCandidate(Candidate c, bool warehouse, bool hasRestaurantBounds, Bounds restaurantBounds)
    {
        if (c == null || c.go == null || IsLogicalContainer(c.go) || IsUiPresentationObject(c.go)) return false;
        string name = Normalize(c.go.name);
        string[] strong = warehouse ? WarehouseStrong : SupplyStrong;
        bool explicitMarker = CountHits(name, strong) > 0;
        if (c.score < 55 || (!c.hasBounds && !explicitMarker)) return false;

        // Un marcador explícito sin bounds solo es válido si su Transform está cerca del restaurante.
        Vector3 center = c.hasBounds ? c.bounds.center : c.go.transform.position;
        if (hasRestaurantBounds)
        {
            float horizontalDistance = HorizontalDistanceToBounds(center, restaurantBounds);
            float diag = Mathf.Sqrt(restaurantBounds.size.x * restaurantBounds.size.x + restaurantBounds.size.z * restaurantBounds.size.z);
            float maxDistance = Mathf.Clamp(diag * 0.45f, 8f, 30f);
            if (horizontalDistance > maxDistance) return false;

            // UI world-space suele vivir a cientos de unidades: sanity adicional respecto al centro.
            Vector2 delta = new Vector2(center.x - restaurantBounds.center.x, center.z - restaurantBounds.center.z);
            if (delta.magnitude > Mathf.Max(35f, diag * 1.25f)) return false;
        }
        return true;
    }

    private static float HorizontalDistanceToBounds(Vector3 p, Bounds b)
    {
        float dx = p.x < b.min.x ? b.min.x - p.x : (p.x > b.max.x ? p.x - b.max.x : 0f);
        float dz = p.z < b.min.z ? b.min.z - p.z : (p.z > b.max.z ? p.z - b.max.z : 0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static bool IsLogicalContainer(GameObject go)
    {
        if (go == null) return true;
        string n = Normalize(go.name).Replace(" ", string.Empty);
        for (int i = 0; i < LogicalContainerNames.Length; i++)
        {
            string term = Normalize(LogicalContainerNames[i]).Replace(" ", string.Empty);
            if (n == term || n.StartsWith(term) || n.EndsWith(term)) return true;
        }

        if (go.GetComponent<Renderer>() == null && go.GetComponent<Collider>() == null)
        {
            Component[] comps = go.GetComponents<Component>();
            int serviceLike = 0;
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null || c is Transform) continue;
                string tn = Normalize(c.GetType().Name);
                if (tn.Contains("service") || tn.Contains("manager") || tn.Contains("controller")) serviceLike++;
            }
            if (serviceLike >= 2) return true;
        }
        return false;
    }

    private static bool IsUiPresentationObject(GameObject go)
    {
        if (go == null) return true;
        if (go.GetComponent<RectTransform>() != null) return true;
        if (go.GetComponentInParent<Canvas>() != null) return true;

        string n = Normalize(go.name).Replace(" ", string.Empty);
        for (int i = 0; i < UiPresentationNames.Length; i++)
        {
            string term = Normalize(UiPresentationNames[i]).Replace(" ", string.Empty);
            if (term.Length >= 3 && n.Contains(term)) return true;
        }

        Component[] comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            Component c = comps[i];
            if (c == null || c is Transform) continue;
            Type t = c.GetType();
            string ns = t.Namespace ?? string.Empty;
            string tn = Normalize(t.Name).Replace(" ", string.Empty);
            if (ns.StartsWith("UnityEngine.UI", StringComparison.Ordinal) || ns.StartsWith("TMPro", StringComparison.Ordinal)) return true;
            if (tn.Contains("runtimeview") || tn.EndsWith("ui") || tn.Contains("editorview")) return true;
        }
        return false;
    }

    private static int CountHits(string text, string[] terms)
    {
        int hits = 0;
        for (int i = 0; i < terms.Length; i++)
            if (text.Contains(Normalize(terms[i]))) hits++;
        return hits;
    }

    private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        bool initialized = false;
        bounds = new Bounds(go.transform.position, Vector3.zero);
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || IsUiPresentationObject(r.gameObject)) continue;
            if (!initialized) { bounds = r.bounds; initialized = true; }
            else bounds.Encapsulate(r.bounds);
        }
        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || IsUiPresentationObject(c.gameObject)) continue;
            if (!initialized) { bounds = c.bounds; initialized = true; }
            else bounds.Encapsulate(c.bounds);
        }
        return initialized;
    }

    private static bool TryBuildRestaurantBounds(List<GameObject> objects, out Bounds bounds)
    {
        // Primero intentamos usar el suelo principal: es la referencia espacial más fiable del prototipo.
        bool floorFound = false;
        Bounds floorBounds = default;
        float bestArea = 0f;
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null || !go.activeInHierarchy || IsLogicalContainer(go) || IsUiPresentationObject(go)) continue;
            string n = Normalize(go.name);
            if (CountHits(n, FloorNames) <= 0) continue;
            Bounds b;
            if (!TryGetWorldBounds(go, out b)) continue;
            float area = Mathf.Abs(b.size.x * b.size.z);
            if (area < 4f || b.size.x > 120f || b.size.z > 120f) continue;
            if (!floorFound || area > bestArea)
            {
                floorFound = true;
                floorBounds = b;
                bestArea = area;
            }
        }
        if (floorFound)
        {
            bounds = floorBounds;
            return true;
        }

        // Fallback: acumulamos solo geometría física razonable, excluyendo UI y outliers lejanos.
        List<Bounds> physical = new List<Bounds>(128);
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null || !go.activeInHierarchy || IsLogicalContainer(go) || IsUiPresentationObject(go)) continue;
            string n = Normalize(go.name);
            if (n.Contains("camera") || n.Contains("light") || n.Contains("volume") ||
                n.Contains("customer") || n.Contains("waiter")) continue;
            Renderer r = go.GetComponent<Renderer>();
            Collider c = go.GetComponent<Collider>();
            Bounds b;
            if (r != null) b = r.bounds;
            else if (c != null) b = c.bounds;
            else continue;
            if (b.size.x > 80f || b.size.z > 80f || b.size.y > 30f) continue;
            physical.Add(b);
            centroid += b.center;
        }
        if (physical.Count == 0)
        {
            bounds = default;
            return false;
        }
        centroid /= physical.Count;

        // Ignora geometría a más de 60 m del centro robusto aproximado.
        bool initialized = false;
        bounds = default;
        for (int i = 0; i < physical.Count; i++)
        {
            Bounds b = physical[i];
            Vector2 d = new Vector2(b.center.x - centroid.x, b.center.z - centroid.z);
            if (d.magnitude > 60f) continue;
            if (!initialized) { bounds = b; initialized = true; }
            else bounds.Encapsulate(b);
        }
        return initialized;
    }

    private static float ResolveFloorY(List<GameObject> objects, Candidate warehouse, Candidate supply)
    {
        GameObject bestFloor = null;
        int bestScore = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null || IsLogicalContainer(go)) continue;
            string n = Normalize(go.name);
            int score = CountHits(n, FloorNames) * 10;
            if (score > bestScore) { bestFloor = go; bestScore = score; }
        }
        Bounds b;
        if (bestFloor != null && TryGetWorldBounds(bestFloor, out b)) return b.max.y;
        if (warehouse != null && warehouse.hasBounds) return warehouse.bounds.min.y;
        if (supply != null && supply.hasBounds) return supply.bounds.min.y;
        return warehouse != null ? warehouse.go.transform.position.y : 0f;
    }

    private static Vector3 GetCandidateAccessPoint(Candidate candidate, Vector3 warehouseCenter)
    {
        if (candidate == null) return Vector3.zero;
        if (!candidate.hasBounds) return candidate.go.transform.position;
        return candidate.bounds.ClosestPoint(warehouseCenter);
    }

    private static Vector3 ResolveWarehouseDoor(Candidate warehouse, Vector3 parking, float floorY)
    {
        if (warehouse == null || warehouse.go == null) return FlattenY(parking, floorY);
        if (!warehouse.hasBounds)
            return FlattenY(warehouse.go.transform.position, floorY);
        Vector3 p = warehouse.bounds.ClosestPoint(parking);
        p.y = floorY;
        return p;
    }

    private static Vector3 PointOnBoundsEdge(Bounds b, Vector3 origin, Vector3 direction, float y)
    {
        direction = HorizontalDirection(direction, Vector3.left);
        Vector3 center = b.center;
        float tx = Mathf.Abs(direction.x) > 0.0001f
            ? ((direction.x > 0f ? b.max.x : b.min.x) - center.x) / direction.x
            : float.PositiveInfinity;
        float tz = Mathf.Abs(direction.z) > 0.0001f
            ? ((direction.z > 0f ? b.max.z : b.min.z) - center.z) / direction.z
            : float.PositiveInfinity;
        float t = Mathf.Min(Mathf.Abs(tx), Mathf.Abs(tz));
        if (float.IsInfinity(t) || float.IsNaN(t)) t = 0f;
        Vector3 p = center + direction * t;
        p.y = y;
        return p;
    }

    private static void Place(Transform t, Vector3 position, Vector3 forward)
    {
        if (t == null) return;
        Undo.RecordObject(t, "Auto-colocar " + t.name);
        t.position = position;
        Vector3 f = HorizontalDirection(forward, t.forward);
        if (f.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(f, Vector3.up);
    }

    private static void PlaceListPoint(List<Transform> list, int index, Vector3 position, Vector3 forward)
    {
        if (list == null || index < 0 || index >= list.Count) return;
        Place(list[index], position, forward);
    }

    private static Vector3 HorizontalDirection(Vector3 value, Vector3 fallback)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.0001f) return value.normalized;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private static Vector3 FlattenY(Vector3 p, float y)
    {
        p.y = y;
        return p;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(decomposed.Length);
        for (int i = 0; i < decomposed.Length; i++)
        {
            char c = decomposed[i];
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (c == '_' || c == '-') c = ' ';
            sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Format(Vector3 p)
    {
        return "(" + p.x.ToString("0.00", CultureInfo.InvariantCulture) + ", " +
               p.y.ToString("0.00", CultureInfo.InvariantCulture) + ", " +
               p.z.ToString("0.00", CultureInfo.InvariantCulture) + ")";
    }
}
#endif
