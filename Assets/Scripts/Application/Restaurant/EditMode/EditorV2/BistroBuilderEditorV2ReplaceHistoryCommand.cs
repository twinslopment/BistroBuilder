using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B10: a replacement is one history entry, even when it changes several
/// registered instances. Domain lifecycle stays with the existing commands.
/// </summary>
public sealed class BistroBuilderEditorV2ReplaceHistoryCommand :
    IRestaurantEditHistoryCommand, IRestaurantEditHistoryCommandGroup
{
    private readonly IRestaurantEditHistoryCommand[] deletions;
    private readonly IRestaurantEditHistoryCommand[] creations;
    private readonly IRestaurantEditHistoryCommand[] children;

    public RestaurantEditHistoryCommandType CommandType =>
        RestaurantEditHistoryCommandType.Replace;
    public string Description => "Sustituir " + creations.Length + " artículo(s)";
    public UnityEngine.Object PrimaryTarget =>
        deletions.Length > 0 ? deletions[0].PrimaryTarget : null;
    public IReadOnlyList<IRestaurantEditHistoryCommand> ChildCommands => children;

    public bool IsValid
    {
        get
        {
            if (deletions.Length == 0 || deletions.Length != creations.Length)
                return false;
            for (int i = 0; i < deletions.Length; i++)
            {
                if (deletions[i] == null || !deletions[i].IsValid ||
                    deletions[i].CommandType != RestaurantEditHistoryCommandType.Delete ||
                    creations[i] == null || !creations[i].IsValid ||
                    creations[i].CommandType != RestaurantEditHistoryCommandType.Create)
                    return false;
            }
            return true;
        }
    }

    public BistroBuilderEditorV2ReplaceHistoryCommand(
        IReadOnlyList<IRestaurantEditHistoryCommand> deleted,
        IReadOnlyList<IRestaurantEditHistoryCommand> created)
    {
        deletions = Copy(deleted);
        creations = Copy(created);
        children = new IRestaurantEditHistoryCommand[deletions.Length + creations.Length];
        for (int i = 0; i < deletions.Length; i++)
            children[i] = deletions[i];
        for (int i = 0; i < creations.Length; i++)
            children[deletions.Length + i] = creations[i];
    }

    public bool TryUndo(out RestaurantEditHistoryCommandResult result) =>
        TryApply(true, out result);

    public bool TryRedo(out RestaurantEditHistoryCommandResult result) =>
        TryApply(false, out result);

    private bool TryApply(bool undo, out RestaurantEditHistoryCommandResult result)
    {
        if (!IsValid)
        {
            result = RestaurantEditHistoryCommandResult.Failure(
                RestaurantEditHistoryCommandFailureReason.CommandInvalid,
                PrimaryTarget, ResolveMember(PrimaryTarget), default,
                "Sustitución inválida o incompleta.");
            return false;
        }

        // Undo: remove new instances before restoring old.
        // Redo: remove old instances before restoring new.
        int count = children.Length;
        var applied = new IRestaurantEditHistoryCommand[count];
        int appliedCount = 0;
        for (int step = 0; step < count; step++)
        {
            bool firstPhase = step < deletions.Length;
            int index = firstPhase ? step : step - deletions.Length;
            IRestaurantEditHistoryCommand child = undo
                ? (firstPhase ? creations[index] : deletions[index])
                : (firstPhase ? deletions[index] : creations[index]);

            bool succeeded = undo
                ? child.TryUndo(out RestaurantEditHistoryCommandResult childResult)
                : child.TryRedo(out childResult);
            if (!succeeded)
            {
                bool restored = true;
                for (int i = appliedCount - 1; i >= 0; i--)
                {
                    bool rolledBack = undo
                        ? applied[i].TryRedo(out _)
                        : applied[i].TryUndo(out _);
                    restored &= rolledBack;
                }

                result = RestaurantEditHistoryCommandResult.Failure(
                    childResult.FailureReason,
                    childResult.AffectedObject ?? child.PrimaryTarget,
                    childResult.AffectedMember ?? ResolveMember(child.PrimaryTarget),
                    childResult.ValidationResult,
                    "No pudo completarse la sustitución: " + childResult.Message +
                    (restored ? " Se revirtió la operación." :
                        " ERROR: falló el rollback; se requiere recuperación."));
                return false;
            }

            applied[appliedCount++] = child;
        }

        result = RestaurantEditHistoryCommandResult.Success(
            PrimaryTarget, ResolveMember(PrimaryTarget),
            undo ? "Sustitución deshecha." : "Sustitución rehecha.");
        return true;
    }

    public void ReleaseResources()
    {
        // Each child owns only the instance it created or retired.
        for (int i = 0; i < children.Length; i++)
            children[i]?.ReleaseResources();
    }

    private static IRestaurantEditHistoryCommand[] Copy(
        IReadOnlyList<IRestaurantEditHistoryCommand> items)
    {
        if (items == null) return Array.Empty<IRestaurantEditHistoryCommand>();
        var result = new IRestaurantEditHistoryCommand[items.Count];
        for (int i = 0; i < items.Count; i++) result[i] = items[i];
        return result;
    }

    private static RestaurantAreaMember ResolveMember(UnityEngine.Object target)
    {
        if (target is RestaurantAreaMember member) return member;
        if (target is Component component)
        {
            component.TryGetComponent(out RestaurantAreaMember result);
            return result;
        }
        return null;
    }
}
