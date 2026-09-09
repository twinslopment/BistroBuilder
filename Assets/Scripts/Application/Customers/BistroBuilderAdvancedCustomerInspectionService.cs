using System;
using System.Text;
using UnityEngine;

/// <summary>
/// 10G. Compone una ficha legible de un cliente individual consumiendo
/// exclusivamente las autoridades de perfiles, conducta, historial y experiencia.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Customers/Advanced Customer Inspection Service")]
public sealed class BistroBuilderAdvancedCustomerInspectionService : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdvancedCustomerProfileService profileService;
    [SerializeField] private BistroBuilderAdvancedCustomerBehaviorService behaviorService;
    [SerializeField] private BistroBuilderAdvancedCustomerHistoryService historyService;
    [SerializeField] private BistroBuilderCustomerExperienceTrackingService trackingService;

    public BistroBuilderAdvancedCustomerProfileService ProfileService => profileService;
    public BistroBuilderAdvancedCustomerBehaviorService BehaviorService => behaviorService;
    public BistroBuilderAdvancedCustomerHistoryService HistoryService => historyService;
    public BistroBuilderCustomerExperienceTrackingService TrackingService => trackingService;

    private void Awake() => CacheDependencies();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (profileService == null || behaviorService == null ||
            historyService == null || trackingService == null)
        {
            error = "10G necesita perfiles, conducta, historial y Experience Tracking canónicos.";
            return false;
        }
        if (!profileService.ValidateConfiguration(out error) ||
            !behaviorService.ValidateConfiguration(out error) ||
            !historyService.ValidateConfiguration(out error) ||
            !trackingService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryBuildSnapshot(
        CustomerGroup group,
        int memberIndex,
        out BistroBuilderAdvancedCustomerInspectionSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = string.Empty;
        if (group == null || group.GroupId < 1 || memberIndex < 1)
        {
            error = "El cliente solicitado por 10G es inválido.";
            return false;
        }
        if (!profileService.TryGetGroupProfile(group.GroupId, out var groupProfile) ||
            memberIndex > groupProfile.members.Count)
        {
            error = "Todavía no existe perfil individual para este cliente.";
            return false;
        }

        BistroBuilderAdvancedCustomerMemberProfile member =
            groupProfile.members[memberIndex - 1];
        snapshot = BuildBaseSnapshot(group, groupProfile, member);
        ApplyBehavior(group.GroupId, memberIndex, snapshot);
        ApplyHistory(groupProfile, snapshot);
        ApplyExperience(group.GroupId, memberIndex, snapshot);
        snapshot.contextualMessage = BuildContextualMessage(snapshot);
        return true;
    }
    private BistroBuilderAdvancedCustomerInspectionSnapshot BuildBaseSnapshot(
        CustomerGroup group,
        BistroBuilderAdvancedCustomerGroupProfile groupProfile,
        BistroBuilderAdvancedCustomerMemberProfile member)
    {
        var result = new BistroBuilderAdvancedCustomerInspectionSnapshot
        {
            groupId = group.GroupId,
            memberIndex = member.memberIndex,
            customerId = member.customerId,
            displayName = string.IsNullOrWhiteSpace(member.displayName)
                ? "Cliente " + member.memberIndex : member.displayName,
            archetypeId = member.archetypeId,
            archetypeLabel = ResolveArchetypeLabel(member.archetypeId),
            serviceState = group.CurrentState,
            serviceStateLabel = FormatServiceState(group.CurrentState),
            mood = BistroBuilderCustomerBehaviorMood.Calm,
            moodLabel = FormatMood(BistroBuilderCustomerBehaviorMood.Calm),
            behaviorReason = BistroBuilderCustomerBehaviorReason.None,
            behaviorReasonLabel = string.Empty,
            patiencePressureBasisPoints = 0,
            remainingPatienceBasisPoints = 10000,
            patienceBand = BistroBuilderCustomerPatienceBand.High,
            patienceLabel = "Paciencia alta",
            loyaltyTier = BistroBuilderCustomerLoyaltyTier.New,
            loyaltyLabel = "Nuevo",
            specialNeeds = member.specialNeeds,
            specialNeedsLabel = FormatSpecialNeeds(member.specialNeeds)
        };
        return result;
    }

    private void ApplyBehavior(
        int groupId,
        int memberIndex,
        BistroBuilderAdvancedCustomerInspectionSnapshot snapshot)
    {
        if (!behaviorService.TryGetBehavior(groupId, out var groupBehavior) ||
            groupBehavior?.individuals == null) return;
        for (int i = 0; i < groupBehavior.individuals.Count; i++)
        {
            var individual = groupBehavior.individuals[i];
            if (individual == null || individual.memberIndex != memberIndex) continue;
            snapshot.mood = individual.mood;
            snapshot.moodLabel = FormatMood(individual.mood);
            snapshot.behaviorReason = individual.reason;
            snapshot.behaviorReasonLabel = FormatBehaviorReason(individual.reason);
            snapshot.patiencePressureBasisPoints =
                Mathf.Clamp(individual.patiencePressureBasisPoints, 0, 10000);
            snapshot.remainingPatienceBasisPoints =
                10000 - snapshot.patiencePressureBasisPoints;
            snapshot.patienceBand = ResolvePatienceBand(snapshot.remainingPatienceBasisPoints);
            snapshot.patienceLabel = FormatPatience(snapshot.patienceBand);
            return;
        }
    }
    private void ApplyHistory(
        BistroBuilderAdvancedCustomerGroupProfile groupProfile,
        BistroBuilderAdvancedCustomerInspectionSnapshot snapshot)
    {
        if (!groupProfile.returningVisit ||
            string.IsNullOrWhiteSpace(groupProfile.returningReferenceId) ||
            !historyService.TryGetCustomer(groupProfile.returningReferenceId, out var history) ||
            history == null)
            return;

        snapshot.loyaltyTier = history.loyaltyTier;
        snapshot.loyaltyLabel = FormatLoyalty(history.loyaltyTier);
    }

    private void ApplyExperience(
        int groupId,
        int memberIndex,
        BistroBuilderAdvancedCustomerInspectionSnapshot snapshot)
    {
        BistroBuilderAdvancedCustomerExperienceResult experience =
            trackingService.LastAdvancedExperience;
        if (experience == null || experience.groupId != groupId ||
            experience.individuals == null) return;
        for (int i = 0; i < experience.individuals.Count; i++)
        {
            var individual = experience.individuals[i];
            if (individual == null || individual.memberIndex != memberIndex) continue;
            snapshot.satisfactionBasisPoints =
                Mathf.Clamp(individual.overallSatisfactionBasisPoints, 0, 10000);
            snapshot.satisfactionLabel =
                FormatSatisfaction(snapshot.satisfactionBasisPoints);
            return;
        }
    }
    private string ResolveArchetypeLabel(string archetypeId)
    {
        var catalog = profileService != null ? profileService.ProfileCatalog : null;
        if (catalog?.Archetypes != null)
        {
            string normalized = BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(archetypeId);
            for (int i = 0; i < catalog.Archetypes.Count; i++)
            {
                var item = catalog.Archetypes[i];
                if (item != null &&
                    BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(item.archetypeId) == normalized)
                    return item.displayName;
            }
        }
        return string.IsNullOrWhiteSpace(archetypeId) ? "Cliente" : archetypeId;
    }

    public static BistroBuilderCustomerPatienceBand ResolvePatienceBand(int remainingBasisPoints)
    {
        int safe = Mathf.Clamp(remainingBasisPoints, 0, 10000);
        if (safe >= 7000) return BistroBuilderCustomerPatienceBand.High;
        if (safe >= 4000) return BistroBuilderCustomerPatienceBand.Medium;
        if (safe >= 1500) return BistroBuilderCustomerPatienceBand.Low;
        return BistroBuilderCustomerPatienceBand.Exhausted;
    }

    public static string FormatPatience(BistroBuilderCustomerPatienceBand band)
    {
        switch (band)
        {
            case BistroBuilderCustomerPatienceBand.High: return "Paciencia alta";
            case BistroBuilderCustomerPatienceBand.Medium: return "Paciencia media";
            case BistroBuilderCustomerPatienceBand.Low: return "Paciencia baja";
            default: return "Al límite";
        }
    }
    public static string FormatMood(BistroBuilderCustomerBehaviorMood mood)
    {
        switch (mood)
        {
            case BistroBuilderCustomerBehaviorMood.Attentive: return "Atento";
            case BistroBuilderCustomerBehaviorMood.Restless: return "Inquieto";
            case BistroBuilderCustomerBehaviorMood.Impatient: return "Impaciente";
            case BistroBuilderCustomerBehaviorMood.Critical: return "Muy molesto";
            default: return "Tranquilo";
        }
    }

    public static string FormatBehaviorReason(BistroBuilderCustomerBehaviorReason reason)
    {
        switch (reason)
        {
            case BistroBuilderCustomerBehaviorReason.TableWait: return "Espera de mesa";
            case BistroBuilderCustomerBehaviorReason.WaiterWait: return "Espera para pedir";
            case BistroBuilderCustomerBehaviorReason.FoodWait: return "Espera de comida";
            case BistroBuilderCustomerBehaviorReason.BillWait: return "Espera de cuenta";
            default: return string.Empty;
        }
    }

    public static string FormatLoyalty(BistroBuilderCustomerLoyaltyTier tier)
    {
        switch (tier)
        {
            case BistroBuilderCustomerLoyaltyTier.Returning: return "Repite visita";
            case BistroBuilderCustomerLoyaltyTier.Regular: return "Habitual";
            case BistroBuilderCustomerLoyaltyTier.Vip: return "VIP";
            default: return "Nuevo";
        }
    }
    public static string FormatSatisfaction(int scoreBasisPoints)
    {
        int score = Mathf.Clamp(scoreBasisPoints, 0, 10000);
        if (score >= 8500) return "Muy satisfecho";
        if (score >= 7000) return "Satisfecho";
        if (score >= 5000) return "Neutral";
        if (score >= 3000) return "Descontento";
        return "Muy descontento";
    }

    public static string FormatServiceState(CustomerGroupState state)
    {
        switch (state)
        {
            case CustomerGroupState.Entering: return "Entrando al restaurante";
            case CustomerGroupState.WaitingForTable: return "Esperando mesa";
            case CustomerGroupState.WalkingToTable: return "Caminando a la mesa";
            case CustomerGroupState.Seated: return "Sentándose";
            case CustomerGroupState.WaitingForWaiter: return "Esperando a que le tomen nota";
            case CustomerGroupState.Ordering: return "Pidiendo";
            case CustomerGroupState.WaitingForFood: return "Pedido realizado · esperando comida";
            case CustomerGroupState.Eating: return "Comiendo";
            case CustomerGroupState.WaitingForBill: return "Esperando la cuenta";
            case CustomerGroupState.Paying: return "Pagando";
            case CustomerGroupState.Leaving: return "Saliendo del restaurante";
            case CustomerGroupState.Finished: return "Visita terminada";
            case CustomerGroupState.WalkingToBar: return "Caminando a la barra";
            case CustomerGroupState.WaitingForBarOrder: return "Esperando para pedir en barra";
            case CustomerGroupState.OrderingAtBar: return "Pidiendo en barra";
            case CustomerGroupState.WaitingForBarItems: return "Esperando su pedido en barra";
            case CustomerGroupState.ConsumingAtBar: return "Consumiendo en barra";
            case CustomerGroupState.PayingAtBar: return "Pagando en barra";
            default: return state.ToString();
        }
    }
    public static string FormatSpecialNeeds(BistroBuilderCustomerSpecialNeed needs)
    {
        if (needs == BistroBuilderCustomerSpecialNeed.None) return string.Empty;
        var text = new StringBuilder();
        void Add(string value)
        {
            if (text.Length > 0) text.Append(" · ");
            text.Append(value);
        }
        if ((needs & BistroBuilderCustomerSpecialNeed.AccessibleSeating) != 0)
            Add("Accesibilidad");
        if ((needs & BistroBuilderCustomerSpecialNeed.QuietSeating) != 0)
            Add("Zona tranquila");
        if ((needs & BistroBuilderCustomerSpecialNeed.DietaryAwareness) != 0)
            Add("Atención dietética");
        return text.ToString();
    }

    private static string BuildContextualMessage(
        BistroBuilderAdvancedCustomerInspectionSnapshot snapshot)
    {
        bool severe = snapshot.mood >= BistroBuilderCustomerBehaviorMood.Impatient;
        switch (snapshot.behaviorReason)
        {
            case BistroBuilderCustomerBehaviorReason.TableWait:
                return severe ? "Lleva demasiado esperando una mesa." :
                    "Está pendiente de cuándo podrá sentarse.";
            case BistroBuilderCustomerBehaviorReason.WaiterWait:
                return severe ? "Se está cansando de esperar para pedir." :
                    "Está esperando a que vengan a tomarle nota.";
            case BistroBuilderCustomerBehaviorReason.FoodWait:
                return severe ? "La comida está tardando más de lo que esperaba." :
                    "Está pendiente de que llegue su comida.";
            case BistroBuilderCustomerBehaviorReason.BillWait:
                return severe ? "Quiere pagar y lleva demasiado esperando." :
                    "Está esperando la cuenta para poder pagar.";
            default:
                return "Está cómodo con el ritmo actual del servicio.";
        }
    }
    private void CacheDependencies()
    {
        if (profileService == null) TryGetComponent(out profileService);
        if (behaviorService == null) TryGetComponent(out behaviorService);
        if (historyService == null) TryGetComponent(out historyService);
        if (trackingService == null) TryGetComponent(out trackingService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
