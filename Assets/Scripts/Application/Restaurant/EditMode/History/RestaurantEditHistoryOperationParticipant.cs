/// <summary>
/// Participante opcional para efectos externos que deben acompañar Undo/Redo
/// de forma transaccional. La autorización ocurre antes de tocar el mundo y
/// el commit después de aplicar el comando pero antes de mover las pilas.
/// </summary>
public interface IRestaurantEditHistoryOperationParticipant
{
    bool TryAuthorizeHistoryOperation(
        IRestaurantEditHistoryCommand command,
        RestaurantEditHistoryDirection direction,
        out string error
    );

    bool TryCommitHistoryOperation(
        IRestaurantEditHistoryCommand command,
        RestaurantEditHistoryDirection direction,
        out string error
    );
}

public enum RestaurantEditHistoryDirection
{
    Undo = 0,
    Redo = 1
}
