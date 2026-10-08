using System;
using System.Collections.Generic;

/// <summary>
/// B10: finance compensates both retirement and purchase in a single
/// authorized history operation. Replace never aliases the Delete track.
/// </summary>
public sealed partial class BistroBuilderPlaceableFinanceBridge
{
    private bool TryAuthorizeReplacementHistory(
        IRestaurantEditHistoryCommand command,
        RestaurantEditHistoryDirection direction,
        out string error)
    {
        error = string.Empty;
        if (!(command is BistroBuilderEditorV2ReplaceHistoryCommand replacement) ||
            !replacement.IsValid)
        {
            error = "El historial de sustitución no es válido.";
            return false;
        }

        var children = replacement.ChildCommands;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var mergedRequests = new List<BistroBuilderFinanceTransactionRequest>(
            children.Count * 2);
        RestaurantPlaceableObject primary = null;
        bool hasCreate = false;
        bool hasDelete = false;
        string description = direction == RestaurantEditHistoryDirection.Undo
            ? "Deshacer " + command.Description
            : "Rehacer " + command.Description;

        for (int i = 0; i < children.Count; i++)
        {
            IRestaurantEditHistoryCommand child = children[i];
            if (child == null ||
                (child.CommandType != RestaurantEditHistoryCommandType.Create &&
                 child.CommandType != RestaurantEditHistoryCommandType.Delete) ||
                !TryResolveFinancialPlaceable(child, out RestaurantPlaceableObject placeable) ||
                placeable == null || string.IsNullOrWhiteSpace(placeable.InstanceId) ||
                !ids.Add(placeable.InstanceId))
            {
                error = "La sustitución financiera contiene artículos incompatibles o duplicados.";
                return false;
            }

            hasCreate |= child.CommandType == RestaurantEditHistoryCommandType.Create;
            hasDelete |= child.CommandType == RestaurantEditHistoryCommandType.Delete;
            if (primary == null) primary = placeable;
            string track = child.CommandType == RestaurantEditHistoryCommandType.Create
                ? CreateTrack : DeleteTrack;

            if (!TryBuildInverseOfLatestTrackGroup(
                    placeable, track, description,
                    out FinancialPlan childPlan, out error))
                return false;

            for (int j = 0; j < childPlan.Requests.Count; j++)
                mergedRequests.Add(childPlan.Requests[j]);
        }

        if (!hasCreate || !hasDelete || primary == null)
        {
            error = "Una sustitución debe contener compras y retiradas.";
            return false;
        }

        var plan = new FinancialPlan("replace", 0, mergedRequests);
        if (!TryAuthorizePlan(plan, out error))
            return false;

        pendingHistory[command] = new HistoryPlan(direction, plan, primary);
        return true;
    }

    private bool TryCommitReplacementHistory(
        IRestaurantEditHistoryCommand command,
        RestaurantEditHistoryDirection direction,
        out string error)
    {
        error = string.Empty;
        if (!pendingHistory.TryGetValue(command, out HistoryPlan historyPlan) ||
            historyPlan.Direction != direction)
        {
            error = "No se ha autorizado el efecto financiero de la sustitución.";
            return false;
        }

        if (!TryPostPlan(historyPlan.Plan, out error))
            return false;

        pendingHistory.Remove(command);
        if (historyPlan.Plan.NetCashCents != 0L)
        {
            pendingHistoryPopups[command] = new PopupPayload(
                historyPlan.Plan.NetCashCents,
                ResolveWorldPosition(historyPlan.Placeable));
        }
        return true;
    }
}
