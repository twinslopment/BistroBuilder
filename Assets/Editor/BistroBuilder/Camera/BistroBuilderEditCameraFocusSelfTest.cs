using System;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderEditCameraFocusSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Camera/Test Edit Focus Policy",
        false,
        36924)]
    public static void Run()
    {
        int checks = 0;

        void Check(
            bool condition,
            string message)
        {
            checks++;

            if (!condition)
                throw new InvalidOperationException(
                    message);
        }

        Vector2 safeMin =
            new Vector2(
                0.28f,
                0.18f);

        Vector2 safeMax =
            new Vector2(
                0.72f,
                0.80f);

        Check(
            BistroBuilderEditCameraFocusCoordinator
                .IsViewportFramingComfortable(
                    new Vector2(0.45f, 0.42f),
                    new Vector2(0.56f, 0.56f),
                    safeMin,
                    safeMax,
                    0.085f),
            "Una selección centrada y legible debe conservar la cámara.");

        Check(
            !BistroBuilderEditCameraFocusCoordinator
                .IsViewportFramingComfortable(
                    new Vector2(0.49f, 0.49f),
                    new Vector2(0.53f, 0.53f),
                    safeMin,
                    safeMax,
                    0.085f),
            "Una selección demasiado pequeña debe pedir reencuadre.");

        Check(
            !BistroBuilderEditCameraFocusCoordinator
                .IsViewportFramingComfortable(
                    new Vector2(0.04f, 0.42f),
                    new Vector2(0.16f, 0.58f),
                    safeMin,
                    safeMax,
                    0.085f),
            "Una selección tapada por el catálogo lateral debe pedir reencuadre.");

        Check(
            !BistroBuilderEditCameraFocusCoordinator
                .IsViewportFramingComfortable(
                    new Vector2(0.83f, 0.42f),
                    new Vector2(0.95f, 0.58f),
                    safeMin,
                    safeMax,
                    0.085f),
            "Una selección tapada por el contextual debe pedir reencuadre.");

        Check(
            !BistroBuilderEditCameraFocusCoordinator
                .IsViewportFramingComfortable(
                    new Vector2(float.NaN, 0.2f),
                    new Vector2(0.5f, 0.5f),
                    safeMin,
                    safeMax,
                    0.085f),
            "La política debe rechazar proyecciones no finitas.");

        GameObject cameraRoot =
            new GameObject(
                "BB_EditFocusPolicy_TestCamera");

        try
        {
            Camera camera =
                cameraRoot.AddComponent<Camera>();

            camera.transform.position =
                new Vector3(
                    0f,
                    7f,
                    -7f);

            camera.transform.rotation =
                Quaternion.Euler(
                    45f,
                    0f,
                    0f);

            camera.fieldOfView =
                45f;

            Vector3[] points =
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 1f, 0.5f),
                new Vector3(-0.5f, 1f, 0.5f)
            };

            Check(
                BistroBuilderEditCameraFocusCoordinator
                    .TryProjectViewportBounds(
                        camera,
                        points,
                        out Vector2 projectedMin,
                        out Vector2 projectedMax),
                "La proyección de bounds visibles debe resolverse.");

            Check(
                projectedMax.x >
                projectedMin.x &&
                projectedMax.y >
                projectedMin.y,
                "La proyección debe conservar una extensión positiva.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(
                cameraRoot);
        }

        Debug.Log(
            "=== BISTRO BUILDER - EDIT CAMERA FOCUS ===\n" +
            "[PASS] " +
            checks +
            " checks. La cámara solo reencuadra selección pequeña o fuera de la zona útil; " +
            "no persigue el objeto durante el arrastre.");
    }
}
