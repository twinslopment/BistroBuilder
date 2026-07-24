/// <summary>
/// Puerta de transición utilizada por RestaurantOrder.
///
/// Permite que una autoridad externa valide y aplique efectos atómicos antes
/// de que el estado legacy cambie. 367C la utiliza para mantener la comanda
/// canónica sincronizada sin permitir que ambos modelos diverjan.
/// </summary>
public interface IRestaurantOrderTransitionGate
{
    bool TryApproveTransition(
        RestaurantOrder order,
        OrderState currentState,
        OrderState targetState,
        out string error
    );
}
