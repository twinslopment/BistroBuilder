using UnityEngine;

/// <summary>
/// Política de acceso de una zona física. La navegación consulta esta capa
/// sin conocer nombres concretos de zonas ni tipos de restaurante.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderNavigationAccessZone : MonoBehaviour
{
    [SerializeField] private RestaurantArea area;
    [SerializeField] private BistroBuilderNavigationAgentMask allowedAgents =
        BistroBuilderNavigationAgentMask.All;
    [SerializeField, Min(0f)] private float traversalCost = 1f;

    public RestaurantArea Area => area;
    public BistroBuilderNavigationAgentMask AllowedAgents => allowedAgents;
    public float TraversalCost => Mathf.Max(0f, traversalCost);

    private void Awake()
    {
        if (area == null) area = GetComponent<RestaurantArea>();
    }

    public bool Allows(BistroBuilderNavigationAgentMask agent)
    {
        return area != null && area.IsOperational && (allowedAgents & agent) != 0;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(RestaurantArea targetArea, BistroBuilderNavigationAgentMask agents, float cost)
    {
        area = targetArea;
        allowedAgents = agents;
        traversalCost = Mathf.Max(0f, cost);
    }
#endif
}
