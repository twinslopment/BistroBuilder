using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BistroBuilder.ConstructionAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class BistroBuilderB7AdversarialSnapProvider :
    MonoBehaviour,
    IRestaurantPlacementSnapProvider
{
    public bool PrimaryBlocked { get; set; }

    public int Priority => 1;
    public bool IsSnapEnabled => true;

    public void CollectCandidates(
        RestaurantPlacementSnapContext context,
        List<RestaurantPlacementSnapCandidate> results)
    {
        if (context.Member == null || results == null)
            return;

        results.Add(
            new RestaurantPlacementSnapCandidate(
                this,
                new RestaurantPlacementSnapTargetKey(
                    GetInstanceID(),
                    7001,
                    1),
                context.RawRootPosition + Vector3.right * 0.05f,
                context.RawRootRotation,
                0.05f,
                0.60f,
                0.90f,
                -0.25f,
                this,
                PrimaryBlocked
                    ? RestaurantPlacementSnapHintState.Blocked
                    : RestaurantPlacementSnapHintState.Available));

        results.Add(
            new RestaurantPlacementSnapCandidate(
                this,
                new RestaurantPlacementSnapTargetKey(
                    GetInstanceID(),
                    7002,
                    2),
                context.RawRootPosition + Vector3.right * 0.20f,
                context.RawRootRotation,
                0.20f,
                0.60f,
                0.90f,
                0f,
                this,
                RestaurantPlacementSnapHintState.Available));
    }

    public void CollectVisualHints(
        RestaurantPlacementSnapContext context,
        List<RestaurantPlacementSnapHint> results)
    {
    }
}

