using UnityEngine;
using extOSC; // Necesitas instalar el paquete gratuito "extOSC" desde la Unity Asset Store

public class OSCHandReceiver : MonoBehaviour
{
    [Header("Configuración OSC")]
    public OSCReceiver receiver;

    [Header("Referencias en la Escena")]
    [Tooltip("El objeto que se moverá como si fuera tu mano (ej. una esfera)")]
    public Transform handCursor; 
    
    [Tooltip("El material de un objeto para cambiar de color al hacer el gesto de pinza")]
    public Renderer pinchIndicator; 

    void Start()
    {
        Application.runInBackground = true;

        if (receiver == null) {
            Debug.LogError("No has asignado el OSC Receiver!");
            return;
        }

        // Conectar las direcciones OSC (que enviamos desde Python) con nuestras funciones en C#
        receiver.Bind("/hand/wrist", OnReceiveWrist);
        receiver.Bind("/hand/pinch", OnReceivePinch);
    }

    // Esta función se dispara automáticamente cada vez que Python envía las coordenadas
    private void OnReceiveWrist(OSCMessage message)
    {
        // Extraer X, Y, Z del mensaje
        float x = message.Values[0].FloatValue;
        float y = message.Values[1].FloatValue;
        float z = message.Values[2].FloatValue;

        // Convertir coordenadas de MediaPipe (0 a 1) a coordenadas del mundo de Unity
        // Multiplicamos por 10 para que el movimiento sea más amplio. Puedes ajustar este valor.
        float unityX = (x - 0.5f) * 10f; 
        // MediaPipe Y crece hacia abajo, Unity Y crece hacia arriba, por eso lo invertimos con un signo negativo
        float unityY = -(y - 0.5f) * 10f; 
        
        // Actualizar la posición de nuestro objeto "mano" en la escena
        if (handCursor != null)
        {
            // Usamos Vector3.Lerp en un caso real para suavizar el movimiento, pero aquí lo asignamos directo
            handCursor.position = new Vector3(unityX, unityY, 0);
        }
    }

    // Esta función se dispara cada vez que Python envía un 1 o un 0 de "pellizco"
    private void OnReceivePinch(OSCMessage message)
    {
        int isPinching = message.Values[0].IntValue;
        
        if (pinchIndicator != null)
        {
            if (isPinching == 1)
            {
                // Cambiar el color a Verde brillante al agarrar/pellizcar
                pinchIndicator.material.color = Color.green;
            }
            else
            {
                // Volver a color Rojo al soltar
                pinchIndicator.material.color = Color.red;
            }
        }
    }
}
