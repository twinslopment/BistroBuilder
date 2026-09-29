using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puente entre la validación de colocación y BB Universal Preview.
///
/// El comportamiento principal publica huella, ghost, snap y conflicto
/// en el sistema universal. El antiguo tintado completo verde/rojo se
/// conserva únicamente como fallback de diagnóstico opcional.
///
/// No utiliza Update. Reacciona exclusivamente a eventos del
/// controlador de interacción.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Edit Placement Visual Feedback"
)]
public sealed class RestaurantEditPlacementVisualFeedback :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Controlador que publica la selección y el resultado " +
        "de cada validación de colocación."
    )]
    [SerializeField]
    private RestaurantEditInteractionController
        interactionController;

    [SerializeField]
    private BistroBuilderUniversalPreviewService
        universalPreviewService;

    [Tooltip("Mantiene el tintado verde/rojo antiguo solo como fallback de diagnóstico.")]
    [SerializeField]
    private bool useLegacyFullObjectTint;

    [Header("Fallback legacy de renderizadores")]

    [Tooltip(
        "Incluye renderizadores de hijos inactivos del objeto."
    )]
    [SerializeField]
    private bool includeInactiveRenderers = true;

    [Header("Colores")]

    [Tooltip(
        "Color aplicado cuando la posición es válida."
    )]
    [SerializeField]
    private Color validPlacementColor =
        new Color(
            0.25f,
            1f,
            0.35f,
            1f
        );

    [Tooltip(
        "Color aplicado cuando la posición es inválida."
    )]
    [SerializeField]
    private Color invalidPlacementColor =
        new Color(
            1f,
            0.2f,
            0.2f,
            1f
        );

    [Header("Propiedades de shader")]

    [Tooltip(
        "Aplica el color a _BaseColor, utilizado habitualmente " +
        "por materiales URP."
    )]
    [SerializeField]
    private bool affectBaseColorProperty = true;

    [Tooltip(
        "Aplica el color a _Color, utilizado por materiales " +
        "Standard y otros shaders."
    )]
    [SerializeField]
    private bool affectLegacyColorProperty = true;

    [Header("Depuración")]

    [Tooltip(
        "Muestra un aviso cuando un objeto editable no tiene " +
        "ningún Renderer."
    )]
    [SerializeField]
    private bool logMissingRenderers = true;

    private static readonly int BaseColorPropertyId =
        Shader.PropertyToID(
            "_BaseColor"
        );

    private static readonly int LegacyColorPropertyId =
        Shader.PropertyToID(
            "_Color"
        );

    /// <summary>
    /// Lista reutilizable para descubrir renderizadores.
    /// </summary>
    private readonly List<Renderer> rendererBuffer =
        new List<Renderer>(8);

    /// <summary>
    /// Estado visual original de cada renderer afectado.
    /// </summary>
    private readonly List<RendererPropertySnapshot>
        rendererSnapshots =
            new List<RendererPropertySnapshot>(8);

    /// <summary>
    /// Bloque reutilizable para aplicar propiedades visuales.
    ///
    /// Se crea en Awake porque MaterialPropertyBlock utiliza
    /// recursos nativos de Unity y no puede construirse en el
    /// inicializador de campos de un MonoBehaviour.
    /// </summary>
    private MaterialPropertyBlock workingBlock;

    private RestaurantAreaMember activeMember;

    private bool hasOriginalPose;
    private Vector3 originalWorldPosition;
    private Quaternion originalWorldRotation = Quaternion.identity;

    private void Awake()
    {
        workingBlock =
            new MaterialPropertyBlock();

        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        EnsureWorkingBlockExists();

        CacheDependenciesIfNeeded();
        SubscribeToController();
        SynchronizeWithControllerState();
    }

    private void OnDisable()
    {
        UnsubscribeFromController();
        RestoreOriginalVisualState();
    }

    private void OnDestroy()
    {
        UnsubscribeFromController();
        RestoreOriginalVisualState();

        workingBlock = null;
    }

    /// <summary>
    /// Sincroniza el feedback si el componente se activa cuando
    /// ya existe una colocación en curso.
    /// </summary>
    private void SynchronizeWithControllerState()
    {
        if (interactionController == null)
        {
            return;
        }

        RestaurantAreaMember currentMember =
            interactionController.ActiveMember;

        if (currentMember == null)
        {
            return;
        }

        CaptureOriginalPose(
            currentMember
        );

        BeginVisualFeedback(
            currentMember
        );

        ApplyValidationResult(
            interactionController.LastValidationResult
        );
    }

    /// <summary>
    /// Reacciona a la selección o liberación de un objeto.
    /// </summary>
    private void HandleActiveMemberChanged(
        RestaurantAreaMember member
    )
    {
        RestoreOriginalVisualState();

        if (member == null)
        {
            return;
        }

        CaptureOriginalPose(
            member
        );

        BeginVisualFeedback(
            member
        );
    }

    /// <summary>
    /// Reacciona al resultado de una nueva posición candidata.
    /// </summary>
    private void HandlePlacementValidationChanged(
        RestaurantPlacementValidationResult result
    )
    {
        if (activeMember == null)
        {
            return;
        }

        ApplyValidationResult(
            result
        );
    }

    /// <summary>
    /// Captura todos los renderizadores del objeto y conserva
    /// sus MaterialPropertyBlock originales.
    /// </summary>
    private void BeginVisualFeedback(
        RestaurantAreaMember member
    )
    {
        if (member == null)
        {
            return;
        }

        activeMember =
            member;

        rendererBuffer.Clear();
        rendererSnapshots.Clear();

        if (!useLegacyFullObjectTint)
            return;

        member.GetComponentsInChildren(
            includeInactiveRenderers,
            rendererBuffer
        );

        for (int index = 0;
             index < rendererBuffer.Count;
             index++)
        {
            Renderer targetRenderer =
                rendererBuffer[index];

            if (targetRenderer == null)
            {
                continue;
            }

            RendererPropertySnapshot snapshot =
                new RendererPropertySnapshot(
                    targetRenderer
                );

            rendererSnapshots.Add(
                snapshot
            );
        }

        rendererBuffer.Clear();

        if (rendererSnapshots.Count == 0 &&
            logMissingRenderers)
        {
            Debug.LogWarning(
                member.name +
                " está siendo editado, pero no tiene ningún " +
                "Renderer en su jerarquía.",
                member
            );
        }
    }

    private void CaptureOriginalPose(
        RestaurantAreaMember member)
    {
        if (member == null)
        {
            hasOriginalPose = false;
            originalWorldPosition = Vector3.zero;
            originalWorldRotation =
                Quaternion.identity;
            return;
        }

        RestaurantPlacementTransactionService
            transactionService =
                interactionController != null
                    ? interactionController
                        .PlacementTransactionService
                    : null;

        if (transactionService != null &&
            transactionService.TryGetOriginalWorldPose(
                out Vector3 capturedPosition,
                out Quaternion capturedRotation))
        {
            originalWorldPosition =
                capturedPosition;

            originalWorldRotation =
                capturedRotation;
        }
        else
        {
            originalWorldPosition =
                member.transform.position;

            originalWorldRotation =
                member.transform.rotation;
        }

        hasOriginalPose = true;
    }

    /// <summary>
    /// Aplica el color correspondiente al resultado actual.
    /// </summary>
    private void ApplyValidationResult(
        RestaurantPlacementValidationResult result
    )
    {
        PublishUniversalPreview(result);

        if (!useLegacyFullObjectTint)
        {
            return;
        }

        Color targetColor;

        if (result.IsValid)
        {
            targetColor =
                validPlacementColor;
        }
        else
        {
            targetColor =
                invalidPlacementColor;
        }

        ApplyColorToCapturedRenderers(
            targetColor
        );
    }

    private void PublishUniversalPreview(
        RestaurantPlacementValidationResult result
    )
    {
        if (universalPreviewService == null ||
            activeMember == null)
        {
            return;
        }

        if (!hasOriginalPose)
        {
            originalWorldPosition = activeMember.transform.position;
            originalWorldRotation = activeMember.transform.rotation;
            hasOriginalPose = true;
        }

        RestaurantPlacementSnapResult snapResult =
            interactionController != null &&
            interactionController.PlacementSnapService != null
                ? interactionController.PlacementSnapService.CurrentResult
                : RestaurantPlacementSnapResult.Unsnapped(
                    activeMember.transform.position,
                    activeMember.transform.rotation
                );

        bool showOriginalGhost =
            interactionController == null ||
            interactionController.PlacementTransactionService == null ||
            interactionController.PlacementTransactionService.ActiveTransactionKind !=
                RestaurantPlacementTransactionKind.CreateNew;

        universalPreviewService.PublishFurniture(
            activeMember,
            originalWorldPosition,
            originalWorldRotation,
            result,
            snapResult,
            showOriginalGhost
        );
    }

    /// <summary>
    /// Aplica un color sin modificar los materiales compartidos.
    /// </summary>
    private void ApplyColorToCapturedRenderers(
        Color targetColor
    )
    {
        EnsureWorkingBlockExists();

        if (workingBlock == null)
        {
            return;
        }

        for (int index = 0;
             index < rendererSnapshots.Count;
             index++)
        {
            RendererPropertySnapshot snapshot =
                rendererSnapshots[index];

            Renderer targetRenderer =
                snapshot.TargetRenderer;

            if (targetRenderer == null)
            {
                continue;
            }

            workingBlock.Clear();

            /*
             * Se recupera el bloque actual para conservar otras
             * propiedades que pudieran haberse configurado.
             */
            targetRenderer.GetPropertyBlock(
                workingBlock
            );

            if (affectBaseColorProperty)
            {
                workingBlock.SetColor(
                    BaseColorPropertyId,
                    targetColor
                );
            }

            if (affectLegacyColorProperty)
            {
                workingBlock.SetColor(
                    LegacyColorPropertyId,
                    targetColor
                );
            }

            targetRenderer.SetPropertyBlock(
                workingBlock
            );
        }

        workingBlock.Clear();
    }

    /// <summary>
    /// Restaura exactamente los bloques de propiedades existentes
    /// antes de comenzar la colocación.
    /// </summary>
    private void RestoreOriginalVisualState()
    {
        for (int index = 0;
             index < rendererSnapshots.Count;
             index++)
        {
            RendererPropertySnapshot snapshot =
                rendererSnapshots[index];

            if (snapshot == null)
            {
                continue;
            }

            snapshot.Restore();
        }

        rendererSnapshots.Clear();
        rendererBuffer.Clear();

        if (workingBlock != null)
        {
            workingBlock.Clear();
        }

        universalPreviewService?.ClearOwner(
            BistroBuilderUniversalPreviewService.FurnitureOwner
        );

        activeMember = null;
        hasOriginalPose = false;
        originalWorldPosition = Vector3.zero;
        originalWorldRotation = Quaternion.identity;
    }

    /// <summary>
    /// Garantiza que el bloque reutilizable exista.
    ///
    /// Normalmente se crea en Awake. Esta comprobación adicional
    /// protege el componente durante recargas del editor o cambios
    /// de habilitación.
    /// </summary>
    private void EnsureWorkingBlockExists()
    {
        if (workingBlock != null)
        {
            return;
        }

        workingBlock =
            new MaterialPropertyBlock();
    }

    private void SubscribeToController()
    {
        if (interactionController == null)
        {
            return;
        }

        interactionController.ActiveMemberChanged -=
            HandleActiveMemberChanged;

        interactionController.PlacementValidationChanged -=
            HandlePlacementValidationChanged;

        interactionController.ActiveMemberChanged +=
            HandleActiveMemberChanged;

        interactionController.PlacementValidationChanged +=
            HandlePlacementValidationChanged;
    }

    private void UnsubscribeFromController()
    {
        if (interactionController == null)
        {
            return;
        }

        interactionController.ActiveMemberChanged -=
            HandleActiveMemberChanged;

        interactionController.PlacementValidationChanged -=
            HandlePlacementValidationChanged;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (interactionController == null)
        {
            TryGetComponent(
                out interactionController
            );
        }

        if (universalPreviewService == null)
        {
            universalPreviewService =
                Application.isPlaying
                    ? BistroBuilderUniversalPreviewService.GetOrCreate()
                    : FindFirstObjectByType<
                        BistroBuilderUniversalPreviewService>();
        }
    }

    private void ValidateDependencies()
    {
        if (interactionController != null)
        {
            return;
        }

        string componentName =
            nameof(
                RestaurantEditPlacementVisualFeedback
            );

        string dependencyName =
            nameof(
                RestaurantEditInteractionController
            );

        Debug.LogError(
            componentName +
            " necesita un " +
            dependencyName +
            ".",
            this
        );
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif

    /// <summary>
    /// Conserva el estado anterior de un Renderer para restaurarlo
    /// cuando finalice la colocación.
    /// </summary>
    private sealed class RendererPropertySnapshot
    {
        public Renderer TargetRenderer
        {
            get;
            private set;
        }

        private readonly bool hadPropertyBlock;

        private readonly MaterialPropertyBlock previousBlock;

        public RendererPropertySnapshot(
            Renderer targetRenderer
        )
        {
            TargetRenderer =
                targetRenderer;

            if (targetRenderer == null)
            {
                hadPropertyBlock = false;
                previousBlock = null;

                return;
            }

            hadPropertyBlock =
                targetRenderer.HasPropertyBlock();

            if (!hadPropertyBlock)
            {
                previousBlock = null;

                return;
            }

            /*
             * Esta instancia se crea durante una interacción real,
             * no durante el constructor del MonoBehaviour.
             */
            previousBlock =
                new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(
                previousBlock
            );
        }

        /// <summary>
        /// Restaura el bloque previo o elimina el bloque temporal
        /// cuando el renderer no tenía ninguno.
        /// </summary>
        public void Restore()
        {
            if (TargetRenderer == null)
            {
                return;
            }

            if (hadPropertyBlock &&
                previousBlock != null)
            {
                TargetRenderer.SetPropertyBlock(
                    previousBlock
                );

                return;
            }

            TargetRenderer.SetPropertyBlock(
                null
            );
        }
    }
}