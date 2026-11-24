using System.Collections;
using UnityEngine;

public class EscapeOjo : MonoBehaviour
{
    [Tooltip("La cámara o el jugador a teletransportar.")]
    public Transform jugador; 

    [Tooltip("Material negro puro precompilado para la pantalla de desbloqueo.")]
    public Material materialPantallaNegra; 

    [Tooltip("Coordenadas a las que se teletransporta al escapar (Nivel 2: La Montaña).")]
    [System.NonSerialized] public Vector3 afuera = new Vector3(0f, 0f, -42.6f); 

    [System.NonSerialized] public Vector3 adentro = new Vector3(0f, 0f, -2.6f);
    [System.NonSerialized] public bool estaAfuera = false;
    public static bool ManoSobreElOjo { get; private set; } = false;
    public static bool PuedeMoverse { get; set; } = true;

    private bool manoEnContactoConOjo = false;
    private float tiempoUltimoTeletransporte = -10f;
    private const float COOLDOWN = 2.0f;

    // Conteo de contacto con símbolo de paz para activar el portal (5 segundos con cambio progresivo de color)
    public const float TIEMPO_REQUERIDO_CONTACTO = 5.0f;
    private float tiempoContactoAcumulado = 0f;
    private bool escapeIniciado = false;

    private BoxCollider boxOjo;
    private Transform esferaMano;
    private Vector3 escalaOriginalOjo;
    private Renderer[] renderersOjo;
    private Color[] coloresBaseOjo;
    private Light luzAuraOjo;
    private LineRenderer rayoConexionOjo;

    [Header("Efectos Sonoros y Resonancia")]
    public AudioClip clipOjoResonancia;
    public AudioClip clipOjoZoom;
    private AudioSource audioOjo3D;

    [Header("Música de Inicio (Fusión Sonora Nivel 1)")]
    public AudioClip clipMusicaInicio;
    private AudioSource audioMusicaInicio;

