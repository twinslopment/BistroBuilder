using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proporciona feedback visual durante la colocación de objetos.
///
/// Comportamiento:
/// - Colocación válida: aplica el color válido.
/// - Colocación inválida: aplica el color inválido.
/// - Confirmación o cancelación: restaura el estado visual previo.
///
/// Utiliza MaterialPropertyBlock para no crear copias de
/// materiales ni modificar materiales compartidos.
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

    [Header("Renderizadores")]

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

    private bool hasActiveFeedback;

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
        if (!hasActiveFeedback ||
            activeMember == null)
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

        hasActiveFeedback =
            rendererSnapshots.Count > 0;

        if (!hasActiveFeedback &&
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

    /// <summary>
    /// Aplica el color correspondiente al resultado actual.
    /// </summary>
    private void ApplyValidationResult(
        RestaurantPlacementValidationResult result
    )
    {
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

        activeMember = null;
        hasActiveFeedback = false;
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