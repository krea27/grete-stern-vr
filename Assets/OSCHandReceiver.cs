using System;
using UnityEngine;
using extOSC;

public class OSCHandReceiver : MonoBehaviour
{
    [Header("Configuración OSC")]
    public OSCReceiver receiver;

    [Header("Referencias en la Escena")]
    [Tooltip("El objeto que se moverá como si fuera tu mano (ej. Sphere)")]
    public Transform handCursor; 
    
    [Tooltip("El material de un objeto para cambiar de color al hacer el gesto de pinza")]
    public Renderer pinchIndicator; 

    [Header("Control Fiel 1:1 y Natural")]
    [Tooltip("Sensibilidad horizontal fiel a los movimientos reales de tu mano")]
    [System.NonSerialized] public float scaleX = 1.45f;
    [Tooltip("Sensibilidad vertical fiel a los movimientos reales de tu mano")]
    [System.NonSerialized] public float scaleY = 1.25f;
    [Tooltip("Sensibilidad en profundidad Z fiel a tu brazo físico")]
    [System.NonSerialized] public float scaleZ = 1.65f;
    [Tooltip("Posición base frente a los ojos del jugador (a distancia óptima y cómoda)")]
    [System.NonSerialized] public Vector3 offset = new Vector3(0.02f, -0.10f, 1.85f);
    private Transform jugador;
    private VRHandVisual handVisual;

    [Header("Respuesta Fluida en Espacio Local (Sin Saltos Bruscos)")]
    public bool suavizar = true;
    public float velocidadSuavizado = 28f;
    private Vector3 offsetLocalObjetivo = new Vector3(0.02f, -0.10f, 1.85f);
    private Vector3 offsetLocalSuavizado = new Vector3(0.02f, -0.10f, 1.85f);
    private Vector3 velocidadLocalSmooth = Vector3.zero;

    // Estado global de pinza, símbolo de paz y gestos de desplazamiento (Z)
    public static bool IsPinching { get; private set; } = false;
    public static event Action OnPinchStarted;
    public static event Action OnPinchEnded;

    public static bool IsPeaceSign { get; private set; } = false;
    public static event Action OnPeaceSignStarted;
    public static event Action OnPeaceSignEnded;

    public static bool EsPulgarArriba { get; private set; } = false;
    public static bool EsManoAbierta => EsPulgarArriba;
    public static bool EsPunoCerrado { get; private set; } = false;

    private float tiempoUltimoMensajeOSC = -10f;

    void Start()
    {
        Application.runInBackground = true;

        if (receiver == null) 
        {
            receiver = GetComponent<OSCReceiver>();
            if (receiver == null) receiver = FindAnyObjectByType<OSCReceiver>();
        }

        if (receiver != null)
        {
            // Forzar escucha en 0.0.0.0 (cualquier IP, tanto 127.0.0.1 de PC como Wi-Fi de Android)
            receiver.LocalHostMode = OSCLocalHostMode.Any;
            receiver.LocalPort = 5005;
            if (!receiver.IsStarted) receiver.Connect();

            receiver.Bind("/hand/wrist", OnReceiveWrist);
            receiver.Bind("/hand/pinch", OnReceivePinch);
            receiver.Bind("/hand/fingers", OnReceiveFingers);
            receiver.Bind("/hand/peace", OnReceivePeace);
            receiver.Bind("/hand/thumb", OnReceiveThumb);
            receiver.Bind("/hand/open", OnReceiveThumb);
            receiver.Bind("/hand/fist", OnReceiveFist);
            Debug.Log("[OSCHandReceiver] Vinculado a /hand/wrist, /hand/pinch, /hand/fingers, /hand/peace, /hand/thumb y /hand/fist en puerto 5005.");
        }
        else
        {
            Debug.LogWarning("[OSCHandReceiver] No se encontró OSCReceiver en la escena.");
        }

        if (handCursor != null)
        {
            // Collider anatómico de la mano (radio ajustado a 20cm, no gigante)
            var col = handCursor.GetComponent<SphereCollider>();
            if (col == null)
            {
                col = handCursor.gameObject.AddComponent<SphereCollider>();
            }
            col.radius = 0.20f;
            col.isTrigger = true; // Fundamental: trigger para que nunca actúe como obstáculo físico bloqueando el movimiento del jugador

            var rb = handCursor.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = handCursor.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Reemplazar la esfera simple por la mano 3D articulada (Free Pack - VR Hands)
            handVisual = handCursor.GetComponent<VRHandVisual>();
            if (handVisual == null)
            {
                handVisual = handCursor.gameObject.AddComponent<VRHandVisual>();
            }
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        // Atajos en Editor para pruebas completas inmediatas en PC (con o sin webcam)
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.pKey.isPressed || kb.vKey.isPressed || kb.spaceKey.isPressed)
            {
                IsPeaceSign = true;
            }
            else if (Time.time - tiempoUltimoMensajeOSC > 0.8f)
            {
                IsPeaceSign = false;
            }

            if (kb.wKey.isPressed)
            {
                EsPulgarArriba = true;
                EsPunoCerrado = false;
            }
            else if (kb.sKey.isPressed)
            {
                EsPunoCerrado = true;
                EsPulgarArriba = false;
            }
            else if (Time.time - tiempoUltimoMensajeOSC > 0.8f)
            {
                EsPulgarArriba = false;
                EsPunoCerrado = false;
            }
        }
#endif