    void Start()
    {
        ManoSobreElOjo = false;
        tiempoContactoAcumulado = 0f;
        escapeIniciado = false;
        escalaOriginalOjo = transform.localScale;

        // Guardar renderers y colores originales del ojo para la transformación de color
        renderersOjo = GetComponentsInChildren<Renderer>(true);
        coloresBaseOjo = new Color[renderersOjo.Length];
        for (int i = 0; i < renderersOjo.Length; i++)
        {
            if (renderersOjo[i] != null && renderersOjo[i].material != null)
            {
                if (renderersOjo[i].material.HasProperty("_BaseColor"))
                    coloresBaseOjo[i] = renderersOjo[i].material.GetColor("_BaseColor");
                else
                    coloresBaseOjo[i] = renderersOjo[i].material.color;
            }
        }

        // Aura luminosa sobre el ojo para feedback visual del contacto
        GameObject goLuz = new GameObject("AuraOjoLuz");
        goLuz.transform.SetParent(transform, false);
        goLuz.transform.localPosition = Vector3.zero;
        luzAuraOjo = goLuz.AddComponent<Light>();
        luzAuraOjo.type = LightType.Point;
        luzAuraOjo.range = 3.5f;
        luzAuraOjo.intensity = 0f;

        // Haz luminoso de conexión mística entre la mano y el ojo
        GameObject goRayo = new GameObject("RayoConexionOjo");
        goRayo.transform.SetParent(transform, false);
        rayoConexionOjo = goRayo.AddComponent<LineRenderer>();
        rayoConexionOjo.startWidth = 0.025f;
        rayoConexionOjo.endWidth = 0.06f;
        rayoConexionOjo.positionCount = 2;
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (unlitShader != null) rayoConexionOjo.material = new Material(unlitShader);
        rayoConexionOjo.enabled = false;

        // Rigidbody cinemático
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        boxOjo = GetComponent<BoxCollider>();
        if (boxOjo != null)
        {
            boxOjo.isTrigger = true;
            boxOjo.size = new Vector3(1.8f, 2.0f, 2.2f);
        }

        var goEsfera = GameObject.Find("Sphere");
        if (goEsfera != null) esferaMano = goEsfera.transform;

        if (jugador == null)
        {
            var cam = Camera.main;
            if (cam != null) jugador = cam.transform;
        }

        if (jugador != null)
        {
            // Garantizar que en Nivel 1 el jugador empiece centrado y alejado (Z = -2.6) para una visión panorámica de las 3 paredes
            if (jugador.position.z > -1.0f && !estaAfuera)
            {
                CharacterController cc = jugador.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                jugador.position = new Vector3(0f, 0f, -2.6f);
                if (cc != null) cc.enabled = true;
            }
            else if (jugador.position.y < -1.0f)
            {
                CharacterController cc = jugador.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                jugador.position = new Vector3(jugador.position.x, 0f, jugador.position.z);
                if (cc != null) cc.enabled = true;
            }
        }

        // Cargar audio si no está asignado en Inspector
        if (clipOjoResonancia == null) clipOjoResonancia = Resources.Load<AudioClip>("Audio/ojo_resonancia_loop");
        if (clipOjoZoom == null) clipOjoZoom = Resources.Load<AudioClip>("Audio/ojo_zoom_portal");

        // Audio 3D espacial para audífonos VR centrado en la pupila del ojo
        audioOjo3D = gameObject.AddComponent<AudioSource>();
        audioOjo3D.spatialBlend = 1.0f;
        audioOjo3D.rolloffMode = AudioRolloffMode.Logarithmic;
        audioOjo3D.minDistance = 1.0f;
        audioOjo3D.maxDistance = 14.0f;
        audioOjo3D.playOnAwake = false;

        // Fusión Sonora: Canción de inicio en bucle infinito envolvente (2D estéreo para ambos oídos)
        if (clipMusicaInicio == null)
        {
            clipMusicaInicio = Resources.Load<AudioClip>("Audio/inicio-grete-music");
            if (clipMusicaInicio == null) clipMusicaInicio = Resources.Load<AudioClip>("Audio/inici");
        }

        if (clipMusicaInicio != null)
        {
            audioMusicaInicio = gameObject.AddComponent<AudioSource>();
            audioMusicaInicio.spatialBlend = 0.0f;
            audioMusicaInicio.clip = clipMusicaInicio;
            audioMusicaInicio.loop = true; // Cuando se termine reinicia automáticamente
            audioMusicaInicio.volume = 0.65f; // Nivel equilibrado para fusión sonora con los FX
            audioMusicaInicio.playOnAwake = false;
            audioMusicaInicio.Play();
            Debug.Log("[EscapeOjo] Música de inicio iniciada en bucle: " + clipMusicaInicio.name);
        }
    }

    void OnDestroy()
    {
    }

    private bool EsColliderMano(Collider col)
    {
        if (col == null) return false;
        string n = col.gameObject.name;
        return n == "Sphere" || n.Contains("Hand") || n.Contains("Bone") || n.Contains("VRHand");
    }

    void OnTriggerEnter(Collider otro)
    {
        if (EsColliderMano(otro))
        {
            manoEnContactoConOjo = true;
        }
    }

    void OnTriggerStay(Collider otro)
    {
        if (EsColliderMano(otro))
        {
            manoEnContactoConOjo = true;
        }
    }

    void OnTriggerExit(Collider otro)
    {
        if (EsColliderMano(otro))
        {
            manoEnContactoConOjo = false;
        }
    }

