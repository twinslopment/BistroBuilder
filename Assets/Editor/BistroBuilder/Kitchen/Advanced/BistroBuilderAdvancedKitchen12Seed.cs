using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderAdvancedKitchen12Seed
{
    public const string CatalogPath =
        "Assets/Data/Kitchen/BB_Kitchen_Station_Catalog.asset";

    public static BistroBuilderKitchenStationCatalog EnsureCatalog(out string error)
    {
        error = string.Empty;
        EnsureFolder("Assets/Data/Kitchen");
        BistroBuilderKitchenStationCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderKitchenStationCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BistroBuilderKitchenStationCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        List<BistroBuilderKitchenStationDefinition> stations = BuildStations();
        List<BistroBuilderKitchenDishRouteDefinition> routes = BuildRoutes();
        catalog.ReplaceData(stations, routes);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);

        if (!catalog.TryValidate(out error)) return null;
        if (catalog.Routes.Count == 0)
        {
            error = "No se encontraron platos canónicos para generar rutas de cocina.";
            return null;
        }
        return catalog;
    }

    private static List<BistroBuilderKitchenStationDefinition> BuildStations()
    {
        return new List<BistroBuilderKitchenStationDefinition>
        {
            Station("station.cold", "Preparación fría", BistroBuilderKitchenStationKind.ColdPrep, 2, 10500, 0, 9980, 1),
            Station("station.range", "Fuegos", BistroBuilderKitchenStationKind.Range, 2, 10000, 0, 9960, 1),
            Station("station.grill", "Plancha / parrilla", BistroBuilderKitchenStationKind.Grill, 2, 9800, 100, 9950, 1),
            Station("station.fryer", "Freidora", BistroBuilderKitchenStationKind.Fryer, 2, 10500, -50, 9940, 1),
            Station("station.oven", "Horno", BistroBuilderKitchenStationKind.Oven, 2, 9500, 150, 9960, 1),
            Station("station.pastry", "Pastelería", BistroBuilderKitchenStationKind.Pastry, 1, 9500, 200, 9980, 1),
            Station("station.plating", "Emplatado", BistroBuilderKitchenStationKind.Plating, 3, 12000, 100, 9990, 1),
            Station("station.bar", "Bebidas / pase de bar", BistroBuilderKitchenStationKind.Bar, 2, 11500, 0, 9990, 1)
        };
    }

    private static BistroBuilderKitchenStationDefinition Station(
        string id,
        string name,
        BistroBuilderKitchenStationKind kind,
        int capacity,
        int speed,
        int quality,
        int reliability,
        int tier)
    {
        return new BistroBuilderKitchenStationDefinition
        {
            stationId = id,
            displayName = name,
            kind = kind,
            baseCapacity = capacity,
            speedBasisPoints = speed,
            qualityModifierBasisPoints = quality,
            reliabilityBasisPoints = reliability,
            equipmentTier = tier
        };
    }

    private static List<BistroBuilderKitchenDishRouteDefinition> BuildRoutes()
    {
        string[] guids = AssetDatabase.FindAssets("t:BistroBuilderDishDefinition");
        Array.Sort(guids, StringComparer.Ordinal);
        var routes = new List<BistroBuilderKitchenDishRouteDefinition>(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            BistroBuilderDishDefinition dish =
                AssetDatabase.LoadAssetAtPath<BistroBuilderDishDefinition>(path);
            if (dish == null || !BistroBuilderOrderIdUtility.IsValid(dish.DishId))
                continue;
            var route = new BistroBuilderKitchenDishRouteDefinition
            {
                dishId = dish.DishId
            };
            string primary = ResolvePrimaryStation(dish.RequiredStation);
            route.stationIds.Add(primary);
            if (!string.Equals(primary, "station.bar", StringComparison.Ordinal) &&
                !string.Equals(primary, "station.plating", StringComparison.Ordinal))
            {
                route.stationIds.Add("station.plating");
            }
            routes.Add(route);
        }
        routes.Sort((a, b) => string.Compare(a.dishId, b.dishId, StringComparison.Ordinal));
        return routes;
    }

    private static string ResolvePrimaryStation(BistroBuilderKitchenStationType type)
    {
        return type switch
        {
            BistroBuilderKitchenStationType.ColdPreparation => "station.cold",
            BistroBuilderKitchenStationType.HotKitchen => "station.range",
            BistroBuilderKitchenStationType.Grill => "station.grill",
            BistroBuilderKitchenStationType.Fryer => "station.fryer",
            BistroBuilderKitchenStationType.Oven => "station.oven",
            BistroBuilderKitchenStationType.Pastry => "station.pastry",
            BistroBuilderKitchenStationType.Bar => "station.bar",
            _ => "station.plating"
        };
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent ?? "Assets", name);
    }
}
