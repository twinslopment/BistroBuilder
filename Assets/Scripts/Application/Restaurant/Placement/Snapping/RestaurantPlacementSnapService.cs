using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio universal de colocación asistida.
///
/// El controlador de interacción entrega una pose libre y este
/// servicio permite que proveedores especializados propongan una
/// pose exacta. La validación transaccional sigue siendo la autoridad
/// final: el snapping solo ayuda a alcanzar posiciones coherentes.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement Snap Service"
)]
public sealed class RestaurantPlacementSnapService :
    MonoBehaviour
{
    [Header("Inicialización")]

    [SerializeField]
    private bool discoverProvidersAutomatically = true;

    [SerializeField]
    private RestaurantPlacementSnapVisualizer visualizer;

    [Header("Depuración")]

    [SerializeField]
    private bool logProviderSummary = true;

    private readonly List<IRestaurantPlacementSnapProvider>
        providers =
            new List<IRestaurantPlacementSnapProvider>(8);

    private readonly List<RestaurantPlacementSnapCandidate>
        candidateBuffer =
            new List<RestaurantPlacementSnapCandidate>(64);

    private readonly List<RestaurantPlacementSnapHint>
        hintBuffer =
            new List<RestaurantPlacementSnapHint>(64);

    private RestaurantAreaMember activeMember;

    private bool hasCapturedTarget;

    private RestaurantPlacementSnapTargetKey capturedTarget;

    private RestaurantPlacementSnapResult currentResult;

    private bool hasCapturedValidation;

    private bool capturedValidationIsValid;

    public event Action<RestaurantPlacementSnapResult>
        SnapChanged;

    public int RegisteredProviderCount =>
        providers.Count;

    public RestaurantPlacementSnapResult CurrentResult =>
        currentResult;

    public bool HasActiveSession =>
        activeMember != null;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        RefreshProviders();
    }

    private void Start()
    {
        if (!logProviderSummary)
        {
            return;
        }

        Debug.Log(
            nameof(RestaurantPlacementSnapService) +
            " ha registrado " +
            providers.Count +
            " proveedor(es) de snapping.",
            this
        );
    }

    private void OnDisable()
    {
        EndSession();
    }

    /// <summary>
    /// Reconstruye de forma determinista la lista de proveedores.
    /// Solo se ejecuta al inicializar, instalar o validar.
    /// </summary>
    public void RefreshProviders()
    {
        providers.Clear();

        if (!discoverProvidersAutomatically)
        {
            return;
        }

        MonoBehaviour[] behaviours =
            GetComponents<MonoBehaviour>();

        for (int index = 0;
             index < behaviours.Length;
             index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour == null ||
                ReferenceEquals(behaviour, this) ||
                !(behaviour is IRestaurantPlacementSnapProvider provider))
            {
                continue;
            }

            providers.Add(provider);
        }

        providers.Sort(CompareProviders);
    }

    /// <summary>
    /// Inicia una sesión para un único artículo activo.
    /// </summary>
    public void BeginSession(RestaurantAreaMember member)
    {
        if (ReferenceEquals(activeMember, member))
        {
            return;
        }

        activeMember = member;
        hasCapturedTarget = false;
        capturedTarget = default;
        hasCapturedValidation = false;
        capturedValidationIsValid = false;

        currentResult =
            member != null
                ? RestaurantPlacementSnapResult.Unsnapped(
                    member.transform.position,
                    member.transform.rotation
                )
                : default;

        visualizer?.HideAll();
    }

    /// <summary>
    /// Finaliza la sesión y libera cualquier captura temporal.
    /// </summary>
    public void EndSession()
    {
        bool hadSession = activeMember != null ||
                          hasCapturedTarget;

        activeMember = null;
        hasCapturedTarget = false;
        capturedTarget = default;
        hasCapturedValidation = false;
        capturedValidationIsValid = false;
        currentResult = default;

        candidateBuffer.Clear();
        hintBuffer.Clear();

        visualizer?.HideAll();

        if (hadSession)
        {
            SnapChanged?.Invoke(default);
        }
    }

    /// <summary>
    /// Resuelve una pose libre contra todos los proveedores.
    ///
    /// La captura usa histéresis: entra con CaptureRadius y se libera
    /// con ReleaseRadius. Esto evita parpadeos cuando el cursor se
    /// mueve cerca del límite de una plaza.
    /// </summary>
    public bool TryResolveSnap(
        RestaurantAreaMember member,
        Vector3 rawRootPosition,
        Quaternion rawRootRotation,
        out RestaurantPlacementSnapResult result
    )
    {
        if (member == null)
        {
            result =
                RestaurantPlacementSnapResult.Unsnapped(
                    rawRootPosition,
                    rawRootRotation
                );

            return false;
        }

        if (!ReferenceEquals(activeMember, member))
        {
            BeginSession(member);
        }

        RestaurantPlacementSnapContext context =
            new RestaurantPlacementSnapContext(
                member,
                rawRootPosition,
                rawRootRotation,
                hasCapturedTarget,
                capturedTarget
            );

        CollectCandidates(context);

        int selectedIndex =
            FindCapturedCandidateIndex();

        if (selectedIndex < 0)
        {
            selectedIndex =
                FindBestCaptureCandidateIndex();
        }

        RestaurantPlacementSnapResult previousResult =
            currentResult;

        if (selectedIndex >= 0)
        {
            RestaurantPlacementSnapCandidate selected =
                candidateBuffer[selectedIndex];

            hasCapturedTarget = true;
            capturedTarget = selected.TargetKey;

            currentResult =
                new RestaurantPlacementSnapResult(
                    true,
                    selected.RootPosition,
                    selected.RootRotation,
                    selected.TargetKey,
                    selected.RelatedObject,
                    selected.HintState,
                    selected.Distance
                );
        }
        else
        {
            hasCapturedTarget = false;
            capturedTarget = default;
            hasCapturedValidation = false;
            capturedValidationIsValid = false;

            currentResult =
                RestaurantPlacementSnapResult.Unsnapped(
                    rawRootPosition,
                    rawRootRotation
                );
        }

        CollectAndRenderHints(context);
        PublishSnapChangeIfNeeded(previousResult, currentResult);

        result = currentResult;
        return currentResult.IsSnapped;
    }

    /// <summary>
    /// Informa al visualizador del resultado de la validación final
    /// de la pose capturada. Un destino capturado solo aparece verde
    /// cuando la transacción confirma que es válido.
    /// </summary>
    public void SetCurrentSnapValidation(bool isValid)
    {
        if (!hasCapturedTarget)
        {
            return;
        }

        hasCapturedValidation = true;
        capturedValidationIsValid = isValid;

        visualizer?.Render(
            hintBuffer,
            true,
            capturedTarget,
            hasCapturedValidation,
            capturedValidationIsValid
        );
    }

    /// <summary>
    /// Elimina únicamente la captura actual, manteniendo la sesión.
    /// Es útil cuando una acción manual quiere forzar una reevaluación.
    /// </summary>
    public void ReleaseCurrentCapture()
    {
        hasCapturedTarget = false;
        capturedTarget = default;
        hasCapturedValidation = false;
        capturedValidationIsValid = false;
    }

    private void CollectCandidates(
        RestaurantPlacementSnapContext context
    )
    {
        candidateBuffer.Clear();

        for (int index = 0;
             index < providers.Count;
             index++)
        {
            IRestaurantPlacementSnapProvider provider =
                providers[index];

            if (provider == null ||
                !provider.IsSnapEnabled)
            {
                continue;
            }

            provider.CollectCandidates(
                context,
                candidateBuffer
            );
        }
    }

    private void CollectAndRenderHints(
        RestaurantPlacementSnapContext context
    )
    {
        if (visualizer == null)
        {
            return;
        }

        hintBuffer.Clear();

        RestaurantPlacementSnapContext updatedContext =
            new RestaurantPlacementSnapContext(
                context.Member,
                context.RawRootPosition,
                context.RawRootRotation,
                hasCapturedTarget,
                capturedTarget
            );

        for (int index = 0;
             index < providers.Count;
             index++)
        {
            IRestaurantPlacementSnapProvider provider =
                providers[index];

            if (provider == null ||
                !provider.IsSnapEnabled)
            {
                continue;
            }

            provider.CollectVisualHints(
                updatedContext,
                hintBuffer
            );
        }

        visualizer.Render(
            hintBuffer,
            hasCapturedTarget,
            capturedTarget,
            hasCapturedValidation,
            capturedValidationIsValid
        );
    }

    private int FindCapturedCandidateIndex()
    {
        if (!hasCapturedTarget)
        {
            return -1;
        }

        for (int index = 0;
             index < candidateBuffer.Count;
             index++)
        {
            RestaurantPlacementSnapCandidate candidate =
                candidateBuffer[index];

            if (candidate.TargetKey == capturedTarget &&
                candidate.Distance <= candidate.ReleaseRadius)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindBestCaptureCandidateIndex()
    {
        int bestIndex = -1;

        for (int index = 0;
             index < candidateBuffer.Count;
             index++)
        {
            RestaurantPlacementSnapCandidate candidate =
                candidateBuffer[index];

            if (candidate.Provider == null ||
                candidate.Distance > candidate.CaptureRadius)
            {
                continue;
            }

            if (bestIndex < 0 ||
                CompareCandidates(
                    candidate,
                    candidateBuffer[bestIndex]
                ) < 0)
            {
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static int CompareCandidates(
        RestaurantPlacementSnapCandidate first,
        RestaurantPlacementSnapCandidate second
    )
    {
        int priorityComparison =
            first.Provider.Priority.CompareTo(
                second.Provider.Priority
            );

        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        float firstNormalizedDistance =
            first.Distance /
            Mathf.Max(0.01f, first.CaptureRadius);

        float secondNormalizedDistance =
            second.Distance /
            Mathf.Max(0.01f, second.CaptureRadius);

        int scoreComparison =
            (
                firstNormalizedDistance +
                first.PreferenceScore
            ).CompareTo(
                secondNormalizedDistance +
                second.PreferenceScore
            );

        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        int distanceComparison =
            first.Distance.CompareTo(second.Distance);

        if (distanceComparison != 0)
        {
            return distanceComparison;
        }

        int relatedComparison =
            first.TargetKey.RelatedObjectInstanceId.CompareTo(
                second.TargetKey.RelatedObjectInstanceId
            );

        if (relatedComparison != 0)
        {
            return relatedComparison;
        }

        return first.TargetKey.LocalTargetId.CompareTo(
            second.TargetKey.LocalTargetId
        );
    }

    private static int CompareProviders(
        IRestaurantPlacementSnapProvider first,
        IRestaurantPlacementSnapProvider second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int priorityComparison =
            first.Priority.CompareTo(second.Priority);

        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        return string.Compare(
            first.GetType().FullName,
            second.GetType().FullName,
            StringComparison.Ordinal
        );
    }

    private void PublishSnapChangeIfNeeded(
        RestaurantPlacementSnapResult previous,
        RestaurantPlacementSnapResult current
    )
    {
        bool changed =
            previous.IsSnapped != current.IsSnapped ||
            previous.TargetKey != current.TargetKey;

        if (changed)
        {
            SnapChanged?.Invoke(current);
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (visualizer == null)
        {
            TryGetComponent(out visualizer);
        }
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
}
