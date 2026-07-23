using System.Collections.Generic;

/// <summary>
/// Contrato universal para relaciones de colocación que deben desplazarse
/// como una única unidad lógica.
///
/// Ejemplos: mesa-sillas, barra-módulos, mueble-decoración anclada,
/// estantería-objetos o equipamiento encastrado.
/// </summary>
public interface IRestaurantPlacementLinkedGroupProvider
{
    int Priority { get; }

    bool IsLinkEnabled { get; }

    /// <summary>
    /// Añade a la lista todos los miembros confirmados que dependen de la
    /// raíz indicada. El proveedor no debe limpiar la lista recibida.
    /// </summary>
    void CollectLinkedMembers(
        RestaurantAreaMember rootMember,
        List<RestaurantAreaMember> results
    );

    /// <summary>
    /// Recibe una notificación después de aplicar definitivamente una pose
    /// de grupo: confirmación, cancelación, Undo o Redo.
    /// </summary>
    void NotifyLinkedGroupPoseApplied(
        RestaurantAreaMember rootMember,
        IReadOnlyList<RestaurantAreaMember> linkedMembers
    );
}
