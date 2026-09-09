using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guardia reutilizable para detectar bloqueos externos antes de modificar
/// escenas. No libera handles ni cierra procesos: solo impide empezar una
/// instalación cuando Windows no permite acceso exclusivo al archivo.
/// </summary>
public static class BistroBuilderSceneLockGuard
{
    public const string MainScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    public static bool TryEnsureWritable(
        string assetPath,
        out string error)
    {
        return TryEnsureWritable(
            assetPath,
            3,
            120,
            out error);
    }

    public static bool TryEnsureWritable(
        string assetPath,
        int attempts,
        int delayMilliseconds,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            error = "La ruta de escena para Scene Lock Guard está vacía.";
            return false;
        }

        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            error = "Scene Lock Guard no encuentra: " + assetPath;
            return false;
        }

        attempts = Mathf.Max(1, attempts);
        delayMilliseconds = Mathf.Max(0, delayMilliseconds);

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using (FileStream stream = new FileStream(
                           fullPath,
                           FileMode.Open,
                           FileAccess.ReadWrite,
                           FileShare.None))
                {
                }

                error = string.Empty;
                return true;
            }
            catch (IOException exception)
            {
                error = "Escena bloqueada por otro proceso: " +
                    assetPath + ". " + exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = "Sin acceso de escritura a la escena: " +
                    assetPath + ". " + exception.Message;
                return false;
            }

            if (attempt < attempts && delayMilliseconds > 0)
            {
                Thread.Sleep(delayMilliseconds);
            }
        }

        return false;
    }

    [MenuItem(
        "Tools/Bistro Builder/Infrastructure/Check Main Scene Lock",
        false,
        90)]
    private static void CheckMainSceneLock()
    {
        if (TryEnsureWritable(MainScenePath, out string error))
        {
            Debug.Log(
                "BB Scene Lock Guard: PASS — escena principal escribible.");
            return;
        }

        Debug.LogError("BB Scene Lock Guard: FAIL — " + error);
    }
}
