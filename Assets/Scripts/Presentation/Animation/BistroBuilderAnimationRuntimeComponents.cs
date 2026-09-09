using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[Serializable]
public sealed class BistroBuilderAnimationInteractionSlotDefinition
{
    [SerializeField] private string slotId = "default";
    [SerializeField] private BistroBuilderInteractionFamily family = BistroBuilderInteractionFamily.Workstation;
    [SerializeField] private Transform interactionFrame;
    [SerializeField] private Transform seatFrame;
    [SerializeField] private Transform exitFrame;
    [SerializeField] private Transform transferTarget;
    [SerializeField] private Transform lookTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private RestaurantSeat seat;
    [SerializeField] private BistroBuilderNavigableDoor door;
    [SerializeField] private BistroBuilderObjectMotionDriver objectMotion;

    public string SlotId => BistroBuilderMotionProfile.NormalizeId(slotId);
    public BistroBuilderInteractionFamily Family => family;
    public Transform InteractionFrame => interactionFrame;
    public Transform SeatFrame => seatFrame;
    public Transform ExitFrame => exitFrame;
    public Transform TransferTarget => transferTarget;
    public Transform LookTarget => lookTarget;
    public Transform LeftHandTarget => leftHandTarget;
    public Transform RightHandTarget => rightHandTarget;
    public RestaurantSeat Seat => seat;
    public BistroBuilderNavigableDoor Door => door;
    public BistroBuilderObjectMotionDriver ObjectMotion => objectMotion;

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(SlotId))
        {
            error = "Un slot de interacción necesita SlotId.";
            return false;
        }
        if (family == BistroBuilderInteractionFamily.None)
        {
            error = "El slot " + SlotId + " necesita familia.";
            return false;
        }
        if (interactionFrame == null)
        {
            error = "El slot " + SlotId + " necesita InteractionFrame.";
            return false;
        }
        if (family == BistroBuilderInteractionFamily.Seat && seat == null)
        {
            error = "El slot Seat " + SlotId + " necesita RestaurantSeat.";
            return false;
        }
        if (family == BistroBuilderInteractionFamily.Portal && door == null)
        {
            error = "El slot Portal " + SlotId + " necesita puerta navegable.";
            return false;
        }
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string id,
        BistroBuilderInteractionFamily configuredFamily,
        Transform configuredInteractionFrame,
        Transform configuredSeatFrame = null,
        Transform configuredExitFrame = null,
        Transform configuredTransferTarget = null,
        RestaurantSeat configuredSeat = null,
        BistroBuilderNavigableDoor configuredDoor = null,
        BistroBuilderObjectMotionDriver configuredObjectMotion = null)
    {
        slotId = id;
        family = configuredFamily;
        interactionFrame = configuredInteractionFrame;
        seatFrame = configuredSeatFrame;
        exitFrame = configuredExitFrame;
        transferTarget = configuredTransferTarget;
        seat = configuredSeat;
        door = configuredDoor;
        objectMotion = configuredObjectMotion;
    }
#endif
}

/// <summary>
/// Descriptor data-driven de un asset interactivo. Un modelo nuevo sólo
/// aporta slots y referencias; no requiere una clase específica.
/// </summary>


/// <summary>
/// Representación visual de un objeto transferible. El componente sólo
/// modifica parenting/pose; ownership e inventario siguen siendo gameplay.
/// </summary>


public enum BistroBuilderCarrySocketKind
{
    RightHand = 0,
    LeftHand = 1,
    Center = 2,
    Tray = 3,
    TwoHandCenter = 4
}



/// <summary>
/// Movimiento procedural universal para partes rígidas. El progreso 0..1
/// es observable y puede compartirse con la capa espacial.
/// </summary>


/// <summary>
/// Adaptador del personaje. Resuelve MotionId contra catálogo y oculta
/// Animator/Playable al resto de Bistro Builder.
/// </summary>


/// <summary>
/// Traduce movimiento real del Transform en parámetros de locomoción.
/// La navegación conserva siempre la autoridad de posición.
/// </summary>

