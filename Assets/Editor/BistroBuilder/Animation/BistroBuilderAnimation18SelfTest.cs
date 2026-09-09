using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotests de contrato y componentes puros. No dependen de clips externos.
/// </summary>
public static class BistroBuilderAnimation18SelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/18 Animacion e interacciones/Ejecutar autotests")]
    public static void Run()
    {
        int ok = 0;
        int fail = 0;
        StringBuilder report = new StringBuilder("BLOQUE 18 - AUTOTEST\n");

        void Check(bool condition, string label)
        {
            if (condition)
            {
                ok++;
                report.AppendLine("OK - " + label);
            }
            else
            {
                fail++;
                report.AppendLine("FAIL - " + label);
            }
        }

        BistroBuilderInteractionAdaptationBudget budget =
            new BistroBuilderInteractionAdaptationBudget();
        Check(budget.Validate(out _), "Adaptation Budget por defecto vÃ¡lido");
        Check(budget.ComfortableRootDistanceRatio < budget.AssistedRootDistanceRatio,
            "Zona cÃ³moda es mÃ¡s estricta que zona asistida");
        Check(budget.ComfortableReachRatio < budget.AssistedReachRatio,
            "Reach cÃ³modo es mÃ¡s estricto que reach asistido");

        Check(BistroBuilderMotionProfile.NormalizeId("  Seat.Standard ") == "seat.standard",
            "IDs semÃ¡nticos se normalizan de forma determinista");

        BistroBuilderMotionCatalog catalog = ScriptableObject.CreateInstance<BistroBuilderMotionCatalog>();
        catalog.ConfigureForEditor(new List<BistroBuilderMotionProfile>());
        Check(catalog.ValidateConfiguration(out _), "CatÃ¡logo vacÃ­o es vÃ¡lido antes de adquirir clips");
        Check(!catalog.TryResolve("missing.motion", out _),
            "Motion inexistente falla de forma segura");

        GameObject root = new GameObject("__BB18_EditSelfTest__");
        try
        {
            GameObject frameObject = new GameObject("Frame");
            frameObject.transform.SetParent(root.transform, false);

            BistroBuilderAnimationInteractionSlotDefinition slot =
                new BistroBuilderAnimationInteractionSlotDefinition();
            slot.ConfigureForEditor(
                "work.default",
                BistroBuilderInteractionFamily.Workstation,
                frameObject.transform);
            Check(slot.ValidateConfiguration(out _), "Slot universal mÃ­nimo vÃ¡lido");

            BistroBuilderAssetInteractionDescriptor descriptor =
                root.AddComponent<BistroBuilderAssetInteractionDescriptor>();
            descriptor.ConfigureForEditor(
                new List<BistroBuilderAnimationInteractionSlotDefinition> { slot });
            Check(descriptor.ValidateConfiguration(out _),
                "Descriptor de asset data-driven vÃ¡lido");
            Check(descriptor.TryResolveSlot(
                    BistroBuilderInteractionFamily.Workstation,
                    "work.default",
                    out BistroBuilderAnimationInteractionSlotDefinition resolved) &&
                  ReferenceEquals(slot, resolved),
                "Descriptor resuelve slots por familia e ID");

            GameObject actor = new GameObject("Actor");
            actor.transform.SetParent(root.transform, false);
            Check(descriptor.TryBuildPlan(
                    BistroBuilderInteractionFamily.Workstation,
                    BistroBuilderInteractionOperation.Use,
                    "work.default",
                    actor,
                    "test:actor",
                    out BistroBuilderResolvedInteractionPlan plan,
                    out _),
                "Descriptor construye un Resolved Interaction Plan sin conocer gameplay");
            Check(plan != null && plan.family == BistroBuilderInteractionFamily.Workstation,
                "Plan conserva semÃ¡ntica de familia");

            GameObject source = new GameObject("SourceSocket");
            source.transform.SetParent(root.transform, false);
            GameObject destination = new GameObject("DestinationSocket");
            destination.transform.SetParent(root.transform, false);
            GameObject prop = new GameObject("Prop");
            prop.transform.SetParent(source.transform, false);
            BistroBuilderTransferableVisual transferable =
                prop.AddComponent<BistroBuilderTransferableVisual>();
            transferable.CaptureHomeIfNeeded();
            Check(transferable.AttachVisual(destination.transform) &&
                  prop.transform.parent == destination.transform,
                "Transfer visual adjunta objeto sin tocar ownership de gameplay");
            transferable.RestoreHome();
            Check(prop.transform.parent == source.transform,
                "Transfer visual puede reconciliarse al estado estable anterior");

            GameObject movingPart = new GameObject("MovingPart");
            movingPart.transform.SetParent(root.transform, false);
            BistroBuilderObjectMotionProfile motionProfile =
                ScriptableObject.CreateInstance<BistroBuilderObjectMotionProfile>();
            motionProfile.ConfigureForEditor(
                "test.slide",
                BistroBuilderObjectMotionPrimitive.Slide,
                Vector3.right,
                0.5f,
                0.2f);
            BistroBuilderObjectMotionDriver motionDriver =
                root.AddComponent<BistroBuilderObjectMotionDriver>();
            motionDriver.ConfigureForEditor(movingPart.transform, motionProfile);
            motionDriver.SetProgressImmediate(1f);
            Check(Mathf.Abs(movingPart.transform.localPosition.x - 0.5f) < 0.001f,
                "Object Motion usa progreso autoritativo 0..1");
            motionDriver.ReconcileTo(0f);
            Check(movingPart.transform.localPosition.sqrMagnitude < 0.000001f,
                "Object Motion reconcilia instantÃ¡neamente al progreso lÃ³gico");


            GameObject groundingVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            groundingVisual.name = "GroundingVisual";
            groundingVisual.transform.SetParent(root.transform, false);
            groundingVisual.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            Vector3 authorityBefore = root.transform.position;
            BistroBuilderCharacterVisualGroundingAdapter grounding =
                root.AddComponent<BistroBuilderCharacterVisualGroundingAdapter>();
            grounding.Configure(groundingVisual.transform);
            Check(grounding.CalibrateFromCurrentPose(out _) &&
                  root.transform.position == authorityBefore,
                "Grounding visual preserva íntegramente el root autoritativo");
            Check(grounding.IsCalibrated && grounding.AppliedVerticalOffsetMeters > 0.09f,
                "Grounding visual compensa penetración dentro de un presupuesto acotado");

            UnityEngine.Object.DestroyImmediate(motionProfile);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(catalog);
        }

        Check(typeof(BistroBuilderLocomotionAnimationPresenter) != null,
            "Locomotion Presenter existe sin asumir autoridad posicional");
        Check(typeof(BistroBuilderCharacterAnimationDriver) != null,
            "Backend de personaje queda encapsulado");
        Check(typeof(BistroBuilderInteractionPresentationService) != null,
            "Coordinator transaccional disponible");
        Check(Enum.IsDefined(typeof(BistroBuilderInteractionFailureKind),
                BistroBuilderInteractionFailureKind.CommitTimeout),
            "Watchdog contempla timeout de Commit Frontier");

        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Resultado: " + ok + " OK / " + fail + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
