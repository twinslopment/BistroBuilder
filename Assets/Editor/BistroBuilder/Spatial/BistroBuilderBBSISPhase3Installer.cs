using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Instala la base previa y valida el hardening BBSIS Fase 3.
/// </summary>
public static class BistroBuilderBBSISPhase3Installer
{
    [MenuItem("Bistro Builder/BBSIS/Fase 3/Instalar y validar")]
    public static void InstallAndValidate()
    {
        BistroBuilderBBSISPhase2DInstaller.Install();
        BistroBuilderBBSISPhase3Validator.Run();
        BistroBuilderBBSISPhase3SelfTest.Run();
    }

    public static void RunFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BBSIS FASE 3 - INSTALACION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
