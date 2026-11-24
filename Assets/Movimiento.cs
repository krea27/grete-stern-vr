using UnityEngine;
using UnityEngine.InputSystem;

public class Movimiento : MonoBehaviour
{
    public float velocidad = 2.2f;
    public float sensibilidadMouse = 0.1f;
#if UNITY_EDITOR || !UNITY_ANDROID
    private float rotacionX = 0f;
    private float rotacionY = 0f;
#endif
    private CharacterController controlador;

    void Start()
    {
        controlador = GetComponent<CharacterController>();
#if UNITY_EDITOR || !UNITY_ANDROID
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
#endif
    }

    public void ResetearRotacion(Quaternion rotacionDeseada)
    {
        transform.localRotation = rotacionDeseada;
#if UNITY_EDITOR || !UNITY_ANDROID
        Vector3 euler = rotacionDeseada.eulerAngles;
        rotacionX = (euler.x > 180f) ? euler.x - 360f : euler.x;
        rotacionY = euler.y;
#endif
    }

    void Update()
    {
#if UNITY_EDITOR || !UNITY_ANDROID
        // 1. Rotación con el botón derecho del ratón (siempre disponible para contemplación fluida)
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue() * sensibilidadMouse;
            rotacionX -= delta.y;
            rotacionX = Mathf.Clamp(rotacionX, -90f, 90f);
            rotacionY += delta.x;
            transform.localRotation = Quaternion.Euler(rotacionX, rotacionY, 0f);
        }
#endif

        if (!EscapeOjo.PuedeMoverse)
        {
            return;
        }

        // Mantener altura erguida y estable a nivel de los ojos (Y = 0)
        if (Mathf.Abs(transform.position.y) > 0.05f)
        {
            if (controlador != null) controlador.enabled = false;
            transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
            if (controlador != null) controlador.enabled = true;
        }

        // 2. Control Gestual de Locomoción en Eje Z
        // Mano en reposo / neutra: gestoZ = 0 (Totalmente quieto en el lugar, reposo natural)
        // Pulgar para arriba (👍): gestoZ = +1 (Avanzar deliberadamente hacia adelante)
        // Puño cerrado (✊): gestoZ = -1 (Retroceder hacia atrás)
        float gestoZ = 0f;
        if (OSCHandReceiver.EsPulgarArriba || VRHandVisual.EsPulgarArriba)
        {
            gestoZ += 1.0f;
        }
        else if (OSCHandReceiver.EsPunoCerrado || VRHandVisual.EsPunoCerrado)
        {
            gestoZ -= 1.0f;
        }

#if UNITY_EDITOR || !UNITY_ANDROID
        // 3. Movimiento WASD + Gestos (Plano horizontal: altura fija en ojos, jamás cae ni se hunde)
        float moverX = 0f;
        float moverZ = gestoZ;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moverX -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moverX += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moverZ += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moverZ -= 1f;
        }

        moverZ = Mathf.Clamp(moverZ, -1f, 1f);

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        else fwd.Normalize();

        Vector3 rgt = transform.right;
        rgt.y = 0f;
        if (rgt.sqrMagnitude < 0.001f) rgt = Vector3.right;
        else rgt.Normalize();

        Vector3 movimiento = rgt * moverX + fwd * moverZ;
        if (controlador != null && movimiento.sqrMagnitude > 0.001f)
        {
            controlador.Move(movimiento.normalized * (velocidad * Time.deltaTime));
        }
#else
        // En Android (Cardboard VR):
        // Avanzar con botón del visor o pantalla O con Pulgar Arriba / Palma Abierta; retroceder con Puño Cerrado
        bool touchAvanzar = DetectarAvanceAndroid();

        float totalZAndroid = gestoZ;
        if (touchAvanzar) totalZAndroid += 1.0f;
        totalZAndroid = Mathf.Clamp(totalZAndroid, -1f, 1f);

        if (controlador != null && Mathf.Abs(totalZAndroid) > 0.01f)
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            else fwd.Normalize();

            controlador.Move(fwd * (totalZAndroid * velocidad * Time.deltaTime));
        }
#endif
    }

    private bool DetectarAvanceAndroid()
    {
        try
        {
            // 1. Google Cardboard XR Trigger (botón físico/conductivo del visor Cardboard)
            if (Google.XR.Cardboard.Api.IsTriggerPressed || Google.XR.Cardboard.Api.IsTriggerHeldPressed)
            {
                return true;
            }

            // 2. TouchScreen con Unity New Input System (cualquier toque activo en la pantalla)
            var ts = Touchscreen.current;
            if (ts != null)
            {
                if (!ts.enabled) InputSystem.EnableDevice(ts);
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].press.isPressed) return true;
                }
            }

            // 3. Fallback Legacy Touch
            if (Input.touchCount > 0)
            {
                return true;
            }
        }
        catch
        {
            // Silencioso para evitar cualquier excepción o banner rojo en VR
        }
        return false;
    }
}
