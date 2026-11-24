using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class KeepEditorAwake
{
    static KeepEditorAwake()
    {
        Application.runInBackground = true;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        // Forzar actualización continua aunque la ventana de Unity no esté en primer plano
    }
}