public static class BistroBuilderEditorV2B7ContextualSnappingSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private static readonly List<string> Lines = new List<string>(220);
    private static readonly List<Object> Cleanup = new List<Object>(64);
    private static int pass;
    private static int fail;

    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem("Bistro Builder/QA/Editor V2/B7 Contextual Snapping Self Test")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static bool Run()
    {
        Lines.Clear();
        Cleanup.Clear();
        pass = 0;
        fail = 0;
        Lines.Add("EDITOR V2 - B7 CONTEXTUAL SNAPPING SELF TEST");

        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InvokeBootstrap(typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap));
            InvokeBootstrap(typeof(BistroBuilderEditorV2RuntimeBootstrap));
            InvokeStaticPrivate(
                typeof(BistroBuilderUniversalPreviewService),
                "EnsureRuntimeInstallation");

            RestaurantPlacementSnapService sceneSnap =
                Find<RestaurantPlacementSnapService>();
            RestaurantContextualPlacementSnapProvider sceneContextual =
                Find<RestaurantContextualPlacementSnapProvider>();
            RestaurantSeatingSnapProvider seatingProvider =
                Find<RestaurantSeatingSnapProvider>();

            Check(sceneSnap != null, "B7 conserva la autoridad universal de snapping existente");
            Check(sceneContextual != null, "B7 instala el provider contextual por perfiles");
            Check(seatingProvider != null, "B7 conserva el provider silla-mesa");
            if (sceneSnap != null)
            {
                sceneSnap.RefreshProviders();
                Check(
                    sceneSnap.RegisteredProviderCount >= 2,
                    "B7 combina providers especializados sin hardcodear un único tipo");
            }

            // Rig aislado: producción real, sin visualizador, para pruebas deterministas.
            GameObject rig = NewGo("__B7_SnapRig");
            RestaurantContextualPlacementSnapProvider contextual =
                rig.AddComponent<RestaurantContextualPlacementSnapProvider>();
            RestaurantPlacementSnapService snap =
                rig.AddComponent<RestaurantPlacementSnapService>();
            snap.RefreshProviders();

            Check(
                snap.RegisteredProviderCount == 1,
                "B7 provider contextual implementa el contrato universal real");

            RestaurantPlacementSnapProfile floorProfile =
                Profile(
                    "floor",
                    RestaurantPlacementSnapTargetKind.Floor,
                    0.60f,
                    0.85f,
                    false);
            RestaurantPlacementSnapProfile wallProfile =
                Profile(
                    "wall",
                    RestaurantPlacementSnapTargetKind.Wall,
                    0.60f,
                    0.85f,
                    true);
            RestaurantPlacementSnapProfile surfaceProfile =
                Profile(
                    "surface",
                    RestaurantPlacementSnapTargetKind.Surface,
                    0.60f,
                    0.85f,
                    false);
            RestaurantPlacementSnapProfile ceilingProfile =
                Profile(
                    "ceiling",
                    RestaurantPlacementSnapTargetKind.Ceiling,
                    0.60f,
                    0.85f,
                    true);

            RestaurantAreaMember member = Member("__B7_GenericAsset", floorProfile);
            Vector3 originalPosition = member.transform.position;
            Quaternion originalRotation = member.transform.rotation;

            RestaurantPlacementSnapTarget floor = Target(
                "__B7_Floor",
                new Vector3(0f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Floor,
                new Vector2(0.6f, 0.6f),
                Vector3.up,
                Vector3.forward);

            RestaurantPlacementSnapTarget wall = Target(
                "__B7_Wall",
                new Vector3(5f, 1f, 0f),
                RestaurantPlacementSnapTargetKind.Wall,
                new Vector2(1.2f, 1.2f),
                Vector3.right,
                Vector3.up);

            RestaurantPlacementSnapTarget surface = Target(
                "__B7_Surface",
                new Vector3(10f, 1f, 0f),
                RestaurantPlacementSnapTargetKind.Surface,
                new Vector2(0.8f, 0.8f),
                Vector3.up,
                Vector3.forward);

            RestaurantPlacementSnapTarget ceiling = Target(
                "__B7_Ceiling",
                new Vector3(15f, 3f, 0f),
                RestaurantPlacementSnapTargetKind.Ceiling,
                new Vector2(1f, 1f),
                Vector3.down,
                Vector3.forward);

            // Floor contract.
            snap.BeginSession(member);
            bool floorSnapped = snap.TryResolveSnap(
                member,
                new Vector3(0.10f, 0.20f, 0.05f),
                Quaternion.Euler(0f, 37f, 0f),
                out RestaurantPlacementSnapResult floorResult);
            Check(
                floorSnapped &&
                ReferenceEquals(floorResult.RelatedObject, floor),
                "B7 contrato Floor propone únicamente superficie de suelo compatible");
            Check(
                Quaternion.Angle(
                    floorResult.RootRotation,
                    Quaternion.Euler(0f, 37f, 0f)) < 0.01f,
                "B7 perfil sin alineación conserva la rotación manual");
            Example("FLOOR", floorResult);

            RestaurantPlacementSnapTargetKey firstFloorKey =
                floorResult.TargetKey;
            snap.EndSession();
            snap.BeginSession(member);
            snap.TryResolveSnap(
                member,
                new Vector3(0.10f, 0.20f, 0.05f),
                Quaternion.Euler(0f, 37f, 0f),
                out RestaurantPlacementSnapResult repeatedFloor);
            Check(
                repeatedFloor.TargetKey == firstFloorKey,
                "B7 identidad de target contextual es estable entre sesiones");

            // Wall contract + target rotation.
            SetProfile(member, wallProfile);
            snap.EndSession();
            snap.BeginSession(member);
            bool wallSnapped = snap.TryResolveSnap(
                member,
                new Vector3(5.15f, 1.05f, 0.05f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult wallResult);
            Check(
                wallSnapped &&
                ReferenceEquals(wallResult.RelatedObject, wall),
                "B7 contrato Wall ignora suelo/superficie/techo");
            Check(
                Vector3.Dot(
                    wallResult.RootRotation * Vector3.up,
                    wall.WorldNormal) > 0.999f,
                "B7 objeto mural puede alinear su orientación al plano host");
            Example("WALL", wallResult);

            // Surface contract.
            SetProfile(member, surfaceProfile);
            snap.EndSession();
            snap.BeginSession(member);
            bool surfaceSnapped = snap.TryResolveSnap(
                member,
                new Vector3(10.05f, 1.18f, 0.05f),
                Quaternion.Euler(0f, 12f, 0f),
                out RestaurantPlacementSnapResult surfaceResult);
            Check(
                surfaceSnapped &&
                ReferenceEquals(surfaceResult.RelatedObject, surface),
                "B7 contrato Surface propone apoyo sobre superficie");
            Example("SURFACE", surfaceResult);

            // Ceiling contract.
            SetProfile(member, ceilingProfile);
            snap.EndSession();
            snap.BeginSession(member);
            bool ceilingSnapped = snap.TryResolveSnap(
                member,
                new Vector3(15.05f, 2.80f, 0.02f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult ceilingResult);
            Check(
                ceilingSnapped &&
                ReferenceEquals(ceilingResult.RelatedObject, ceiling),
                "B7 contrato Ceiling propone techo y no otro tipo de destino");
            Check(
                Vector3.Dot(
                    ceilingResult.RootRotation * Vector3.up,
                    ceiling.WorldNormal) > 0.999f,
                "B7 objeto de techo puede orientar su eje al techo");
            Example("CEILING", ceilingResult);

            // Relationship/socket contracts.
            RestaurantPlacementSnapProfile relationProfile =
                Profile(
                    "group.a",
                    RestaurantPlacementSnapTargetKind.Socket,
                    0.60f,
                    0.90f,
                    true,
                    "group.a");

            RestaurantPlacementSnapTarget wrongRelation = Target(
                "__B7_RelationWrong",
                new Vector3(20f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Socket,
                new Vector2(0.2f, 0.2f),
                Vector3.up,
                Vector3.forward,
                "group.b");

            SetProfile(member, relationProfile);
            snap.EndSession();
            snap.BeginSession(member);
            bool wrongRelationSnapped = snap.TryResolveSnap(
                member,
                new Vector3(20f, 0.1f, 0f),
                Quaternion.identity,
                out _);
            Check(
                !wrongRelationSnapped,
                "B7 relación de conjunto incompatible no genera sugerencia");

            RestaurantPlacementSnapTarget genericSocket = Target(
                "__B7_GenericSocket",
                new Vector3(22f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Socket,
                new Vector2(0.2f, 0.2f),
                Vector3.up,
                Vector3.forward);

            snap.EndSession();
            snap.BeginSession(member);
            bool genericSocketCapturedByRelation = snap.TryResolveSnap(
                member,
                new Vector3(22f, 0.1f, 0f),
                Quaternion.identity,
                out _);
            Check(
                !genericSocketCapturedByRelation,
                "B7 un socket relacional exige contrato explícito y no captura un target genérico");

            wrongRelation.EditorConfigure(
                RestaurantPlacementSnapTargetKind.Socket,
                new Vector2(0.2f, 0.2f),
                Vector3.zero,
                Vector3.up,
                Vector3.forward,
                "group.a");

            bool rightRelationSnapped = snap.TryResolveSnap(
                member,
                new Vector3(20f, 0.1f, 0f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult relationResult);
            Check(
                rightRelationSnapped &&
                ReferenceEquals(relationResult.RelatedObject, wrongRelation),
                "B7 relaciones compatibles se resuelven mediante contrato, no por nombre de asset");
            Example("RELATION", relationResult);

            RestaurantPlacementSnapProfile selfProfile =
                Profile(
                    "self.only",
                    RestaurantPlacementSnapTargetKind.Socket,
                    0.70f,
                    0.95f,
                    true,
                    "self.only");
            RestaurantPlacementSnapTarget selfTarget = Target(
                "__B7_SelfSocket",
                new Vector3(24f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Socket,
                new Vector2(0.2f, 0.2f),
                Vector3.up,
                Vector3.forward,
                "self.only");
            selfTarget.transform.SetParent(member.transform, true);

            SetProfile(member, selfProfile);
            snap.EndSession();
            snap.BeginSession(member);
            bool selfSnapped = snap.TryResolveSnap(
                member,
                new Vector3(24f, 0.1f, 0f),
                Quaternion.identity,
                out _);
            Check(
                !selfSnapped,
                "B7 un asset no puede hacerse snap a un socket de su propia jerarquía");

            // Safety/usefulness: blocked nearest target must lose to a farther valid target.
            RestaurantPlacementSnapProfile safeProfile =
                Profile(
                    "safe",
                    RestaurantPlacementSnapTargetKind.Floor,
                    0.70f,
                    0.95f,
                    false,
                    "safe");

            RestaurantPlacementSnapTarget blocked = Target(
                "__B7_BlockedNearest",
                new Vector3(30f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Floor,
                new Vector2(0.1f, 0.1f),
                Vector3.up,
                Vector3.forward,
                "safe",
                true);

            RestaurantPlacementSnapTarget safe = Target(
                "__B7_SafeFarther",
                new Vector3(30.35f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Floor,
                new Vector2(0.1f, 0.1f),
                Vector3.up,
                Vector3.forward,
                "safe");

            SetProfile(member, safeProfile);
            snap.EndSession();
            snap.BeginSession(member);
            bool safeSnapped = snap.TryResolveSnap(
                member,
                new Vector3(30.03f, 0.08f, 0f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult safeResult);
            Check(
                safeSnapped &&
                ReferenceEquals(safeResult.RelatedObject, safe),
                "B7 descarta destino bloqueado aunque sea el más cercano");
            Check(
                !ReferenceEquals(safeResult.RelatedObject, blocked),
                "B7 nunca ofrece target marcado Blocked como candidato");
            Example("SAFE_NEAREST", safeResult);

            GameObject guardRig = NewGo("__B7_AdversarialGuardRig");
            BistroBuilderB7AdversarialSnapProvider adversarial =
                guardRig.AddComponent<BistroBuilderB7AdversarialSnapProvider>();
            RestaurantPlacementSnapService guardedSnap =
                guardRig.AddComponent<RestaurantPlacementSnapService>();
            guardedSnap.RefreshProviders();
            guardedSnap.BeginSession(member);

            adversarial.PrimaryBlocked = false;
            guardedSnap.TryResolveSnap(
                member,
                Vector3.zero,
                Quaternion.identity,
                out RestaurantPlacementSnapResult initiallyCaptured);
            Check(
                initiallyCaptured.IsSnapped &&
                initiallyCaptured.TargetKey.LocalTargetId == 1,
                "B7 captura un candidato disponible incluso con provider adversarial");

            adversarial.PrimaryBlocked = true;
            guardedSnap.TryResolveSnap(
                member,
                Vector3.zero,
                Quaternion.identity,
                out RestaurantPlacementSnapResult guardedResult);
            Check(
                guardedResult.IsSnapped &&
                guardedResult.TargetKey.LocalTargetId == 2 &&
                guardedResult.HintState ==
                    RestaurantPlacementSnapHintState.Available,
                "B7 el núcleo libera un target que pasa a Blocked y elige el candidato seguro");
            guardedSnap.EndSession();

            // Hysteresis.
            RestaurantPlacementSnapProfile hysteresisProfile =
                Profile(
                    "hysteresis",
                    RestaurantPlacementSnapTargetKind.Surface,
                    0.40f,
                    0.80f,
                    false,
                    "hysteresis");

            RestaurantPlacementSnapTarget hysteresisA = Target(
                "__B7_Hysteresis_A",
                new Vector3(40f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Surface,
                new Vector2(0.1f, 0.1f),
                Vector3.up,
                Vector3.forward,
                "hysteresis");
            RestaurantPlacementSnapTarget hysteresisB = Target(
                "__B7_Hysteresis_B",
                new Vector3(40.60f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Surface,
                new Vector2(0.1f, 0.1f),
                Vector3.up,
                Vector3.forward,
                "hysteresis");

            SetProfile(member, hysteresisProfile);
            snap.EndSession();
            snap.BeginSession(member);
            snap.TryResolveSnap(
                member,
                new Vector3(40.02f, 0.05f, 0f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult capturedA);
            snap.TryResolveSnap(
                member,
                new Vector3(40.38f, 0.05f, 0f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult retainedA);
            Check(
                ReferenceEquals(capturedA.RelatedObject, hysteresisA) &&
                ReferenceEquals(retainedA.RelatedObject, hysteresisA),
                "B7 histéresis evita saltos de target por pequeños cambios del cursor");

            snap.TryResolveSnap(
                member,
                new Vector3(40.95f, 0.05f, 0f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult switchedB);
            Check(
                ReferenceEquals(switchedB.RelatedObject, hysteresisB),
                "B7 libera el target al superar ReleaseRadius y adopta el mejor candidato");
            Example("HYSTERESIS_SWITCH", switchedB);

            // Explicit bypass: Alt path delegates here with suppressSnapping=true.
            Vector3 rawBypass = new Vector3(40.02f, 0.17f, 0.03f);
            Quaternion rawBypassRotation = Quaternion.Euler(0f, 73f, 0f);
            bool bypassSnapped = snap.TryResolveSnap(
                member,
                rawBypass,
                rawBypassRotation,
                true,
                out RestaurantPlacementSnapResult bypassResult);
            Check(
                !bypassSnapped &&
                Nearly(bypassResult.RootPosition, rawBypass) &&
                Quaternion.Angle(bypassResult.RootRotation, rawBypassRotation) < 0.001f,
                "B7 Alt/override permite ignorar totalmente la sugerencia");
            Check(
                !snap.CurrentResult.IsSnapped,
                "B7 bypass libera cualquier captura anterior");
            Example("BYPASS", bypassResult);

            // Pure proposal: snapping never mutates the object.
            Check(
                Nearly(member.transform.position, originalPosition) &&
                Quaternion.Angle(member.transform.rotation, originalRotation) < 0.001f,
                "B7 snapping propone pose sin modificar Transform real");

            // No hardcode by asset ID/name.
            RestaurantAreaMember arbitraryA =
                Member("BB_Chair_Arbitrary_999", floorProfile);
            RestaurantAreaMember arbitraryB =
                Member("TotallyDifferentAssetName", floorProfile);
            snap.EndSession();
            snap.BeginSession(arbitraryA);
            snap.TryResolveSnap(
                arbitraryA,
                new Vector3(0.1f, 0.2f, 0.05f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult arbitraryResultA);
            snap.EndSession();
            snap.BeginSession(arbitraryB);
            snap.TryResolveSnap(
                arbitraryB,
                new Vector3(0.1f, 0.2f, 0.05f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult arbitraryResultB);
            Check(
                arbitraryResultA.IsSnapped &&
                arbitraryResultB.IsSnapped &&
                arbitraryResultA.TargetKey.RelatedObjectInstanceId ==
                    arbitraryResultB.TargetKey.RelatedObjectInstanceId,
                "B7 comportamiento depende del perfil, no de IDs/nombres de assets");

            // Canonical content path: the profile can live on the catalog definition.
            RestaurantPlaceableItemDefinition itemDefinition =
                ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            itemDefinition.name = "__B7_ItemDefinition";
            Cleanup.Add(itemDefinition);
            SerializedObject serializedDefinition =
                new SerializedObject(itemDefinition);
            serializedDefinition.FindProperty("snapProfile").objectReferenceValue =
                floorProfile;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            GameObject definitionGo = NewGo("__B7_DefinitionDrivenAsset");
            RestaurantPlaceableObject definitionPlaceable =
                definitionGo.AddComponent<RestaurantPlaceableObject>();
            definitionPlaceable.SetItemDefinition(itemDefinition);
            RestaurantAreaMember definitionMember =
                definitionGo.GetComponent<RestaurantAreaMember>();

            snap.EndSession();
            snap.BeginSession(definitionMember);
            bool definitionSnap = snap.TryResolveSnap(
                definitionMember,
                new Vector3(0.08f, 0.15f, 0.04f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult definitionResult);

            Check(
                definitionSnap &&
                ReferenceEquals(definitionResult.RelatedObject, floor),
                "B7 lee el perfil canónico desde RestaurantPlaceableItemDefinition/SAVIC");

            // Spatial authority: a snap outside the restaurant is still rejected by Placement.
            RestaurantPlacementValidationService validator =
                Find<RestaurantPlacementValidationService>();
            RestaurantAreaRegistry areaRegistry = Find<RestaurantAreaRegistry>();
            RestaurantAreaAssignmentService areaAssignment =
                Find<RestaurantAreaAssignmentService>();
            InvokeLifecycle(areaRegistry, "OnEnable");
            InvokeLifecycle(areaRegistry, "Start");
            InvokeLifecycle(areaAssignment, "Awake");
            InvokeLifecycle(areaAssignment, "Start");
            InvokeLifecycle(validator, "Awake");

            RestaurantPlacementSnapProfile authorityProfile =
                Profile(
                    "authority",
                    RestaurantPlacementSnapTargetKind.Surface,
                    1f,
                    1.2f,
                    false,
                    "authority");
            RestaurantPlacementSnapTarget outside = Target(
                "__B7_OutsideRestaurant",
                new Vector3(1000f, 0f, 1000f),
                RestaurantPlacementSnapTargetKind.Surface,
                Vector2.one,
                Vector3.up,
                Vector3.forward,
                "authority");

            SetProfile(member, authorityProfile);
            snap.EndSession();
            snap.BeginSession(member);
            bool outsideSnap = snap.TryResolveSnap(
                member,
                new Vector3(1000f, 0.1f, 1000f),
                Quaternion.identity,
                out RestaurantPlacementSnapResult outsideResult);
            Check(outsideSnap, "B7 puede proponer una pose aunque todavía no la haya validado Placement");

            RestaurantPlacementValidationResult authorityValidation =
                validator != null
                    ? validator.ValidatePlacement(
                        member,
                        outsideResult.RootPosition,
                        outsideResult.RootRotation)
                    : default;

            Check(
                validator != null && !authorityValidation.IsValid,
                "B7 no invalida la autoridad espacial: Placement/BBSIS rechaza la propuesta inválida");
            Lines.Add(
                "EXAMPLE - AUTHORITY snap=True placementValid=" +
                authorityValidation.IsValid +
                " status=" + authorityValidation.Status);

            // Chair <-> table provider using real scene seating topology.
            RestaurantTableRegistry tableRegistry = Find<RestaurantTableRegistry>();
            RestaurantSeatRegistry seatRegistry = Find<RestaurantSeatRegistry>();
            RestaurantSeatingTopologyService topology = Find<RestaurantSeatingTopologyService>();
            InvokeLifecycle(tableRegistry, "Awake");
            InvokeLifecycle(tableRegistry, "Start");
            InvokeLifecycle(seatRegistry, "Awake");
            InvokeLifecycle(seatRegistry, "OnEnable");
            InvokeLifecycle(seatRegistry, "Start");
            InvokeLifecycle(topology, "Awake");
            InvokeLifecycle(topology, "OnEnable");
            topology?.RebuildImmediately();
            InvokeLifecycle(seatingProvider, "Awake");

            RestaurantSeat[] seats = Object.FindObjectsByType<RestaurantSeat>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            RestaurantSeat candidateSeat = null;
            RestaurantSeat occupiedSeat = null;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] == null ||
                    !seats[i].IsAssociated ||
                    seats[i].UseProfile == null ||
                    seats[i].GetComponent<RestaurantAreaMember>() == null)
                    continue;

                candidateSeat = seats[i];
                for (int j = 0; j < seats.Length; j++)
                {
                    if (i == j || seats[j] == null || !seats[j].IsAssociated)
                        continue;
                    if (ReferenceEquals(
                            seats[j].AssociatedTable,
                            candidateSeat.AssociatedTable) &&
                        seats[j].AssociatedSlotIndex !=
                            candidateSeat.AssociatedSlotIndex)
                    {
                        occupiedSeat = seats[j];
                        break;
                    }
                }
                if (occupiedSeat != null) break;
            }

            Check(
                candidateSeat != null,
                "B7 encuentra una silla real asociada para probar silla-mesa");

            if (candidateSeat != null)
            {
                List<RestaurantTableSeatSlot> slots =
                    new List<RestaurantTableSeatSlot>(16);
                int slotCount =
                    candidateSeat.AssociatedTable.WriteCurrentSlots(slots);
                RestaurantTableSeatSlot ownSlot = default;
                bool ownFound = false;
                for (int i = 0; i < slotCount; i++)
                {
                    if (slots[i].SlotIndex ==
                        candidateSeat.AssociatedSlotIndex)
                    {
                        ownSlot = slots[i];
                        ownFound = true;
                        break;
                    }
                }

                List<RestaurantPlacementSnapCandidate> seatingCandidates =
                    new List<RestaurantPlacementSnapCandidate>(16);
                Quaternion ownRotation =
                    candidateSeat.CalculateRootRotationForFacingDirection(
                        ownSlot.FacingDirection);
                Vector3 ownRoot =
                    candidateSeat.CalculateRootPositionForAssociationAtPose(
                        ownSlot.AssociationPosition,
                        ownRotation);

                seatingProvider.CollectCandidates(
                    new RestaurantPlacementSnapContext(
                        candidateSeat.GetComponent<RestaurantAreaMember>(),
                        ownRoot,
                        ownRotation,
                        false,
                        default),
                    seatingCandidates);

                bool ownCandidate = false;
                for (int i = 0; i < seatingCandidates.Count; i++)
                {
                    if (seatingCandidates[i].TargetKey.LocalTargetId ==
                        ownSlot.SlotIndex)
                    {
                        ownCandidate = true;
                        break;
                    }
                }

                Check(
                    ownFound && ownCandidate,
                    "B7 silla real captura una plaza libre de su mesa");

                if (occupiedSeat != null)
                {
                    RestaurantTableSeatSlot occupiedSlot = default;
                    bool occupiedFound = false;
                    for (int i = 0; i < slotCount; i++)
                    {
                        if (slots[i].SlotIndex ==
                            occupiedSeat.AssociatedSlotIndex)
                        {
                            occupiedSlot = slots[i];
                            occupiedFound = true;
                            break;
                        }
                    }

                    seatingCandidates.Clear();
                    Quaternion occupiedRotation =
                        candidateSeat.CalculateRootRotationForFacingDirection(
                            occupiedSlot.FacingDirection);
                    Vector3 occupiedRoot =
                        candidateSeat.CalculateRootPositionForAssociationAtPose(
                            occupiedSlot.AssociationPosition,
                            occupiedRotation);

                    seatingProvider.CollectCandidates(
                        new RestaurantPlacementSnapContext(
                            candidateSeat.GetComponent<RestaurantAreaMember>(),
                            occupiedRoot,
                            occupiedRotation,
                            false,
                            default),
                        seatingCandidates);

                    bool offeredOccupied = false;
                    for (int i = 0; i < seatingCandidates.Count; i++)
                    {
                        if (seatingCandidates[i].TargetKey.LocalTargetId ==
                            occupiedSlot.SlotIndex)
                        {
                            offeredOccupied = true;
                            break;
                        }
                    }

                    Check(
                        occupiedFound && !offeredOccupied,
                        "B7 plaza de mesa ocupada se visualiza pero nunca se ofrece como snap");
                    Lines.Add(
                        "EXAMPLE - CHAIR_TABLE freeSlot=" +
                        ownSlot.SlotIndex +
                        " occupiedSlot=" +
                        occupiedSlot.SlotIndex +
                        " occupiedOffered=False");
                }
                else
                {
                    Check(
                        false,
                        "B7 necesita dos sillas reales asociadas a la misma mesa para probar ocupación");
                }
            }

            // Door/window host: nearest compatible host, not merely nearest wall.
            BistroBuilderEditDocument openingDoc =
                new BistroBuilderEditDocument { documentId = "b7-opening-host" };
            openingDoc.walls.Add(
                Wall("short_near", 0f, 0f, 0.55f, 0f));
            openingDoc.walls.Add(
                Wall("valid_farther", -2f, 0.22f, 2f, 0.22f));
            BistroBuilderEditSession openingSession =
                new BistroBuilderEditSession(openingDoc);
            ArchitectureQueryCache openingQueries = new ArchitectureQueryCache();
            openingQueries.Refresh(openingSession);

            ArchitectureHit openingHost =
                openingQueries.PickOpeningHost(
                    new Vector2(0.25f, 0.02f),
                    0.5f,
                    0.9f,
                    0f,
                    2.1f,
                    "default");

            Check(
                openingHost.IsValid &&
                openingHost.Id.Value == "valid_farther",
                "B7 puerta ignora pared más cercana incompatible y elige host válido");
            Lines.Add(
                "EXAMPLE - OPENING_HOST chosen=" +
                (openingHost.IsValid ? openingHost.Id.Value : "none") +
                " nearInvalid=short_near");

            BistroBuilderEditDocument noHostDoc =
                new BistroBuilderEditDocument { documentId = "b7-no-host" };
            noHostDoc.walls.Add(
                Wall("short_only", 0f, 0f, 0.4f, 0f));
            BistroBuilderEditSession noHostSession =
                new BistroBuilderEditSession(noHostDoc);
            ArchitectureQueryCache noHostQueries = new ArchitectureQueryCache();
            noHostQueries.Refresh(noHostSession);
            Check(
                !noHostQueries.PickOpeningHost(
                    new Vector2(0.2f, 0.01f),
                    0.5f,
                    0.9f,
                    0f,
                    2.1f,
                    "default").IsValid,
                "B7 puerta/ventana no hace snap a host geométricamente incompatible");

            // Deterministic tie.
            BistroBuilderEditDocument tieDoc =
                new BistroBuilderEditDocument { documentId = "b7-tie" };
            tieDoc.walls.Add(Wall("b", -2f, 0.2f, 2f, 0.2f));
            tieDoc.walls.Add(Wall("a", -2f, -0.2f, 2f, -0.2f));
            BistroBuilderEditSession tieSession = new BistroBuilderEditSession(tieDoc);
            ArchitectureQueryCache tieQueries = new ArchitectureQueryCache();
            tieQueries.Refresh(tieSession);
            ArchitectureHit tieHit = tieQueries.PickOpeningHost(
                Vector2.zero,
                0.5f,
                0.9f,
                0f,
                2.1f,
                "default");
            Check(
                tieHit.IsValid && tieHit.Id.Value == "a",
                "B7 empate de hosts es determinista por identidad canónica");

            // Fuzz/property battery.
            RestaurantPlacementSnapProfile fuzzProfile =
                Profile(
                    "fuzz",
                    RestaurantPlacementSnapTargetKind.Floor,
                    0.65f,
                    0.90f,
                    false,
                    "fuzz");
            RestaurantPlacementSnapTarget fuzzTarget = Target(
                "__B7_FuzzTarget",
                new Vector3(60f, 0f, 0f),
                RestaurantPlacementSnapTargetKind.Floor,
                new Vector2(0.5f, 0.5f),
                Vector3.up,
                Vector3.forward,
                "fuzz");
            SetProfile(member, fuzzProfile);
            snap.EndSession();
            snap.BeginSession(member);

            Vector3 transformBeforeFuzz = member.transform.position;
            bool fuzzFinite = true;
            bool fuzzTransformStable = true;
            const int fuzzIterations = 5000;
            for (int i = 0; i < fuzzIterations; i++)
            {
                float x = 60f + ((i * 37) % 181 - 90) * 0.01f;
                float z = ((i * 53) % 181 - 90) * 0.01f;
                float y = ((i * 17) % 41) * 0.01f;
                snap.TryResolveSnap(
                    member,
                    new Vector3(x, y, z),
                    Quaternion.Euler(0f, i % 360, 0f),
                    out RestaurantPlacementSnapResult fuzzResult);
                fuzzFinite &=
                    Finite(fuzzResult.RootPosition) &&
                    Finite(fuzzResult.RootRotation);
                fuzzTransformStable &=
                    Nearly(member.transform.position, transformBeforeFuzz);
            }
            Check(
                fuzzFinite,
                "B7 fuzz 5.000 poses: ninguna propuesta produce NaN/Infinity");
            Check(
                fuzzTransformStable,
                "B7 fuzz 5.000 poses: ninguna propuesta muta el objeto");

            // Allocation/performance battery on isolated real service.
            snap.ReleaseCurrentCapture();
            for (int i = 0; i < 250; i++)
            {
                snap.TryResolveSnap(
                    member,
                    new Vector3(60.05f, 0.1f, 0.04f),
                    Quaternion.identity,
                    out _);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            const int stressIterations = 10000;
            for (int i = 0; i < stressIterations; i++)
            {
                float x = 60f + (i % 7) * 0.01f;
                snap.TryResolveSnap(
                    member,
                    new Vector3(x, 0.08f, 0.03f),
                    Quaternion.identity,
                    out _);
            }
            stopwatch.Stop();
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - allocationBefore;

            Check(
                stopwatch.ElapsedMilliseconds < 3000,
                "B7 rendimiento: 10.000 resolves contextuales < 3 s (" +
                stopwatch.ElapsedMilliseconds + " ms)");
            Check(
                allocated < 1024L * 1024L,
                "B7 asignaciones acotadas en 10.000 resolves (" +
                allocated + " bytes)");

            Lines.Add(
                "METRIC - SNAP_10000_MS=" +
                stopwatch.ElapsedMilliseconds);
            Lines.Add(
                "METRIC - SNAP_10000_ALLOCATED_BYTES=" +
                allocated);
            Lines.Add(
                "METRIC - FUZZ_ITERATIONS=" + fuzzIterations);

            Example(
                "FUZZ_FINAL",
                snap.CurrentResult);

            // Existing universal preview integration remains live through B6 path.
            Check(
                Object.FindFirstObjectByType<BistroBuilderUniversalPreviewService>(
                    FindObjectsInactive.Include) != null,
                "B7 conserva Universal Preview B6 para mostrar las sugerencias");

            // Explicitly close sessions.
            snap.EndSession();
            sceneSnap?.EndSession();
        }
        catch (Exception ex)
        {
            fail++;
            Lines.Add("EXCEPTION - " + ex);
        }
        finally
        {
            for (int i = Cleanup.Count - 1; i >= 0; i--)
            {
                if (Cleanup[i] != null)
                    Object.DestroyImmediate(Cleanup[i]);
            }
            Cleanup.Clear();
        }

        return Finish();
    }

    private static RestaurantPlacementSnapProfile Profile(
        string name,
        RestaurantPlacementSnapTargetKind kinds,
        float capture,
        float release,
        bool align,
        string relation = "")
    {
        RestaurantPlacementSnapProfile profile =
            ScriptableObject.CreateInstance<RestaurantPlacementSnapProfile>();
        profile.name = "__B7_Profile_" + name;
        profile.EditorConfigure(
            kinds,
            capture,
            release,
            align,
            relation);
        Cleanup.Add(profile);
        return profile;
    }

    private static RestaurantAreaMember Member(
        string name,
        RestaurantPlacementSnapProfile profile)
    {
        GameObject go = NewGo(name);
        RestaurantAreaMember member = go.AddComponent<RestaurantAreaMember>();
        RestaurantPlacementSnapProfileBinding binding =
            go.AddComponent<RestaurantPlacementSnapProfileBinding>();
        binding.SetProfile(profile);
        InvokeLifecycle(member, "Awake");
        return member;
    }

    private static void SetProfile(
        RestaurantAreaMember member,
        RestaurantPlacementSnapProfile profile)
    {
        RestaurantPlacementSnapProfileBinding binding =
            member.GetComponent<RestaurantPlacementSnapProfileBinding>();
        binding.SetProfile(profile);
    }

    private static RestaurantPlacementSnapTarget Target(
        string name,
        Vector3 position,
        RestaurantPlacementSnapTargetKind kind,
        Vector2 size,
        Vector3 normal,
        Vector3 forward,
        string relation = "",
        bool blocked = false)
    {
        GameObject go = NewGo(name);
        go.transform.position = position;
        RestaurantPlacementSnapTarget target =
            go.AddComponent<RestaurantPlacementSnapTarget>();
        target.EditorConfigure(
            kind,
            size,
            Vector3.zero,
            normal,
            forward,
            relation,
            blocked);
        return target;
    }

    private static BistroBuilderWallRecord Wall(
        string id,
        float ax,
        float ay,
        float bx,
        float by)
    {
        return new BistroBuilderWallRecord
        {
            wallId = new BistroBuilderEditId(id),
            buildPlaneId = "default",
            axisStart = new Vector2(ax, ay),
            axisEnd = new Vector2(bx, by),
            baseElevation = 0f,
            height = 2.5f,
            thickness = 0.12f,
            wallDefinitionId = "wall.default"
        };
    }

    private static GameObject NewGo(string name)
    {
        GameObject go = new GameObject(name);
        Cleanup.Add(go);
        return go;
    }

    private static void Example(
        string name,
        RestaurantPlacementSnapResult result)
    {
        Lines.Add(
            "EXAMPLE - " + name +
            " snapped=" + result.IsSnapped +
            " target=" +
            (result.RelatedObject != null
                ? result.RelatedObject.name
                : "none") +
            " distance=" + result.Distance.ToString("0.000") +
            " hint=" + result.HintState);
    }

    private static bool Nearly(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude < 0.000001f;
    }

    private static bool Finite(Vector3 value)
    {
        return Finite(value.x) && Finite(value.y) && Finite(value.z);
    }

    private static bool Finite(Quaternion value)
    {
        return Finite(value.x) &&
               Finite(value.y) &&
               Finite(value.z) &&
               Finite(value.w);
    }

    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void InvokeBootstrap(Type type)
    {
        MethodInfo method = type.GetMethod(
            "Install",
            BindingFlags.Static |
            BindingFlags.NonPublic |
            BindingFlags.Public);
        if (method == null)
            throw new MissingMethodException(type.FullName, "Install");
        method.Invoke(null, null);
    }

    private static void InvokeStaticPrivate(
        Type type,
        string methodName)
    {
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(type.FullName, methodName);
        method.Invoke(null, null);
    }

    private static void InvokeLifecycle(
        object target,
        string methodName)
    {
        if (target == null)
            return;
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public);
        method?.Invoke(target, null);
    }

    private static T Find<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(
            FindObjectsInactive.Include);
    }

    private static void Check(bool condition, string label)
    {
        if (condition)
        {
            pass++;
            Lines.Add("OK - " + label);
        }
        else
        {
            fail++;
            Lines.Add("FAIL - " + label);
        }
    }

    private static bool Finish()
    {
        Lines.Add(
            "Resultado: " + pass +
            " OK / " + fail +
            " fallos.");

        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "EditorV2_B7_ContextualSnapping_Report.txt");
        File.WriteAllLines(
            path,
            Lines,
            Encoding.UTF8);

        Debug.Log(string.Join(Environment.NewLine, Lines));
        return fail == 0;
    }
}
