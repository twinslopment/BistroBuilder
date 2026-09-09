/// <summary>
/// Frontera entre Edición y el sistema económico. Los servicios de creación
/// y eliminación no conocen Finanzas; únicamente consultan y confirman la
/// operación mediante este contrato enlazable.
/// </summary>
public interface IRestaurantPlaceableEconomyGate
{
    bool TryAuthorizeCreation(
        RestaurantPlaceableObject placeable,
        out string error
    );

    bool TryCommitCreation(
        RestaurantPlaceableObject placeable,
        out string error
    );

    bool TryRollbackCreation(
        RestaurantPlaceableObject placeable,
        out string error
    );

    bool TryAuthorizeDeletion(
        RestaurantPlaceableObject placeable,
        out string error
    );

    bool TryCommitDeletion(
        RestaurantPlaceableObject placeable,
        out string error
    );

    bool TryRollbackDeletion(
        RestaurantPlaceableObject placeable,
        out string error
    );
}
