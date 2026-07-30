#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.CameraSystem.Editor
{
    internal sealed class BistroBuilderCamera369AReport
    {
        private readonly string title;
        private readonly List<string> lines = new List<string>();

        public int Passed { get; private set; }
        public int Warnings { get; private set; }
        public int Errors { get; private set; }

        public BistroBuilderCamera369AReport(string title)
        {
            this.title = title;
        }

        public void Pass(string message)
        {
            Passed++;
            lines.Add("- OK: " + message);
        }

        public void Warn(string message)
        {
            Warnings++;
            lines.Add("- ADVERTENCIA: " + message);
        }

        public void Fail(string message)
        {
            Errors++;
            lines.Add("- ERROR: " + message);
        }

        public string BuildText()
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine(title);
            builder.AppendLine("Correctos: " + Passed);
            builder.AppendLine("Advertencias: " + Warnings);
            builder.AppendLine("Errores: " + Errors);
            for (int index = 0; index < lines.Count; index++)
            {
                builder.AppendLine(lines[index]);
            }

            return builder.ToString().TrimEnd();
        }

        public void Log(Object context = null)
        {
            string message = BuildText();
            if (Errors > 0)
            {
                Debug.LogError(message, context);
            }
            else if (Warnings > 0)
            {
                Debug.LogWarning(message, context);
            }
            else
            {
                Debug.Log(message, context);
            }
        }

        public void ShowDialog()
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                BuildText(),
                "Aceptar");
        }
    }
}
#endif