        ActualizarPosicionMano();
    }

    void LateUpdate()
    {
        // Ejecutar también en LateUpdate después de CardboardStartup para que al girar la cabeza la mano no tenga ni 1 frame de retraso
        ActualizarPosicionMano();
    }

    private void ActualizarPosicionMano()
    {
        if (jugador == null && Camera.main != null)
        {
            jugador = Camera.main.transform;
        }

        if (handCursor != null)
        {
            // Suavizado cinemático (0.038s): absorbe microtemblores y elimina todo movimiento brusco
            if (suavizar)
            {
                offsetLocalSuavizado = Vector3.SmoothDamp(offsetLocalSuavizado, offsetLocalObjetivo, ref velocidadLocalSmooth, 0.038f);
            }
            else
            {
                offsetLocalSuavizado = offsetLocalObjetivo;
            }

            // Seguir la rotación de la cámara manteniendo el horizonte nivelado para la mano
            Quaternion lookRot = (jugador != null)
                ? Quaternion.Euler(jugador.eulerAngles.x, jugador.eulerAngles.y, 0f)
                : Quaternion.identity;
            Vector3 origin = (jugador != null) ? jugador.position : Vector3.zero;

            Vector3 rawTarget = origin + lookRot * offsetLocalSuavizado;
            Vector3 toTarget = rawTarget - origin;
            float targetDist = toTarget.magnitude;
            Vector3 finalPos = rawTarget;

            if (targetDist > 0.05f)
            {
                Vector3 dir = toTarget / targetDist;
                float radioMano = 0.20f;

                // SphereCast para detectar paredes, suelo, techo y superficies sólidas
                // La superficie actúa como TOPE FÍSICO impidiendo que la mano la atraviese
                RaycastHit[] hits = Physics.SphereCastAll(origin, radioMano, dir, targetDist);
                float menorDist = targetDist;

                foreach (var hit in hits)
                {
                    if (hit.collider == null || hit.collider.isTrigger) continue;
                    // IMPORTANTE: Ignorar al jugador y su CharacterController para que no empuje la mano contra la cara
                    if (jugador != null && (hit.collider.transform == jugador || hit.collider.transform.IsChildOf(jugador))) continue;
                    if (hit.collider.transform.IsChildOf(handCursor) || hit.collider.gameObject == handCursor.gameObject) continue;

                    if (hit.distance > 0.01f && hit.distance < menorDist)
                    {
                        menorDist = hit.distance;
                    }
                }

                // Raycast directo adicional para detectar colisiones frontales precisas
                if (Physics.Raycast(origin, dir, out RaycastHit rHit, targetDist))
                {
                    if (rHit.collider != null && !rHit.collider.isTrigger &&
                        (jugador == null || (!rHit.collider.transform.IsChildOf(jugador) && rHit.collider.transform != jugador)) &&
                        !rHit.collider.transform.IsChildOf(handCursor) && rHit.collider.gameObject != handCursor.gameObject)
                    {
                        float d = Mathf.Max(0.1f, rHit.distance - radioMano);
                        if (d < menorDist) menorDist = d;
                    }
                }

                // DISTANCIA MÍNIMA DE SEGURIDAD (1.15m):
                // Impide que la mano se acerque demasiado a los ojos, evitando que se recorte o se rompa
                menorDist = Mathf.Max(menorDist, 1.15f);

                finalPos = origin + dir * menorDist;
            }

            // Límite perimetral interior de la habitación cúbica (tope absoluto en paredes)
            var n3 = FindAnyObjectByType<Nivel3_PuertaLlave>();
            bool esNivel3 = (n3 != null && n3.Nivel3Iniciado) || (GameObject.Find("PuertaAisladaGrete") != null);

            if (esNivel3)
            {
                // En Nivel 3: entorno exterior abierto con sendero hacia la puerta y la gigantografía.
                // No hay paredes cúbicas que traben el avance natural de la mano ni del jugador.
            }
            else if (origin.z < -20f)
            {
                // En Nivel 2: limitar en el espacio local de Nivel2_Montana para respetar su rotación hacia el jugador
                var nivel2 = GameObject.Find("Nivel2_Montana");
                if (nivel2 != null)
                {
                    Vector3 localPos = nivel2.transform.InverseTransformPoint(finalPos);
                    localPos.x = Mathf.Clamp(localPos.x, -3.75f, 3.75f);
                    localPos.y = Mathf.Clamp(localPos.y, -3.75f, 3.75f);
                    localPos.z = Mathf.Clamp(localPos.z, -3.75f, 3.75f);
                    finalPos = nivel2.transform.TransformPoint(localPos);
                }
                else
                {
                    finalPos.x = Mathf.Clamp(finalPos.x, -3.75f, 3.75f);
                    finalPos.y = Mathf.Clamp(finalPos.y, -3.75f, 3.75f);
                    finalPos.z = Mathf.Clamp(finalPos.z, -43.75f, -36.25f);
                }
            }
            else
            {
                // En Nivel 1: habitación cúbica centrada en (0, 0, 0)
                finalPos.x = Mathf.Clamp(finalPos.x, -3.75f, 3.75f);
                finalPos.y = Mathf.Clamp(finalPos.y, -3.75f, 3.75f);
                finalPos.z = Mathf.Clamp(finalPos.z, -3.75f, 3.75f);
            }

            handCursor.position = finalPos;
            handCursor.rotation = lookRot;
        }
    }

    // Se dispara cuando Python MediaPipe envía [x, y, z] de la palma
    private void OnReceiveWrist(OSCMessage message)
    {
        tiempoUltimoMensajeOSC = Time.time;
        if (message == null || message.Values == null || message.Values.Count < 3) return;

        float x = message.Values[0].FloatValue;
        float y = message.Values[1].FloatValue;
        float z = message.Values[2].FloatValue;

        // Modulación gestual de Z: al abrir la mano se proyecta ligeramente hacia adelante; al cerrar el puño retrocede
        float zGestoOffset = (EsManoAbierta || VRHandVisual.EsManoAbierta) ? 0.35f : ((EsPunoCerrado || VRHandVisual.EsPunoCerrado) ? -0.30f : 0f);

        // Profundidad Z: distancia óptima y cómoda (1.20m a 3.65m), nunca pegada a la cara
        float localZ = Mathf.Clamp(offset.z + (-z * scaleZ) + zGestoOffset, 1.15f, 3.65f);

        // Escalar X e Y de forma fiel y centrada a los movimientos naturales del brazo
        float perspectiveFactor = localZ / 1.85f;
        float baseX = Mathf.Clamp((x - 0.5f) * scaleX + offset.x, -0.60f, 0.60f);
        float baseY = Mathf.Clamp(-(y - 0.5f) * scaleY + offset.y, -0.42f, 0.42f);

        offsetLocalObjetivo = new Vector3(baseX * perspectiveFactor, baseY * perspectiveFactor, localZ);
    }

    // Se dispara cuando Python envía la flexión individual de los 5 dedos + nivel continuo de pinza
    private void OnReceiveFingers(OSCMessage message)
    {
        tiempoUltimoMensajeOSC = Time.time;
        if (handVisual == null || message == null || message.Values == null || message.Values.Count < 6) return;

        float thumb  = message.Values[0].FloatValue;
        float index  = message.Values[1].FloatValue;
        float middle = message.Values[2].FloatValue;
        float ring   = message.Values[3].FloatValue;
        float pinky  = message.Values[4].FloatValue;
        float pinch  = message.Values[5].FloatValue;

        handVisual.ActualizarDedos(thumb, index, middle, ring, pinky, pinch);
    }

    // Se dispara cuando Python detecta Pulgar para Arriba (para avanzar en eje Z)
    private void OnReceiveThumb(OSCMessage message)
    {
        tiempoUltimoMensajeOSC = Time.time;
        if (message == null || message.Values == null || message.Values.Count < 1) return;
        EsPulgarArriba = (message.Values[0].IntValue == 1);
        if (EsPulgarArriba) EsPunoCerrado = false;
    }

    // Se dispara cuando Python detecta Puño Cerrado (para retroceder en eje Z)
    private void OnReceiveFist(OSCMessage message)
    {
        tiempoUltimoMensajeOSC = Time.time;
        if (message == null || message.Values == null || message.Values.Count < 1) return;
        EsPunoCerrado = (message.Values[0].IntValue == 1);
        if (EsPunoCerrado) EsPulgarArriba = false;
    }

    // Se dispara cuando Python detecta el Símbolo de Paz / Victoria (índice y mayor extendidos)
    private void OnReceivePeace(OSCMessage message)
    {
        tiempoUltimoMensajeOSC = Time.time;
        if (message == null || message.Values == null || message.Values.Count < 1) return;

        int isPeaceInt = message.Values[0].IntValue;
        bool wasPeace = IsPeaceSign;
        IsPeaceSign = (isPeaceInt == 1);

        if (!wasPeace && IsPeaceSign)
        {
            OnPeaceSignStarted?.Invoke();
        }
        else if (wasPeace && !IsPeaceSign)
        {
            OnPeaceSignEnded?.Invoke();
        }
    }

    // Se dispara cuando Python envía 1 o 0 de "pinch"
    private void OnReceivePinch(OSCMessage message)
    {
        tiempoUltimoMensajeOSC = Time.time;
        if (message == null || message.Values == null || message.Values.Count < 1) return;

        int isPinchingInt = message.Values[0].IntValue;
        bool wasPinching = IsPinching;
        IsPinching = (isPinchingInt == 1);

        if (!wasPinching && IsPinching)
        {
            OnPinchStarted?.Invoke();
        }
        else if (wasPinching && !IsPinching)
        {
            OnPinchEnded?.Invoke();
        }
        
        if (pinchIndicator != null && pinchIndicator.enabled)
        {
            pinchIndicator.material.color = IsPinching ? Color.green : Color.red;
        }
    }
}
