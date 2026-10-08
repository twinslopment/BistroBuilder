using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B8: un único comando histórico para una operación colectiva.
///
/// Ejecuta Undo en orden inverso y Redo en orden directo. Si un hijo falla,
/// revierte inmediatamente todos los hijos ya aplicados, por lo que el
/// historial nunca deja una operación grupal a medias.
/// </summary>
public sealed class BistroBuilderEditorV2CompoundPlaceableHistoryCommand :
    IRestaurantEditHistoryCommand,
    IRestaurantEditHistoryCommandGroup
{
    private readonly IRestaurantEditHistoryCommand[] children;
    private readonly RestaurantEditHistoryCommandType commandType;
    private readonly string description;

    public RestaurantEditHistoryCommandType CommandType => commandType;

    public string Description => description;

    public UnityEngine.Object PrimaryTarget =>
        children.Length > 0
            ? children[0]?.PrimaryTarget
            : null;

    public IReadOnlyList<IRestaurantEditHistoryCommand> ChildCommands =>
        children;

    public bool IsValid
    {
        get
        {
            if (children == null || children.Length == 0)
                return false;

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null ||
                    !children[i].IsValid ||
                    children[i].CommandType != commandType)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public BistroBuilderEditorV2CompoundPlaceableHistoryCommand(
        RestaurantEditHistoryCommandType commandType,
        string description,
        IReadOnlyList<IRestaurantEditHistoryCommand> childCommands)
    {
        this.commandType = commandType;
        this.description = string.IsNullOrWhiteSpace(description)
            ? "Operación grupal"
            : description.Trim();

        if (childCommands == null || childCommands.Count == 0)
        {
            children = Array.Empty<IRestaurantEditHistoryCommand>();
            return;
        }

        children =
            new IRestaurantEditHistoryCommand[childCommands.Count];

        for (int i = 0; i < childCommands.Count; i++)
            children[i] = childCommands[i];
    }

    public bool TryUndo(
        out RestaurantEditHistoryCommandResult result)
    {
        return TryApply(
            true,
            out result);
    }

    public bool TryRedo(
        out RestaurantEditHistoryCommandResult result)
    {
        return TryApply(
            false,
            out result);
    }

    public void ReleaseResources()
    {
        if (children == null)
            return;

        for (int i = 0; i < children.Length; i++)
            children[i]?.ReleaseResources();
    }

    private bool TryApply(
        bool undo,
        out RestaurantEditHistoryCommandResult result)
    {
        if (!IsValid)
        {
            result = RestaurantEditHistoryCommandResult.Failure(
                RestaurantEditHistoryCommandFailureReason.CommandInvalid,
                PrimaryTarget,
                ResolveMember(PrimaryTarget),
                default,
                "La operación grupal no contiene comandos válidos.");
            return false;
        }

        int count = children.Length;
        var appliedIndices = new int[count];
        int appliedCount = 0;

        for (int step = 0; step < count; step++)
        {
            int index = undo
                ? count - 1 - step
                : step;

            IRestaurantEditHistoryCommand child = children[index];

            bool succeeded = undo
                ? child.TryUndo(out RestaurantEditHistoryCommandResult childResult)
                : child.TryRedo(out childResult);

            if (!succeeded)
            {
                bool rollbackSucceeded = RollbackApplied(
                    undo,
                    appliedIndices,
                    appliedCount);

                string message =
                    "Falló un miembro de la operación grupal: " +
                    childResult.Message;

                if (!rollbackSucceeded)
                {
                    message +=
                        " Además falló la restauración atómica del conjunto.";
                }

                result = RestaurantEditHistoryCommandResult.Failure(
                    childResult.FailureReason,
                    childResult.AffectedObject ?? child.PrimaryTarget,
                    childResult.AffectedMember ??
                        ResolveMember(child.PrimaryTarget),
                    childResult.ValidationResult,
                    message);

                return false;
            }

            appliedIndices[appliedCount++] = index;
        }

        result = RestaurantEditHistoryCommandResult.Success(
            PrimaryTarget,
            ResolveMember(PrimaryTarget),
            undo
                ? "Operación grupal deshecha."
                : "Operación grupal rehecha.");

        return true;
    }

    private bool RollbackApplied(
        bool originalWasUndo,
        int[] appliedIndices,
        int appliedCount)
    {
        bool allSucceeded = true;

        for (int i = appliedCount - 1; i >= 0; i--)
        {
            IRestaurantEditHistoryCommand child =
                children[appliedIndices[i]];

            bool restored = originalWasUndo
                ? child.TryRedo(out _)
                : child.TryUndo(out _);

            allSucceeded &= restored;
        }

        return allSucceeded;
    }

    private static RestaurantAreaMember ResolveMember(
        UnityEngine.Object target)
    {
        if (target is RestaurantAreaMember member)
            return member;

        if (target is Component component)
        {
            component.TryGetComponent(
                out RestaurantAreaMember componentMember);
            return componentMember;
        }

        if (target is GameObject gameObject)
        {
            gameObject.TryGetComponent(
                out RestaurantAreaMember gameObjectMember);
            return gameObjectMember;
        }

        return null;
    }
}
