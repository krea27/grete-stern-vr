using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControladorRocaSifiso : MonoBehaviour
{
    [Header("Materiales del Nivel 2 (La Cueva)")]
    [Tooltip("Material de roca para las paredes, techo y suelo de la cueva")]
    public Material materialCuevaRoca;

    [Tooltip("Fotomontaje original de Grete Stern en blanco y negro (pared frontal)")]
    public Material materialGreteBN;

    [Tooltip("Fotomontaje psicodélico de Grete Stern (invade todas las paredes)")]
    public Material materialGretePsicodelica;

    [Header("Referencias de Entorno Exterior (Nivel 3: Afuera)")]
    public Transform fondoMontana;
    public Transform sueloMontana;
    public Light luzExterior;

    [Header("Roca y Soga 3D con Física Newtoniana (Sísifo)")]
    public Material materialRoca3D;
    public Material materialSoga3D;
    private GameObject goRocaRoot;
    private GameObject goSogaRoot;
    private Rigidbody rbRoca;

    // Estados de la progresión cinemática
    public bool SogaCortada { get; private set; } = false;
    public bool PsicodeliaActiva { get; private set; } = false;
    public bool Afuera { get; private set; } = false;

    // Conteo de Símbolo de Paz apuntando a la soga (5 segundos requeridos)
    public const float TIEMPO_REQUERIDO_PAZ_SOGA = 5.0f;
    private float tiempoPazSogaAcumulado = 0f;
    private bool puedeInteractuar = false;
    private bool secuenciaIniciada = false;

    // Componentes del Cubo Cueva
    private GameObject cuboCuevaRoot;
    private GameObject paredFrontalGrete;
    private List<GameObject> paredesCubo = new List<GameObject>();
    private List<Renderer> renderersParedes = new List<Renderer>();
    private List<Vector3> direccionesExplosion = new List<Vector3>();

    // Componentes de la Soga y el Fuego
    private Transform puntoSoga;
    private Transform esferaMano;
    private LineRenderer laserFuego;
    private Light luzFuegoSoga;
    private Light luzAmbientalCueva;
    private Renderer rendEmberSoga;

    [Header("Audio y Sonido Espacial (Audífonos VR)")]
    public AudioClip clipFuegoLaser;
    public AudioClip clipCorteSoga;
    public AudioClip clipAmbientePsicodelico;
    public AudioClip clipDisolverCubo;
    public AudioClip clipVientoTriunfo;
    [Tooltip("Canción de fondo para el exterior de Nivel 3 previa al estallido. Si se deja vacía, reproduce el viento de triunfo.")]
    public AudioClip clipCancionFondoExterior;

    private AudioSource audioSourceSoga3D;
    private AudioSource audioSourceAmbiente2D;
    private ParticleSystem particulasChispas;
    private ParticleSystem particulasPolvoPsicodelico;

    void Awake()
    {
        // 1. Cargar materiales desde Resources si no están asignados en el inspector
        if (materialCuevaRoca == null)
            materialCuevaRoca = Resources.Load<Material>("Mat_CuevaRoca");
        if (materialGreteBN == null)
            materialGreteBN = Resources.Load<Material>("Mat_GreteBN");
        if (materialGretePsicodelica == null)
            materialGretePsicodelica = Resources.Load<Material>("Mat_GretePsicodelica");
        if (materialRoca3D == null)
            materialRoca3D = Resources.Load<Material>("Mat_RocaSifiso3D");
        if (materialSoga3D == null)
            materialSoga3D = Resources.Load<Material>("Mat_SogaSifiso3D");

        // Cargar audio desde Resources si no están asignados en el Inspector
        if (clipFuegoLaser == null) clipFuegoLaser = Resources.Load<AudioClip>("Audio/fuego_laser_loop");
        if (clipCorteSoga == null) clipCorteSoga = Resources.Load<AudioClip>("Audio/soga_corte_snap");
        if (clipAmbientePsicodelico == null) clipAmbientePsicodelico = Resources.Load<AudioClip>("Audio/ambiente_psicodelico_loop");
        if (clipDisolverCubo == null) clipDisolverCubo = Resources.Load<AudioClip>("Audio/cubo_disolver_estallido");
        if (clipVientoTriunfo == null) clipVientoTriunfo = Resources.Load<AudioClip>("Audio/exterior_viento_triunfo");
        if (clipCancionFondoExterior == null)
        {
            clipCancionFondoExterior = Resources.Load<AudioClip>("Audio/cancion_fondo_exterior")
                                     ?? Resources.Load<AudioClip>("Audio/musica_fondo_nivel3");
        }

        // 2. Destruir o desactivar de raíz objetos antiguos de la escena
        EliminarObjetosFlotantesAntiguos();
    }

    void Start()
    {
        SogaCortada = false;
        PsicodeliaActiva = false;
        Afuera = false;
        secuenciaIniciada = false;
        puedeInteractuar = false;
        tiempoPazSogaAcumulado = 0f;

        // Audio ambiental envolvente 2D (ambos oídos) para psicodelia y viento
        audioSourceAmbiente2D = gameObject.AddComponent<AudioSource>();
        audioSourceAmbiente2D.spatialBlend = 0.0f;
        audioSourceAmbiente2D.playOnAwake = false;
        audioSourceAmbiente2D.loop = true;

        // Ocultar entorno exterior al inicio
        BuscarYOcultarExterior();

        // Si la cámara ya está cerca de Nivel 2 (ej. pruebas en editor en z = -40), inicializar de inmediato
        var cam = Camera.main;
        if (cam != null && cam.transform.position.z < -20f)
        {
            Vector3 gaze = cam.transform.forward;
            gaze.y = 0f;
            if (gaze.sqrMagnitude < 0.01f) gaze = Vector3.forward;
            IniciarNivel2(gaze.normalized);
        }
    }

    /// <summary>
    /// Elimina o desactiva permanentemente quads flotantes viejos.
    /// </summary>
    private void EliminarObjetosFlotantesAntiguos()
    {
        string[] nombres = new string[] { "MujerYRoca", "FondoPsicodelico", "TriggerRoca", "ROCA", "PendienteFisicaCueva" };
        foreach (var n in nombres)
        {
            var go = GameObject.Find(n);
            if (go != null)
            {
                go.SetActive(false);
            }
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform hijo = transform.GetChild(i);
            string hName = hijo.name;
            if (hName.Contains("Mujer") || hName.Contains("Trigger") || hName == "ROCA" || hName.Contains("Psicodelic") || hName.Contains("Pendiente"))
            {
                hijo.gameObject.SetActive(false);
            }
        }
    }

    private void BuscarYOcultarExterior()
    {
        if (fondoMontana == null)
        {
            var t = transform.Find("FondoMontana");
            if (t != null) fondoMontana = t;
        }
        if (sueloMontana == null)
        {
            var t = transform.Find("SueloMontana");
            if (t != null) sueloMontana = t;
        }
        if (luzExterior == null)
        {
            var l = transform.Find("LuzNivel2");
            if (l != null) luzExterior = l.GetComponent<Light>();
        }

        if (fondoMontana != null) fondoMontana.gameObject.SetActive(false);
        if (sueloMontana != null) sueloMontana.gameObject.SetActive(false);
        if (luzExterior != null) luzExterior.gameObject.SetActive(false);
    }

    /// <summary>
    /// Se invoca al ingresar a Nivel 2. Coloca el cubo directamente frente al jugador
    /// de modo que la foto de Grete Stern sea la pared frontal frente a sus ojos.
    /// </summary>
    public void IniciarNivel2(Vector3 forwardPlayer)
    {
        Debug.Log("[ControladorRocaSifiso] IniciarNivel2 iniciado con vista frontal");

        transform.position = new Vector3(0f, 0f, -40f);
        transform.rotation = Quaternion.identity;

        // Asegurar que el jugador esté centrado y alejado en (0, 0, -42.6) para una vista panorámica de las 3 paredes
        var cam = Camera.main;
        if (cam != null)
        {
            CharacterController cc = cam.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            cam.transform.position = new Vector3(0f, 0f, -42.6f);
            var mov = cam.GetComponent<Movimiento>();
            if (mov != null) mov.ResetearRotacion(Quaternion.identity);
            if (EscapeOjo.PuedeMoverse && cc != null) cc.enabled = true;
        }

        // Asegurarse de que los materiales estén cargados de Resources si no vinieron del inspector
        if (materialCuevaRoca == null) materialCuevaRoca = Resources.Load<Material>("Mat_CuevaRoca");
        if (materialGreteBN == null) materialGreteBN = Resources.Load<Material>("Mat_GreteBN");
        if (materialGretePsicodelica == null) materialGretePsicodelica = Resources.Load<Material>("Mat_GretePsicodelica");
        if (materialRoca3D == null) materialRoca3D = Resources.Load<Material>("Mat_RocaSifiso3D");
        if (materialSoga3D == null) materialSoga3D = Resources.Load<Material>("Mat_SogaSifiso3D");

        // Reconstruir cubo de la cueva con orientación limpia hacia la mirada
        ConstruirCuboCuevaConFotoFrontal();
        ConfigurarSogaYLaser();

        puedeInteractuar = false;
        secuenciaIniciada = false;
        SogaCortada = false;
        PsicodeliaActiva = false;
        Afuera = false;
        tiempoPazSogaAcumulado = 0f;

        // Pausa de gracia de 0.8s para evitar que el gesto del nivel 1 active el fuego inmediatamente
        StopAllCoroutines();
        StartCoroutine(ActivarInteraccionConGracia());
    }

    private IEnumerator ActivarInteraccionConGracia()
    {
        yield return new WaitForSeconds(0.8f);
        puedeInteractuar = true;
    }

    /// <summary>
    /// Construye el Cubo de la Cueva (8m x 8m x 8m).
    /// La pared frontal es un Quad de 8m x 8m con la foto en blanco y negro de Grete Stern
    /// y posee un BoxCollider sólido como tope físico impenetrable.
    /// </summary>
    private void ConstruirCuboCuevaConFotoFrontal()
    {
        if (cuboCuevaRoot != null)
        {
            Destroy(cuboCuevaRoot);
        }

        cuboCuevaRoot = new GameObject("EstructuraCuboCueva");
        cuboCuevaRoot.transform.SetParent(transform, false);
        cuboCuevaRoot.transform.localPosition = Vector3.zero;
        cuboCuevaRoot.transform.localRotation = Quaternion.identity;

        paredesCubo.Clear();
        renderersParedes.Clear();
        direccionesExplosion.Clear();

        // 1. PARED FRONTAL: Fotografía B&W de Grete Stern ("La mujer y la roca")
        // Quad de 8m x 8m a local Z = +4m, mirando directamente al jugador (-Z)
        paredFrontalGrete = CrearCaraQuad("Pared_Frontal_Grete",
            new Vector3(0f, 0f, 4f),
            Quaternion.identity,
            new Vector3(8f, 8f, 1f),
            materialGreteBN,
            Vector3.forward);

        // 2. OTRAS 5 CARAS DE ROCA DE LA CUEVA
        // Piso
        CrearCaraQuad("Piso_Cueva",
            new Vector3(0f, -4f, 0f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(8f, 8f, 1f),
            materialCuevaRoca,
            Vector3.down);

        // Techo
        CrearCaraQuad("Techo_Cueva",
            new Vector3(0f, 4f, 0f),
            Quaternion.Euler(-90f, 0f, 0f),
            new Vector3(8f, 8f, 1f),
            materialCuevaRoca,
            Vector3.up);

        // Pared Derecha
        CrearCaraQuad("Pared_Derecha_Cueva",
            new Vector3(4f, 0f, 0f),
            Quaternion.Euler(0f, -90f, 0f),
            new Vector3(8f, 8f, 1f),
            materialCuevaRoca,
            Vector3.right);

        // Pared Izquierda
        CrearCaraQuad("Pared_Izquierda_Cueva",
            new Vector3(-4f, 0f, 0f),
            Quaternion.Euler(0f, 90f, 0f),
            new Vector3(8f, 8f, 1f),
            materialCuevaRoca,
            Vector3.left);

        // Pared Atrás
        CrearCaraQuad("Pared_Atras_Cueva",
            new Vector3(0f, 0f, -4f),
            Quaternion.Euler(0f, 180f, 0f),
            new Vector3(8f, 8f, 1f),
            materialCuevaRoca,
            Vector3.back);

        // Luz ambiental interior de la cueva (cálida, brillante y envolvente)
        GameObject luzGo = new GameObject("LuzAmbientalCueva");
        luzGo.transform.SetParent(cuboCuevaRoot.transform, false);
        luzGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        luzAmbientalCueva = luzGo.AddComponent<Light>();
        luzAmbientalCueva.type = LightType.Point;
        luzAmbientalCueva.range = 22f;
        luzAmbientalCueva.intensity = 3.0f;
        luzAmbientalCueva.color = new Color(1.0f, 0.98f, 0.92f);

        // Preparar la Roca 3D (permanece oculta hasta el corte de soga)
        CargarOConstruirRocaYSoga3D();
    }

    private GameObject CrearCaraQuad(string nombre, Vector3 localPos, Quaternion localRot, Vector3 localScale, Material mat, Vector3 dirExplosion)
    {
        GameObject cara = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cara.name = nombre;
        cara.transform.SetParent(cuboCuevaRoot.transform, false);
        cara.transform.localPosition = localPos;
        cara.transform.localRotation = localRot;
        cara.transform.localScale = localScale;

        // BoxCollider sólido como tope físico impenetrable
        var oldCol = cara.GetComponent<Collider>();
        if (oldCol != null) Destroy(oldCol);

        var boxCol = cara.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(1f, 1f, 2.0f);
        boxCol.center = new Vector3(0f, 0f, 1.0f);
        boxCol.isTrigger = false;

        var rend = cara.GetComponent<Renderer>();
        if (rend != null)
        {
            if (mat != null) rend.material = mat;
            if (nombre.Contains("Pared_Frontal") && rend.material != null)
            {
                if (rend.material.HasProperty("_EmissionColor"))
                {
                    rend.material.EnableKeyword("_EMISSION");
                    rend.material.SetColor("_EmissionColor", new Color(0.2f, 0.2f, 0.2f));
                }
            }
            renderersParedes.Add(rend);
        }

        paredesCubo.Add(cara);
        direccionesExplosion.Add(dirExplosion);
        return cara;
    }

    /// <summary>
    /// Configura el punto interactivo de la soga EXACTAMENTE en el punto marrón marcado por el usuario
    /// sobre la soga entre la mujer y la roca, con su brasa de indicación visual y el láser de fuego.
    /// </summary>
    private void ConfigurarSogaYLaser()
    {
        // Coordenadas calculadas al píxel exacto desde la imagen marcada con punto marrón por el usuario (Pixel 424.0, 179.1 en 1024x744)
        // localX = -0.687f, localY = +2.074f, localZ = 3.86f (sobre la pared frontal a z = 4.0m)
        GameObject goSoga = new GameObject("PuntoSoga");
        goSoga.transform.SetParent(cuboCuevaRoot.transform, false);
        goSoga.transform.localPosition = new Vector3(-0.687f, 2.074f, 3.86f);
        puntoSoga = goSoga.transform;

        // Audio 3D espacial para audífonos VR centrado exactamente en el punto de fuego de la soga
        audioSourceSoga3D = goSoga.AddComponent<AudioSource>();
        audioSourceSoga3D.spatialBlend = 1.0f;
        audioSourceSoga3D.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSourceSoga3D.minDistance = 1.0f;
        audioSourceSoga3D.maxDistance = 12.0f;
        audioSourceSoga3D.playOnAwake = false;

        // Sistema de chispas incandescentes de fuego brotando de la soga
        particulasChispas = CrearSistemaChispas(puntoSoga);

        // Indicador visual: brasa ardiente viva exactamente sobre el punto marrón de la soga
        GameObject goEmber = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goEmber.name = "EmberSoga";
        goEmber.transform.SetParent(puntoSoga, false);
        goEmber.transform.localPosition = Vector3.zero;
        goEmber.transform.localScale = new Vector3(0.18f, 0.18f, 0.05f);
        var colEmber = goEmber.GetComponent<Collider>();
        if (colEmber != null) Destroy(colEmber);

        rendEmberSoga = goEmber.GetComponent<Renderer>();
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (rendEmberSoga != null && unlitShader != null)
        {
            rendEmberSoga.material = new Material(unlitShader);
            rendEmberSoga.material.color = new Color(1.0f, 0.55f, 0.12f, 0.95f);
        }

        // Collider de proximidad para interacción
        var colSoga = goSoga.AddComponent<SphereCollider>();
        colSoga.radius = 1.15f;
        colSoga.isTrigger = true;

        // Luz de fuego sobre la soga
        GameObject goLuzFuego = new GameObject("LuzFuegoSoga");
        goLuzFuego.transform.SetParent(puntoSoga, false);
        goLuzFuego.transform.localPosition = new Vector3(0f, 0f, -0.15f);
        luzFuegoSoga = goLuzFuego.AddComponent<Light>();
        luzFuegoSoga.type = LightType.Point;
        luzFuegoSoga.range = 3.5f;
        luzFuegoSoga.intensity = 0.8f;
        luzFuegoSoga.color = new Color(1.0f, 0.55f, 0.15f);

        // Línea láser de fuego conectando la mano a la soga
        GameObject goLaser = new GameObject("LaserFuegoMano");
        goLaser.transform.SetParent(transform, false);
        laserFuego = goLaser.AddComponent<LineRenderer>();
        laserFuego.startWidth = 0.045f;
        laserFuego.endWidth = 0.085f;
        laserFuego.positionCount = 2;
        if (unlitShader != null) laserFuego.material = new Material(unlitShader);
        laserFuego.startColor = new Color(1.0f, 0.45f, 0.05f, 1.0f);
        laserFuego.endColor = new Color(1.0f, 0.95f, 0.40f, 1.0f);
        laserFuego.enabled = false;
    }

    void Update()
    {
        if (esferaMano == null)
        {
            var go = GameObject.Find("Sphere");
            if (go != null) esferaMano = go.transform;
        }

        // FASE 1: Esperar contacto con Símbolo de Paz durante 5 segundos
        if (!SogaCortada && puedeInteractuar && !secuenciaIniciada)
        {
            ProcesarContactoYPazSoga();
        }

        // FASE 2: Animación psicodélica viva en todas las paredes tras invadir el cubo
        if (PsicodeliaActiva && !Afuera)
        {
            // Ondulación UV viva y fluida (efecto GIF notorio)
            float offsetU = Mathf.Sin(Time.time * 2.5f) * 0.08f;
            float offsetV = Mathf.Cos(Time.time * 2.0f) * 0.07f;

            // Ciclo cromático psicodélico vivo y brillante
            float hue = (Time.time * 0.18f) % 1.0f;
            Color cChroma = Color.HSVToRGB(hue, 0.35f, 1.25f);

            // Modulación de brillo vivo
            float pulsoBrillo = 1.0f + Mathf.Sin(Time.time * 3.6f) * 0.28f;
            Color cFinal = cChroma * pulsoBrillo;
            cFinal.a = 1.0f;

            foreach (var rend in renderersParedes)
            {
                if (rend != null && rend.material != null)
                {
                    rend.material.SetTextureOffset("_BaseMap", new Vector2(offsetU, offsetV));
                    rend.material.SetTextureOffset("_MainTex", new Vector2(offsetU, offsetV));
                    rend.material.SetVector("_BaseMap_ST", new Vector4(1f, 1f, offsetU, offsetV));

                    if (rend.material.HasProperty("_BaseColor"))
                        rend.material.SetColor("_BaseColor", cFinal);
                    if (rend.material.HasProperty("_Color"))
                        rend.material.SetColor("_Color", cFinal);
                }
            }

            // Respiración física hipnótica del cubo (la habitación se expande y contrae orgánicamente)
            float pulsoEscala = 1.0f + Mathf.Sin(Time.time * 2.2f) * 0.045f;
            if (cuboCuevaRoot != null)
            {
                float temblor = Mathf.Sin(Time.time * 28f) * 0.015f;
                cuboCuevaRoot.transform.localScale = Vector3.one * pulsoEscala;
                cuboCuevaRoot.transform.localPosition = new Vector3(temblor, temblor * 0.5f, 0f);
            }

            // Luz ambiental cambiante psicodélica
            if (luzAmbientalCueva != null)
            {
                float hueLuz = (Time.time * 0.25f) % 1.0f;
                luzAmbientalCueva.color = Color.HSVToRGB(hueLuz, 0.80f, 1.0f);
                luzAmbientalCueva.intensity = 2.5f + Mathf.Sin(Time.time * 4.2f) * 1.5f;
            }
        }
    }

    /// <summary>
    /// Comprueba si el jugador está apuntando o tocando el centro de la soga Y manteniendo el SÍMBOLO DE PAZ durante 5 segundos.
    /// </summary>
    private void ProcesarContactoYPazSoga()
    {
        if (puntoSoga == null) return;

        // 1. Determinar si la mano apunta o toca la soga
        bool contactoSoga = false;
        if (esferaMano != null)
        {
            float distSoga = Vector3.Distance(esferaMano.position, puntoSoga.position);
            bool manoCercaSoga = (distSoga <= 2.2f);

            var cam = Camera.main;
            bool apuntandoSoga = false;
            if (cam != null)
            {
                Vector3 toMano = esferaMano.position - cam.transform.position;
                if (toMano.sqrMagnitude > 0.3f)
                {
                    Ray rayo = new Ray(cam.transform.position, toMano.normalized);
                    Bounds bSoga = new Bounds(puntoSoga.position, new Vector3(3.2f, 3.2f, 2.4f));
                    if (bSoga.IntersectRay(rayo, out float hitDist) && hitDist <= 11.0f)
                    {
                        apuntandoSoga = true;
                    }
                }
            }

            contactoSoga = (manoCercaSoga || apuntandoSoga);
        }

        // 2. Determinar si se está realizando el símbolo de paz
        bool haciendoPaz = OSCHandReceiver.IsPeaceSign || VRHandVisual.EsSimboloDePaz;

#if UNITY_EDITOR
        // Atajo en editor para probar fácilmente con teclado (teclas P, V, Espacio o Clic Izquierdo)
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.pKey.isPressed ||
                UnityEngine.InputSystem.Keyboard.current.vKey.isPressed ||
                UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed)
            {
                contactoSoga = true;
                haciendoPaz = true;
            }
        }
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        {
            contactoSoga = true;
            haciendoPaz = true;
        }
#endif

        // 3. Progresión del fuego durante los 5 segundos
        if (contactoSoga && haciendoPaz)
        {
            tiempoPazSogaAcumulado += Time.deltaTime;
            float progreso = Mathf.Clamp01(tiempoPazSogaAcumulado / TIEMPO_REQUERIDO_PAZ_SOGA);

            // Activar y actualizar el láser de fuego incandescente
            if (laserFuego != null)
            {
                laserFuego.enabled = true;
                Vector3 posOrigen = (esferaMano != null) ? esferaMano.position : (transform.position + Vector3.up * 1.2f);
                laserFuego.SetPosition(0, posOrigen);

                Vector3 jitter = new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(-0.04f, 0.04f), 0f);
                laserFuego.SetPosition(1, puntoSoga.position + jitter);

                // Engrosar y hacer más brillante el láser a medida que avanza el tiempo
                laserFuego.startWidth = Mathf.Lerp(0.04f, 0.09f, progreso);
                laserFuego.endWidth = Mathf.Lerp(0.08f, 0.16f, progreso);

                Color colorProgreso = Color.Lerp(new Color(1.0f, 0.45f, 0.05f), new Color(1.0f, 0.95f, 0.40f), progreso);
                laserFuego.startColor = colorProgreso;
            }

            // Luz de fuego intensa y parpadeante
            if (luzFuegoSoga != null)
            {
                luzFuegoSoga.intensity = Mathf.Lerp(1.5f, 6.5f, progreso) + Random.Range(0f, 2.5f);
                luzFuegoSoga.range = Mathf.Lerp(3.0f, 5.5f, progreso);
                luzFuegoSoga.color = Color.Lerp(new Color(1.0f, 0.45f, 0.10f), new Color(1.0f, 0.90f, 0.35f), progreso);
            }

            // Pulsación de la brasa en la soga
            if (rendEmberSoga != null && rendEmberSoga.material != null)
            {
                float pulso = 1.0f + Mathf.Sin(Time.time * 25f) * 0.35f;
                rendEmberSoga.transform.localScale = new Vector3(0.12f, 0.12f, 0.04f) * (1.0f + progreso * 0.8f) * pulso;
                rendEmberSoga.material.color = Color.Lerp(new Color(1.0f, 0.5f, 0.1f), Color.white, progreso);
            }

            // Audio 3D del fuego ardiente (sube de tono e intensidad durante los 5s)
            if (audioSourceSoga3D != null && clipFuegoLaser != null)
            {
                if (!audioSourceSoga3D.isPlaying)
                {
                    audioSourceSoga3D.clip = clipFuegoLaser;
                    audioSourceSoga3D.loop = true;
                    audioSourceSoga3D.Play();
                }
                audioSourceSoga3D.pitch = Mathf.Lerp(0.9f, 1.45f, progreso);
                audioSourceSoga3D.volume = Mathf.Lerp(0.45f, 1.0f, progreso);
            }

            // Lluvia de chispas incandescentes que se intensifica con el corte
            if (particulasChispas != null)
            {
                var em = particulasChispas.emission;
                em.rateOverTime = Mathf.Lerp(25f, 90f, progreso);
                if (!particulasChispas.isPlaying) particulasChispas.Play();
            }

            // Al cumplirse los 5 segundos completos: ¡LA SOGA SE CORTA!
            if (tiempoPazSogaAcumulado >= TIEMPO_REQUERIDO_PAZ_SOGA)
            {
                CortarSogaYDesatarMagia();
            }
        }
        else
        {
            // Apagar el láser si no se mantiene la puntería o el gesto
            if (laserFuego != null) laserFuego.enabled = false;

            // Atenuar y pausar el sonido del fuego
            if (audioSourceSoga3D != null && audioSourceSoga3D.isPlaying)
            {
                audioSourceSoga3D.volume = Mathf.Max(0f, audioSourceSoga3D.volume - Time.deltaTime * 2.5f);
                if (audioSourceSoga3D.volume <= 0.05f) audioSourceSoga3D.Stop();
            }

            // Detener emisión de chispas
            if (particulasChispas != null)
            {
                var em = particulasChispas.emission;
                em.rateOverTime = 0;
            }

            // Decaimiento suave (no se reinicia a 0 de golpe por un microparpadeo)
            if (tiempoPazSogaAcumulado > 0f)
            {
                tiempoPazSogaAcumulado = Mathf.Max(0f, tiempoPazSogaAcumulado - Time.deltaTime * 0.8f);
                float progresoRestante = tiempoPazSogaAcumulado / TIEMPO_REQUERIDO_PAZ_SOGA;

                if (luzFuegoSoga != null)
                {
                    luzFuegoSoga.intensity = Mathf.Lerp(0.8f, 2.5f, progresoRestante);
                }
            }
        }
    }

    public void CortarSogaYDesatarMagia()
    {
        if (secuenciaIniciada) return;
        secuenciaIniciada = true;
        StartCoroutine(SecuenciaCinematicaNivel2());
    }

    private IEnumerator SecuenciaCinematicaNivel2()
    {
        Debug.Log("[ControladorRocaSifiso] ¡5 SEGUNDOS DE SÍMBOLO DE PAZ CUMPLIDOS! La soga se corta: Liberando la roca bajo física newtoniana...");

        // 1. ¡LA SOGA SE CORTA!
        SogaCortada = true;

        if (laserFuego != null) laserFuego.enabled = false;
        if (luzFuegoSoga != null) Destroy(luzFuegoSoga.gameObject);
        if (rendEmberSoga != null) Destroy(rendEmberSoga.gameObject);

        // Sonido 3D de chasquido seco del corte de la soga
        if (audioSourceSoga3D != null && clipCorteSoga != null)
        {
            audioSourceSoga3D.Stop();
            audioSourceSoga3D.pitch = 1.0f;
            audioSourceSoga3D.PlayOneShot(clipCorteSoga, 1.0f);
        }

        // Eliminar inmediatamente las chispas al cortar la soga para evitar cuadraditos o partículas residuales
        if (particulasChispas != null)
        {
            particulasChispas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(particulasChispas.gameObject);
        }

        // Si existe soga separada, desactivarla para mostrar el corte
        if (goSogaRoot != null)
        {
            goSogaRoot.SetActive(false);
        }

        // 2. SISTEMA COMPLEJO FÍSICO NEWTONIANO: La roca 3D emerge detrás de la roca de la imagen y sale disparada rodando y rebotando
        if (goRocaRoot != null)
        {
            Debug.Log("[ControladorRocaSifiso] La roca 3D emerge detrás de la imagen de Grete Stern: Disparo dinámico con física aplicada y rebotes...");
            goRocaRoot.transform.localPosition = new Vector3(0.85f, 0.45f, 3.95f);
            goRocaRoot.transform.localRotation = Quaternion.Euler(-15f, -28f, 10f);
            goRocaRoot.SetActive(true);

            if (rbRoca != null)
            {
                rbRoca.isKinematic = false;
                rbRoca.useGravity = true;
                rbRoca.linearVelocity = Vector3.zero;
                rbRoca.angularVelocity = Vector3.zero;

                // Desprendimiento físico controlado y pesado (más lento, sin giro descontrolado)
                rbRoca.maxAngularVelocity = 6.0f;
                rbRoca.linearDamping = 0.12f;
                rbRoca.angularDamping = 0.55f;

                // Impulso moderado hacia adelante (-Z) y abajo (-Y) con rebotes
                Vector3 impulsoSalida = new Vector3(0.4f, -1.2f, -2.2f).normalized * 220f;
                rbRoca.AddForce(impulsoSalida, ForceMode.Impulse);

                // Torque moderado para rodadura natural y pesada
                Vector3 torqueRodadura = new Vector3(-6f, 2f, -4f) * 18f;
                rbRoca.AddTorque(torqueRodadura, ForceMode.Impulse);
            }
        }

        // Temblor sísmico en cámara por el estruendo de la enorme roca rodando y cayendo
        StartCoroutine(TemblorCamaraRodadura(3.2f));

        // Dejar que la simulación física newtoniana se desarrolle por la cueva antes de la psicodelia
        yield return new WaitForSeconds(3.2f);

        Debug.Log("[ControladorRocaSifiso] La roca ha descendido rodando al abismo. ¡Iniciando invasión psicodélica limpia!");

        // 3. FASE PSICODÉLICA: Invasión de las 6 paredes y ambiente psicodélico (sin partículas cuadradas molestas)
        // Música ambiental psicodélica envolvente 2D
        if (audioSourceAmbiente2D != null && clipAmbientePsicodelico != null)
        {
            audioSourceAmbiente2D.clip = clipAmbientePsicodelico;
            audioSourceAmbiente2D.volume = 0.85f;
            audioSourceAmbiente2D.loop = true;
            audioSourceAmbiente2D.Play();
        }

        // ¡ENTRA EL CUBO PSICODÉLICO EN LAS 6 PAREDES LIMPIAS!
        Debug.Log("[ControladorRocaSifiso] ¡Invasión psicodélica en las 6 paredes!");
        if (materialGretePsicodelica != null)
        {
            foreach (var cara in paredesCubo)
            {
                if (cara != null)
                {
                    var rend = cara.GetComponent<Renderer>();
                    if (rend != null) rend.material = materialGretePsicodelica;
                }
            }
        }
        PsicodeliaActiva = true;

        if (luzAmbientalCueva != null)
        {
            luzAmbientalCueva.color = new Color(0.95f, 0.4f, 1.0f); // Púrpura psicodélico vivo
            luzAmbientalCueva.intensity = 3.0f;
        }

        // 3. LA ESCENA PSICODÉLICA DURA 10 SEGUNDOS COMPLETOS (tiempo extendido para disfrutar la inmersión)
        Debug.Log("[ControladorRocaSifiso] Inmersión psicodélica activa por 10 segundos completos...");
        yield return new WaitForSeconds(10.0f);

        // 4. EL CUBO SE DISUELVE EN PEDAZOS: ¡LA ESCULTURA 3D APARECE AFUERA DE INMEDIATO SIN RETARDOS!
        Debug.Log("[ControladorRocaSifiso] ¡El cubo se rompe y cae todo! Mostrando de inmediato la Escultura 3D afuera sin retardos.");
        PsicodeliaActiva = false;
        Afuera = true;

        // Sonido de estallido y derrumbe de roca
        if (audioSourceAmbiente2D != null && clipDisolverCubo != null)
        {
            audioSourceAmbiente2D.PlayOneShot(clipDisolverCubo, 1.0f);
        }

        // Viento exterior y notas celestiales de triunfo al salir al exterior
        if (audioSourceAmbiente2D != null)
        {
            if (clipVientoTriunfo != null)
            {
                audioSourceAmbiente2D.clip = clipVientoTriunfo;
                audioSourceAmbiente2D.volume = 0.75f;
                audioSourceAmbiente2D.loop = true;
                audioSourceAmbiente2D.Play();
            }
            else
            {
                audioSourceAmbiente2D.Stop();
            }
        }

        // Quitar de inmediato la imagen 2D gigante de fondo
        if (fondoMontana != null)
        {
            fondoMontana.gameObject.SetActive(false);
        }
        var goFM = GameObject.Find("FondoMontana");
        if (goFM != null) goFM.SetActive(false);

        // Configurar suelo exterior con la textura de roca (textura_cueva_roca)
        if (sueloMontana != null)
        {
            sueloMontana.gameObject.SetActive(true);
            sueloMontana.localPosition = new Vector3(0f, -1.3f, 3.5f);
            sueloMontana.localScale = new Vector3(3.5f, 1.0f, 3.5f);

            var rendSuelo = sueloMontana.GetComponent<Renderer>();
            if (rendSuelo != null && materialCuevaRoca != null)
            {
                rendSuelo.material = materialCuevaRoca;
            }
        }

        // Iluminación exterior activa
        if (luzExterior != null)
        {
            luzExterior.gameObject.SetActive(true);
            luzExterior.enabled = true;
            luzExterior.intensity = 1.6f;
        }

        // Posicionar al jugador erguido y firme sobre el suelo mirando hacia la escultura de frente
        var camJugador = Camera.main;
        if (camJugador != null)
        {
            CharacterController ccJugador = camJugador.GetComponent<CharacterController>();
            if (ccJugador != null) ccJugador.enabled = false;

            camJugador.transform.position = new Vector3(0f, 0f, -42.6f);

            var mov = camJugador.GetComponent<Movimiento>();
            if (mov != null) mov.ResetearRotacion(Quaternion.identity);

            if (ccJugador != null) ccJugador.enabled = true;
        }

        // ¡LA ESCULTURA 3D YA ESTÁ AFUERA DESDE EL PRIMER INSTANTE DEL QUIEBRE!
        var escultura = FindAnyObjectByType<EsculturaExplosivaFlocking>();
        if (escultura == null)
        {
            var goEsc = new GameObject("EsculturaExplosivaFlocking");
            escultura = goEsc.AddComponent<EsculturaExplosivaFlocking>();
        }
        escultura.IniciarContemplacionEscultura();

        // Las 6 paredes del cubo se rompen, giran y caen al abismo mientras la escultura YA se contempla afuera
        float tDisolver = 0f;
        float duracionDisolver = 1.8f;
        List<Vector3> posicionesIniciales = new List<Vector3>();
        foreach (var cara in paredesCubo)
        {
            posicionesIniciales.Add(cara != null ? cara.transform.localPosition : Vector3.zero);
        }

        while (tDisolver < duracionDisolver)
        {
            tDisolver += Time.deltaTime;
            float progreso = Mathf.Clamp01(tDisolver / duracionDisolver);
            float factorSuave = progreso * progreso;

            for (int i = 0; i < paredesCubo.Count; i++)
            {
                if (paredesCubo[i] != null)
                {
                    Vector3 offset = direccionesExplosion[i] * (factorSuave * 16f);
                    paredesCubo[i].transform.localPosition = posicionesIniciales[i] + offset;
                    paredesCubo[i].transform.Rotate(direccionesExplosion[i] * (Time.deltaTime * 65f), Space.Self);

                    paredesCubo[i].transform.localScale = Vector3.Lerp(new Vector3(8f, 8f, 1f), Vector3.zero, progreso);
                }
            }
            yield return null;
        }

        if (cuboCuevaRoot != null) Destroy(cuboCuevaRoot);
    }

    private IEnumerator MostrarBannerNivel3()
    {
        var cam = Camera.main;
        if (cam == null) yield break;

        // Contenedor anclado frente a la mirada del jugador (2.2m)
        GameObject goBanner = new GameObject("BannerNivel3Exterior");
        goBanner.transform.SetParent(cam.transform, false);
        goBanner.transform.localPosition = new Vector3(0f, 0.12f, 2.2f);
        goBanner.transform.localRotation = Quaternion.identity;

        // 1. Placa de fondo negro para contraste perfecto y lectura nítida
        GameObject fondoPlaca = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fondoPlaca.name = "FondoNegroBanner";
        fondoPlaca.transform.SetParent(goBanner.transform, false);
        fondoPlaca.transform.localPosition = new Vector3(0f, 0f, 0.04f); // Justo detrás de las letras
        fondoPlaca.transform.localScale = new Vector3(3.6f, 1.8f, 1.0f);

        var colFondo = fondoPlaca.GetComponent<Collider>();
        if (colFondo != null) Destroy(colFondo);

        Renderer rendFondo = fondoPlaca.GetComponent<Renderer>();
        Material matFondo = null;
        Material matBaseNegro = Resources.Load<Material>("Mat_PantallaNegra");
        if (matBaseNegro != null)
        {
            matFondo = new Material(matBaseNegro);
        }
        else
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (unlitShader != null) matFondo = new Material(unlitShader);
        }
        if (rendFondo != null && matFondo != null) rendFondo.material = matFondo;

        // 2. Texto dorado nítido y centrado
        GameObject goTexto = new GameObject("TextoBanner");
        goTexto.transform.SetParent(goBanner.transform, false);
        goTexto.transform.localPosition = Vector3.zero;

        TextMesh tm = goTexto.AddComponent<TextMesh>();
        tm.text = "★ NIVEL 3 DESBLOQUEADO ★\n\nEl Mundo Exterior\n¡Has liberado a la mujer!";
        tm.fontSize = 46;
        tm.characterSize = 0.024f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1.0f, 0.92f, 0.35f, 0f);

        // Fade In conjunto (0.8s)
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / 0.8f);
            if (matFondo != null)
            {
                Color cFondo = new Color(0.02f, 0.02f, 0.02f, alpha * 0.94f);
                if (matFondo.HasProperty("_BaseColor")) matFondo.SetColor("_BaseColor", cFondo);
                else matFondo.color = cFondo;
            }
            if (tm != null) tm.color = new Color(1.0f, 0.92f, 0.35f, alpha);
            yield return null;
        }

        // Mantener 4.0 segundos para lectura cómoda y relajada
        yield return new WaitForSeconds(4.0f);

        // Fade Out conjunto (1.0s)
        t = 0f;
        while (t < 1.0f)
        {
            t += Time.deltaTime;
            float alpha = 1.0f - Mathf.Clamp01(t / 1.0f);
            if (matFondo != null)
            {
                Color cFondo = new Color(0.02f, 0.02f, 0.02f, alpha * 0.94f);
                if (matFondo.HasProperty("_BaseColor")) matFondo.SetColor("_BaseColor", cFondo);
                else matFondo.color = cFondo;
            }
            if (tm != null) tm.color = new Color(1.0f, 0.92f, 0.35f, alpha);
            yield return null;
        }

        Destroy(goBanner);

        // Iniciar la contemplación de la Escultura 3D de Grete Stern con Física de Rebotes y Flocking
        var escultura = FindAnyObjectByType<EsculturaExplosivaFlocking>();
        if (escultura == null)
        {
            var goEsc = new GameObject("EsculturaExplosivaFlocking");
            escultura = goEsc.AddComponent<EsculturaExplosivaFlocking>();
        }
        escultura.IniciarContemplacionEscultura();
    }

    private ParticleSystem CrearSistemaChispas(Transform parent)
    {
        GameObject go = new GameObject("ChispasFuegoSoga");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.45f;
        main.startSpeed = 2.4f;
        main.startSize = 0.045f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.1f), new Color(1f, 0.95f, 0.4f));
        main.gravityModifier = 2.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (unlit != null)
            {
                Material matP = new Material(unlit);
                matP.color = new Color(1f, 0.85f, 0.3f);
                rend.material = matP;
            }
        }

        return ps;
    }

    private ParticleSystem CrearPolvoPsicodelico(Transform parent)
    {
        // Desactivado por completo: la escena psicodélica se mantiene limpia y libre de partículas cuadradas invasivas
        return null;
    }

    /// <summary>
    /// Configura la Roca y la Soga 3D dentro de la cueva sobre una pendiente inclinada,
    /// aplicando las texturas auténticas de Grete Stern y los componentes de física newtoniana.
    /// </summary>
    private void CargarOConstruirRocaYSoga3D()
    {
        // 1. Cargar materiales
        if (materialRoca3D == null) materialRoca3D = Resources.Load<Material>("Mat_RocaSifiso3D");
        if (materialSoga3D == null) materialSoga3D = Resources.Load<Material>("Mat_SogaSifiso3D");

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (materialRoca3D == null && litShader != null)
        {
            materialRoca3D = new Material(litShader);
            Texture2D texRoca = Resources.Load<Texture2D>("Roca_albedo") ?? Resources.Load<Texture2D>("Roca_Grete_Original");
            if (texRoca != null) materialRoca3D.mainTexture = texRoca;
        }
        if (materialSoga3D == null && litShader != null)
        {
            materialSoga3D = new Material(litShader);
            Texture2D texSoga = Resources.Load<Texture2D>("Soga_albedo");
            if (texSoga != null) materialSoga3D.mainTexture = texSoga;
        }

        // 2. Cargar o instanciar la Roca 3D
        GameObject prefabRoca = Resources.Load<GameObject>("Roca_sola") ?? Resources.Load<GameObject>("Roca_y_soga");
        if (prefabRoca != null)
        {
            goRocaRoot = Instantiate(prefabRoca, cuboCuevaRoot.transform, false);
            goRocaRoot.name = "RocaSifiso3D";
        }
        else
        {
            // Intentar cargar la malla directamente desde el archivo OBJ en Assets
            string rutaObj = System.IO.Path.Combine(Application.dataPath, "Nivel2_Montana", "RocaYSoga", "Roca_sola.obj");
            if (!System.IO.File.Exists(rutaObj))
            {
                rutaObj = System.IO.Path.Combine(Application.dataPath, "Resources", "Roca_sola.obj");
            }
            Mesh mObj = CargarMeshDesdeObj(rutaObj);
            if (mObj != null)
            {
                goRocaRoot = new GameObject("RocaSifiso3D");
                goRocaRoot.transform.SetParent(cuboCuevaRoot.transform, false);
                var mf = goRocaRoot.AddComponent<MeshFilter>();
                mf.mesh = mObj;
                var mr = goRocaRoot.AddComponent<MeshRenderer>();
                if (materialRoca3D != null) mr.material = materialRoca3D;
            }
            else
            {
                goRocaRoot = ConstruirRocaProcedural(cuboCuevaRoot.transform);
            }
        }

        // Posición y orientación de la roca justo en la pared frontal (Z = 3.95m, detrás de la roca de la foto)
        goRocaRoot.transform.localPosition = new Vector3(0.85f, 0.45f, 3.95f);
        goRocaRoot.transform.localRotation = Quaternion.Euler(-15f, -28f, 10f);
        goRocaRoot.transform.localScale = Vector3.one * 1.35f;

        // Asignar materiales a todos los renderers hijos
        foreach (var r in goRocaRoot.GetComponentsInChildren<Renderer>())
        {
            if (r.name.ToLower().Contains("soga"))
            {
                if (materialSoga3D != null) r.material = materialSoga3D;
            }
            else
            {
                if (materialRoca3D != null) r.material = materialRoca3D;
            }
        }

        // 3. Configurar Física Newtoniana: Rigidbody y Collider
        foreach (var col in goRocaRoot.GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }

        PhysicsMaterial matFisicoRoca = new PhysicsMaterial("FisicaRocaSifiso")
        {
            dynamicFriction = 0.32f,
            staticFriction = 0.42f,
            bounciness = 0.45f,
            frictionCombine = PhysicsMaterialCombine.Multiply,
            bounceCombine = PhysicsMaterialCombine.Average
        };

        SphereCollider sphereCol = goRocaRoot.AddComponent<SphereCollider>();
        sphereCol.radius = 1.15f;
        sphereCol.center = Vector3.zero;
        sphereCol.material = matFisicoRoca;

        rbRoca = goRocaRoot.GetComponent<Rigidbody>();
        if (rbRoca == null) rbRoca = goRocaRoot.AddComponent<Rigidbody>();
        rbRoca.mass = 320f;
        rbRoca.linearDamping = 0.12f;
        rbRoca.angularDamping = 0.55f;
        rbRoca.maxAngularVelocity = 6.0f;
        rbRoca.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rbRoca.interpolation = RigidbodyInterpolation.Interpolate;
        rbRoca.isKinematic = true; // En reposo estático hasta que se corte la soga
        rbRoca.useGravity = false;

        // Detector de colisiones con audio para simular el impacto físico
        var detector = goRocaRoot.AddComponent<DetectorImpactoRoca>();
        detector.clipImpacto = clipDisolverCubo;

        // 4. Cargar o instanciar Soga 3D adicional si está separada
        GameObject prefabSoga = Resources.Load<GameObject>("Soga_sola");
        if (prefabSoga != null && goRocaRoot.transform.Find("Soga_sola") == null)
        {
            goSogaRoot = Instantiate(prefabSoga, cuboCuevaRoot.transform, false);
            goSogaRoot.name = "SogaSifiso3D";
            goSogaRoot.transform.localPosition = new Vector3(0.85f, 0.45f, 3.95f);
            goSogaRoot.transform.localRotation = Quaternion.Euler(-15f, -28f, 10f);
            goSogaRoot.transform.localScale = Vector3.one * 1.35f;

            if (materialSoga3D != null)
            {
                foreach (var r in goSogaRoot.GetComponentsInChildren<Renderer>())
                {
                    r.material = materialSoga3D;
                }
            }
        }

        // La roca 3D y la soga permanecen OCULTAS al inicio.
        // Solo emergerán detrás de la roca de la imagen al momento exacto en que se corta la soga.
        goRocaRoot.SetActive(false);
        if (goSogaRoot != null)
        {
            goSogaRoot.SetActive(false);
        }
    }

    private GameObject ConstruirRocaProcedural(Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "RocaSifiso3D_Procedural";
        go.transform.SetParent(parent, false);

        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Mesh m = Instantiate(mf.sharedMesh);
            Vector3[] v = m.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                float ruido = Mathf.PerlinNoise(v[i].x * 3.5f + 1.2f, v[i].y * 3.5f + 2.8f) * 0.35f;
                v[i] += v[i].normalized * ruido;
            }
            m.vertices = v;
            m.RecalculateNormals();
            m.RecalculateBounds();
            mf.mesh = m;
        }

        var r = go.GetComponent<Renderer>();
        if (r != null && materialRoca3D != null) r.material = materialRoca3D;

        return go;
    }

    private Mesh CargarMeshDesdeObj(string rutaObj)
    {
        if (string.IsNullOrEmpty(rutaObj) || !System.IO.File.Exists(rutaObj)) return null;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        try
        {
            string[] lineas = System.IO.File.ReadAllLines(rutaObj);
            List<Vector2> tempUVs = new List<Vector2>();

            for (int i = 0; i < lineas.Length; i++)
            {
                string l = lineas[i].Trim();
                if (l.StartsWith("v "))
                {
                    string[] p = l.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4)
                    {
                        float x = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                        float y = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
                        float z = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture);
                        verts.Add(new Vector3(x, y, z));
                    }
                }
                else if (l.StartsWith("vt "))
                {
                    string[] p = l.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 3)
                    {
                        float u = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                        float v = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
                        tempUVs.Add(new Vector2(u, v));
                    }
                }
                else if (l.StartsWith("f "))
                {
                    string[] p = l.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4)
                    {
                        int v0 = int.Parse(p[1].Split('/')[0]) - 1;
                        int v1 = int.Parse(p[2].Split('/')[0]) - 1;
                        int v2 = int.Parse(p[3].Split('/')[0]) - 1;

                        tris.Add(v0);
                        tris.Add(v1);
                        tris.Add(v2);

                        if (p.Length >= 5)
                        {
                            int v3 = int.Parse(p[4].Split('/')[0]) - 1;
                            tris.Add(v0);
                            tris.Add(v2);
                            tris.Add(v3);
                        }
                    }
                }
            }

            if (verts.Count > 0 && tris.Count > 0)
            {
                Mesh m = new Mesh();
                if (verts.Count > 65000)
                {
                    m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }
                m.SetVertices(verts);
                if (tempUVs.Count == verts.Count)
                {
                    m.SetUVs(0, tempUVs);
                }
                else
                {
                    List<Vector2> autoUVs = new List<Vector2>();
                    for (int i = 0; i < verts.Count; i++)
                    {
                        float u = Mathf.Atan2(verts[i].z, verts[i].x) / (2f * Mathf.PI) + 0.5f;
                        float v = Mathf.Clamp01((verts[i].y + 1.2f) / 2.4f);
                        autoUVs.Add(new Vector2(u, v));
                    }
                    m.SetUVs(0, autoUVs);
                }
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[ControladorRocaSifiso] Error leyendo OBJ manual: " + ex.Message);
        }
        return null;
    }

    private IEnumerator TemblorCamaraRodadura(float duracion)
    {
        var cam = Camera.main;
        if (cam == null) yield break;

        Vector3 posOriginal = cam.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < duracion)
        {
            elapsed += Time.deltaTime;
            float progreso = elapsed / duracion;
            float intensidad = (1.0f - progreso) * 0.038f;
            float offsetX = (Mathf.PerlinNoise(Time.time * 28f, 0f) - 0.5f) * intensidad;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * 28f) - 0.5f) * intensidad;

            cam.transform.localPosition = posOriginal + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }
        cam.transform.localPosition = posOriginal;
    }
}