    /// <summary>
    /// Comprueba si la mano virtual está apuntando o tocando el ojo en la pared.
    /// Funciona por proximidad directa (distancia < 1.6m) o apuntando con la mano hacia la pared del ojo.
    /// </summary>
    private bool EstaManoSobreElOjo()
    {
        if (manoEnContactoConOjo) return true;

        if (esferaMano == null)
        {
            var go = GameObject.Find("Sphere");
            if (go != null) esferaMano = go.transform;
        }
        if (jugador == null && Camera.main != null)
        {
            jugador = Camera.main.transform;
        }

        if (esferaMano != null && boxOjo != null)
        {
            // 1. Contacto / proximidad directa
            float distSqr = boxOjo.bounds.SqrDistance(esferaMano.position);
            if (distSqr <= 1.6f * 1.6f)
            {
                return true;
            }

            // 2. Apuntar con la mano hacia el ojo en la pared
            if (jugador != null)
            {
                Vector3 toMano = esferaMano.position - jugador.position;
                if (toMano.sqrMagnitude > 0.2f)
                {
                    Ray rayoMano = new Ray(jugador.position, toMano.normalized);
                    Bounds b = boxOjo.bounds;
                    b.Expand(1.4f);
                    if (b.IntersectRay(rayoMano, out float hitDist) && hitDist <= 7.0f)
                    {
                        return true;
                    }
                }
            }

            // 3. Mirada directa hacia el ojo en la pared (muy natural en VR)
            if (jugador != null)
            {
                Ray rayoMirada = new Ray(jugador.position, jugador.forward);
                Bounds bOjo = boxOjo.bounds;
                bOjo.Expand(2.0f);
                if (bOjo.IntersectRay(rayoMirada, out float distMirada) && distMirada <= 8.5f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    void Update()
    {
        if (estaAfuera || escapeIniciado) return;

        // REQUISITO: Apuntar con la mano / mirar al ojo + Gesto SÍMBOLO DE PAZ (V)
        bool manoApuntando = EstaManoSobreElOjo();
        bool pazDetectada = OSCHandReceiver.IsPeaceSign || VRHandVisual.EsSimboloDePaz;

#if UNITY_EDITOR
        // En PC Editor: Atajo con teclado (P, V, Espacio) o Clic Izquierdo para probar de inmediato sin webcam
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (kb != null && (kb.pKey.isPressed || kb.vKey.isPressed || kb.spaceKey.isPressed))
        {
            manoApuntando = true;
            pazDetectada = true;
        }
        else if (mouse != null && mouse.leftButton.isPressed)
        {
            manoApuntando = true;
            pazDetectada = true;
        }
#endif

        bool contactoActivo = manoApuntando && pazDetectada;

        ManoSobreElOjo = contactoActivo;

        if (contactoActivo)
        {
            tiempoContactoAcumulado += Time.deltaTime;
            float progreso = Mathf.Clamp01(tiempoContactoAcumulado / TIEMPO_REQUERIDO_CONTACTO);

            // CAMBIO DE COLOR PROGRESIVO DURANTE LOS 5 SEGUNDOS:
            // 0.0s - 1.5s: Blanco/Natural -> Celeste Místico (Cyan)
            // 1.5s - 3.5s: Celeste -> Violeta / Púrpura Eléctrico
            // 3.5s - 5.0s: Violeta -> Dorado Radiante / Blanco Brillante
            Color colorFase;
            if (progreso < 0.35f)
            {
                float t = progreso / 0.35f;
                colorFase = Color.Lerp(Color.white, new Color(0.1f, 0.9f, 1.0f), t);
            }
            else if (progreso < 0.70f)
            {
                float t = (progreso - 0.35f) / 0.35f;
                colorFase = Color.Lerp(new Color(0.1f, 0.9f, 1.0f), new Color(0.85f, 0.2f, 1.0f), t);
            }
            else
            {
                float t = (progreso - 0.70f) / 0.30f;
                colorFase = Color.Lerp(new Color(0.85f, 0.2f, 1.0f), new Color(1.0f, 0.95f, 0.45f), t);
            }

            // Aplicar cambio de color a todos los renderers del ojo
            if (renderersOjo != null)
            {
                for (int i = 0; i < renderersOjo.Length; i++)
                {
                    if (renderersOjo[i] != null && renderersOjo[i].material != null)
                    {
                        if (renderersOjo[i].material.HasProperty("_BaseColor"))
                            renderersOjo[i].material.SetColor("_BaseColor", colorFase);
                        else
                            renderersOjo[i].material.color = colorFase;
                    }
                }
            }

            // Intensificar luz ambiental del portal
            if (luzAuraOjo != null)
            {
                luzAuraOjo.color = colorFase;
                luzAuraOjo.intensity = 1.0f + progreso * 3.5f;
            }

            // Haz de conexión visible entre mano y ojo
            if (rayoConexionOjo != null && esferaMano != null)
            {
                rayoConexionOjo.enabled = true;
                rayoConexionOjo.SetPosition(0, esferaMano.position);
                rayoConexionOjo.SetPosition(1, transform.position);
                rayoConexionOjo.startColor = colorFase;
                rayoConexionOjo.endColor = Color.white;
            }

            // Audio 3D de resonancia armónica ascendente mientras se mantiene el símbolo de paz
            if (audioOjo3D != null && clipOjoResonancia != null)
            {
                if (!audioOjo3D.isPlaying)
                {
                    audioOjo3D.clip = clipOjoResonancia;
                    audioOjo3D.loop = true;
                    audioOjo3D.Play();
                }
                audioOjo3D.pitch = Mathf.Lerp(0.85f, 1.55f, progreso);
                audioOjo3D.volume = Mathf.Lerp(0.35f, 0.95f, progreso);
            }

            // Vibración y pulsación acelerada
            float frecuenciaPulso = Mathf.Lerp(5f, 26f, progreso);
            float intensidadPulso = Mathf.Sin(Time.time * frecuenciaPulso) * (0.05f + progreso * 0.20f);
            float crecimiento = Mathf.Lerp(1.0f, 1.35f, progreso);
            transform.localScale = escalaOriginalOjo * (crecimiento + intensidadPulso);

            if (tiempoContactoAcumulado >= TIEMPO_REQUERIDO_CONTACTO)
            {
                if (rayoConexionOjo != null) rayoConexionOjo.enabled = false;
                EjecutarEscapeANivel2();
            }
        }
        else
        {
            if (rayoConexionOjo != null) rayoConexionOjo.enabled = false;

            // Atenuar y detener audio si se suelta el gesto
            if (audioOjo3D != null && audioOjo3D.isPlaying)
            {
                audioOjo3D.volume = Mathf.Max(0f, audioOjo3D.volume - Time.deltaTime * 2.5f);
                if (audioOjo3D.volume <= 0.05f) audioOjo3D.Stop();
            }

            if (tiempoContactoAcumulado > 0f)
            {
                // Decaimiento suave (no se borra al instante por un micro-parpadeo)
                tiempoContactoAcumulado = Mathf.Max(0f, tiempoContactoAcumulado - Time.deltaTime * 0.8f);
                float progresoRestante = tiempoContactoAcumulado / TIEMPO_REQUERIDO_CONTACTO;
                transform.localScale = Vector3.Lerp(transform.localScale, escalaOriginalOjo, Time.deltaTime * 6f);

                // Restaurar colores originales
                if (renderersOjo != null)
                {
                    for (int i = 0; i < renderersOjo.Length; i++)
                    {
                        if (renderersOjo[i] != null && renderersOjo[i].material != null && i < coloresBaseOjo.Length)
                        {
                            Color cActual = renderersOjo[i].material.HasProperty("_BaseColor")
                                ? renderersOjo[i].material.GetColor("_BaseColor")
                                : renderersOjo[i].material.color;
                            Color cRestaurado = Color.Lerp(coloresBaseOjo[i], cActual, progresoRestante);

                            if (renderersOjo[i].material.HasProperty("_BaseColor"))
                                renderersOjo[i].material.SetColor("_BaseColor", cRestaurado);
                            else
                                renderersOjo[i].material.color = cRestaurado;
                        }
                    }
                }

                if (luzAuraOjo != null)
                {
                    luzAuraOjo.intensity = Mathf.Lerp(0f, luzAuraOjo.intensity, progresoRestante);
                }
            }
        }
    }

    private void EjecutarEscapeANivel2()
    {
        if (estaAfuera || escapeIniciado) return;
        escapeIniciado = true;
        StartCoroutine(SecuenciaGranZoomYDesbloqueo());
    }

    private IEnumerator SecuenciaGranZoomYDesbloqueo()
    {
        Debug.Log("[EscapeOjo] ¡Misión cumplida! Iniciando GRAN ZOOM cinemático hacia el ojo...");
        PuedeMoverse = false;

        // Disparo sonoro 3D del vórtice dimensional del ojo
        if (audioOjo3D != null && clipOjoZoom != null)
        {
            audioOjo3D.Stop();
            audioOjo3D.pitch = 1.0f;
            audioOjo3D.PlayOneShot(clipOjoZoom, 1.0f);
        }

        if (jugador == null && Camera.main != null) jugador = Camera.main.transform;

        // Desactivar CharacterController temporalmente para permitir el vuelo cinemático hacia el ojo
        CharacterController cc = (jugador != null) ? jugador.GetComponent<CharacterController>() : null;
        if (cc != null) cc.enabled = false;

        // Contenedor de pantalla negra para fundido final al entrar en la pupila
        GameObject contenedorPantallaNegra = null;
        Renderer rendQuad = null;
        TextMesh tm = null;

        if (jugador != null)
        {
            contenedorPantallaNegra = new GameObject("PantallaNegraNivelDesbloqueado");
            contenedorPantallaNegra.transform.SetParent(jugador, false);
            contenedorPantallaNegra.transform.localPosition = new Vector3(0f, 0f, 1.20f);
            contenedorPantallaNegra.transform.localRotation = Quaternion.identity;

            GameObject fondoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fondoQuad.name = "FondoNegro";
            fondoQuad.transform.SetParent(contenedorPantallaNegra.transform, false);
            fondoQuad.transform.localPosition = Vector3.zero;
            fondoQuad.transform.localScale = new Vector3(7.5f, 7.5f, 1f);

            var col = fondoQuad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            rendQuad = fondoQuad.GetComponent<Renderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (unlit != null)
            {
                rendQuad.material = new Material(unlit);
            }
            else if (materialPantallaNegra != null)
            {
                rendQuad.material = new Material(materialPantallaNegra);
            }
            else
            {
                Material matFallback = Resources.Load<Material>("Mat_PantallaNegra");
                if (matFallback != null) rendQuad.material = new Material(matFallback);
                else rendQuad.material.color = Color.black;
            }

            // Alpha 0 inicial (transparente durante el vuelo)
            SetColorFade(rendQuad, new Color(0f, 0f, 0f, 0f));

            // Texto "NIVEL DESBLOQUEADO"
            GameObject goTexto = new GameObject("TextoNivelDesbloqueado");
            goTexto.transform.SetParent(contenedorPantallaNegra.transform, false);
            goTexto.transform.localPosition = new Vector3(0f, 0.08f, -0.05f);

            tm = goTexto.AddComponent<TextMesh>();
            tm.text = "★ NIVEL DESBLOQUEADO ★\n\nLa Cueva de Sísifo";
            tm.fontSize = 46;
            tm.characterSize = 0.026f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1.0f, 0.92f, 0.35f, 0f);
        }

        try
        {
            // 1. GRAN ZOOM CINEMÁTICO HACIA EL OJO (1.35 segundos)
            // La cámara vuela directamente hacia el centro de la pupila
            if (jugador != null)
            {
                Vector3 posInicialCam = jugador.position;
                Vector3 posDestinoOjo = transform.position - transform.forward * 0.35f;
                Quaternion rotInicialCam = jugador.rotation;
                Quaternion rotHaciaOjo = Quaternion.LookRotation((transform.position - posInicialCam).normalized, Vector3.up);

                float duracionZoom = 1.35f;
                float tZoom = 0f;

                while (tZoom < duracionZoom)
                {
                    tZoom += Time.deltaTime;
                    float progreso = Mathf.Clamp01(tZoom / duracionZoom);
                    float factorSuave = Mathf.SmoothStep(0f, 1f, progreso);

                    jugador.position = Vector3.Lerp(posInicialCam, posDestinoOjo, factorSuave);
                    jugador.rotation = Quaternion.Slerp(rotInicialCam, rotHaciaOjo, factorSuave);

                    // Dilatación extrema del ojo al ser penetrado por la cámara
                    transform.localScale = escalaOriginalOjo * Mathf.Lerp(1.35f, 2.8f, progreso);

                    // Desvanecer música de inicio suavemente al entrar al portal
                    if (audioMusicaInicio != null)
                    {
                        audioMusicaInicio.volume = Mathf.Lerp(0.65f, 0f, progreso);
                    }

                    // En los últimos 0.45s, fundir a negro absoluto al entrar en la pupila
                    if (tZoom > duracionZoom - 0.45f)
                    {
                        float a = Mathf.Clamp01((tZoom - (duracionZoom - 0.45f)) / 0.45f);
                        SetColorFade(rendQuad, new Color(0f, 0f, 0f, a));
                    }

                    yield return null;
                }
            }

            // Asegurar negro absoluto al culminar el zoom
            SetColorFade(rendQuad, Color.black);

            if (audioMusicaInicio != null) audioMusicaInicio.Stop();

            // 2. Teletransportar al interior de la Cueva 3D en completa oscuridad
            estaAfuera = true;
            transform.localScale = escalaOriginalOjo;
            Teletransportar(afuera);

            // Construir e inicializar INMEDIATAMENTE Nivel 2 para que existan las paredes y el suelo antes de cualquier pausa
            var ctrlNivel2 = FindAnyObjectByType<ControladorRocaSifiso>();
            if (ctrlNivel2 != null)
            {
                ctrlNivel2.IniciarNivel2(Vector3.forward);
            }

            // 3. Mostrar el título dorado "NIVEL DESBLOQUEADO" en la oscuridad
            float tTexto = 0f;
            while (tTexto < 0.5f)
            {
                tTexto += Time.deltaTime;
                if (tm != null) tm.color = new Color(1.0f, 0.92f, 0.35f, Mathf.Clamp01(tTexto / 0.5f));
                yield return null;
            }

            // Pausa contemplativa de 2.0 segundos con el título en la penumbra
            yield return new WaitForSeconds(2.0f);

            // 4. DISOLVER HACIA LA CUEVA: Desvanecer suavemente la pantalla negra revelando la foto de Grete
            float tFade = 0f;
            while (tFade < 0.8f)
            {
                tFade += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(tFade / 0.8f);
                SetColorFade(rendQuad, new Color(0f, 0f, 0f, alpha));
                if (tm != null)
                {
                    tm.color = new Color(1.0f, 0.92f, 0.35f, alpha);
                }
                yield return null;
            }
        }
        finally
        {
            if (contenedorPantallaNegra != null)
            {
                Destroy(contenedorPantallaNegra);
            }
            if (cc != null) cc.enabled = true;
            PuedeMoverse = true;
        }
    }

    private void SetColorFade(Renderer rend, Color col)
    {
        if (rend != null && rend.material != null)
        {
            if (rend.material.HasProperty("_BaseColor")) rend.material.SetColor("_BaseColor", col);
            if (rend.material.HasProperty("_Color")) rend.material.SetColor("_Color", col);
            rend.material.color = col;
        }
    }

    public void Teletransportar(Vector3 destino)
    {
        if (jugador == null && Camera.main != null) jugador = Camera.main.transform;

        if (jugador != null)
        {
            tiempoUltimoTeletransporte = Time.time;

            CharacterController controlador = jugador.GetComponent<CharacterController>();
            if (controlador != null)
            {
                controlador.enabled = false;
            }

            jugador.position = destino;

            // Resetear rotación de cámara mirando al frente limpio (+Z)
            var mov = jugador.GetComponent<Movimiento>();
            if (mov != null)
            {
                mov.ResetearRotacion(Quaternion.identity);
            }
            else
            {
                jugador.rotation = Quaternion.identity;
            }

            // Nivel 2 siempre centrado en (0, 0, -40) y orientado de frente (+Z)
            var nivel2 = GameObject.Find("Nivel2_Montana");
            if (nivel2 != null)
            {
                nivel2.transform.position = new Vector3(0f, 0f, -40f);
                nivel2.transform.rotation = Quaternion.identity;
            }
        }
    }
}
