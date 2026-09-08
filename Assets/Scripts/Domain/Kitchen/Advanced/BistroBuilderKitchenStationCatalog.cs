using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "KitchenStationCatalog",
    menuName = "Bistro Builder/Kitchen/Advanced Station Catalog")]
public sealed class BistroBuilderKitchenStationCatalog : ScriptableObject
{
    [SerializeField] private List<BistroBuilderKitchenStationDefinition> stations =
        new List<BistroBuilderKitchenStationDefinition>();
    [SerializeField] private List<BistroBuilderKitchenDishRouteDefinition> routes =
        new List<BistroBuilderKitchenDishRouteDefinition>();

    public IReadOnlyList<BistroBuilderKitchenStationDefinition> Stations => stations;
    public IReadOnlyList<BistroBuilderKitchenDishRouteDefinition> Routes => routes;

    public void ReplaceData(
        IList<BistroBuilderKitchenStationDefinition> stationDefinitions,
        IList<BistroBuilderKitchenDishRouteDefinition> routeDefinitions)
    {
        stations = new List<BistroBuilderKitchenStationDefinition>();
        routes = new List<BistroBuilderKitchenDishRouteDefinition>();
        if (stationDefinitions != null)
            for (int i = 0; i < stationDefinitions.Count; i++)
                if (stationDefinitions[i] != null) stations.Add(stationDefinitions[i].DeepClone());
        if (routeDefinitions != null)
            for (int i = 0; i < routeDefinitions.Count; i++)
                if (routeDefinitions[i] != null) routes.Add(routeDefinitions[i].DeepClone());
    }

    public bool TryGetStation(string stationId, out BistroBuilderKitchenStationDefinition station)
    {
        station = null;
        string normalized = BistroBuilderStaffStableIdUtility.Normalize(stationId);
        for (int i = 0; stations != null && i < stations.Count; i++)
        {
            BistroBuilderKitchenStationDefinition candidate = stations[i];
            if (candidate != null && string.Equals(candidate.stationId, normalized, StringComparison.Ordinal))
            {
                station = candidate.DeepClone();
                return true;
            }
        }
        return false;
    }

    public bool TryResolveRoute(string dishId, List<string> destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        string normalized = BistroBuilderOrderIdUtility.Normalize(dishId);
        for (int i = 0; routes != null && i < routes.Count; i++)
        {
            BistroBuilderKitchenDishRouteDefinition route = routes[i];
            if (route == null || !string.Equals(
                    BistroBuilderOrderIdUtility.Normalize(route.dishId), normalized,
                    StringComparison.Ordinal)) continue;
            if (route.stationIds != null)
                for (int j = 0; j < route.stationIds.Count; j++)
                {
                    string stationId = BistroBuilderStaffStableIdUtility.Normalize(route.stationIds[j]);
                    if (BistroBuilderStaffStableIdUtility.IsValid(stationId)) destination.Add(stationId);
                }
            return destination.Count > 0;
        }
        if (stations != null && stations.Count > 0 && stations[0] != null)
        {
            destination.Add(stations[0].stationId);
            return true;
        }
        return false;
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (stations == null || stations.Count == 0)
        {
            error = "La cocina avanzada necesita al menos una estación.";
            return false;
        }
        var stationIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < stations.Count; i++)
        {
            BistroBuilderKitchenStationDefinition station = stations[i];
            if (station == null || !station.TryValidate(out error) || !stationIds.Add(station.stationId))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "El catálogo contiene estaciones duplicadas."
                    : error;
                return false;
            }
        }
        var dishIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; routes != null && i < routes.Count; i++)
        {
            BistroBuilderKitchenDishRouteDefinition route = routes[i];
            if (route == null)
            {
                error = "El catálogo contiene una ruta nula.";
                return false;
            }
            string dishId = BistroBuilderOrderIdUtility.Normalize(route.dishId);
            if (!BistroBuilderOrderIdUtility.IsValid(dishId) || !dishIds.Add(dishId) ||
                route.stationIds == null || route.stationIds.Count == 0 || route.stationIds.Count > 3)
            {
                error = "La ruta de cocina de " + dishId + " no es válida.";
                return false;
            }
            var routeStations = new HashSet<string>(StringComparer.Ordinal);
            for (int j = 0; j < route.stationIds.Count; j++)
            {
                string stationId = BistroBuilderStaffStableIdUtility.Normalize(route.stationIds[j]);
                if (!stationIds.Contains(stationId) || !routeStations.Add(stationId))
                {
                    error = "La ruta " + dishId + " referencia una estación inválida o repetida.";
                    return false;
                }
            }
        }
        error = string.Empty;
        return true;
    }
}