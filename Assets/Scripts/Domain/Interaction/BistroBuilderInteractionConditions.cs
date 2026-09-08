using System;

public enum BistroBuilderInteractionConditionPhase
{
    Discovery = 0,
    Commit = 1,
    Engage = 2
}

/// <summary>
/// Vista de sólo lectura para condiciones lógicas. Las implementaciones no deben mutar mundo.
/// </summary>
public readonly struct BistroBuilderInteractionConditionContext
{
    public readonly BistroBuilderInteractionConditionPhase phase;
    public readonly string holderId;
    public readonly BistroBuilderInteractionHolderKind holderKind;
    public readonly string interactionId;
    public readonly string targetId;
    public readonly string channelId;
    public readonly string slotId;
    public readonly string resourceId;
    public readonly BistroBuilderInteractionGrantHandle grantHandle;

    public BistroBuilderInteractionConditionContext(
        BistroBuilderInteractionConditionPhase conditionPhase,
        string actorOrHolderId,
        BistroBuilderInteractionHolderKind kind,
        string semanticInteractionId,
        string logicalTargetId,
        string logicalChannelId,
        string logicalSlotId,
        string logicalResourceId,
        BistroBuilderInteractionGrantHandle handle)
    {
        phase = conditionPhase;
        holderId = actorOrHolderId ?? string.Empty;
        holderKind = kind;
        interactionId = semanticInteractionId ?? string.Empty;
        targetId = logicalTargetId ?? string.Empty;
        channelId = logicalChannelId ?? string.Empty;
        slotId = logicalSlotId ?? string.Empty;
        resourceId = logicalResourceId ?? string.Empty;
        grantHandle = handle;
    }
}

/// <summary>
/// Condición pura de interacción. Debe limitarse a responder permitido/no permitido + razón.
/// </summary>
public interface IBistroBuilderInteractionConditionProvider
{
    bool Evaluate(
        in BistroBuilderInteractionConditionContext context,
        out BistroBuilderInteractionReasonCode reason);
}