using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema Cinematográfico de Escultura 3D Monumental de Grete Stern:
/// 1. Escultura de frente con proporciones uniformes 1:1:1.
/// 2. Carga Sísmica y Resonancia Previa con tensión creciente (1.8s).
/// 3. Gran Estallido Épico: 340 rocas físicas texturizadas que estallan AL CIELO en arcos visibles,
///    caen ante la mirada del espectador y rebotan con retumbos de choque de piedra.
/// 4. Nube de Polvo de Cantera Orgánica y Difuminada (44 bocanadas asimétricas desincronizadas, sin contorno circular artificial).
/// 5. Sistema Complejo de Vida Artificial: Enjambre de 36 Boids de roca pura con algoritmo canónico de Flocking de Craig Reynolds (Separación, Alineación y Cohesión), sin estelas blancas, que tras unos segundos de vuelo conjunto caen paulatinamente en cascada hacia el suelo con gravedad newtoniana y rebotes sonoros.
/// 6. Cero artefactos turquesas ni estelas artificiales: iluminación cálida natural de cantera y física continua con rebotes múltiples en cadena.
/// 7. Duración épica extendida (12.0 segundos) para contemplar la lluvia total de rocas, la disipación del polvo y el asentamiento final.
/// </summary>
public class EsculturaExplosivaFlocking : MonoBehaviour
{
    [Header("Configuración y Pruebas")]
    [Tooltip("Iniciar directamente la escultura al presionar Play para pruebas")]
    public bool probarDirecto = false;

    [Header("Posición y Orientación de la Escultura")]
    public Vector3 posicionRelativa = new Vector3(0f, 0.35f, 6.0f);
    public float alturaEsculturaDeseada = 3.8f; // Escultura 3D más grande e imponente (antes 2.4f)
    [Tooltip("Ajuste fino de rotación Y del modelo si se desea cambiar el ángulo de la cara")]
    public float rotacionYModelo = 0f;

    [Header("Parámetros Físicos del Gran Estallido Épico (340 Rocas Texturizadas)")]
    public int totalFragmentosRoca = 340;   // Cientos de rocas en tamaños continuos aleatorios
    public float bouncinessRebotes = 0.88f; // Rebotes elásticos muy vivos y prolongados

    [Header("Parámetros del Sistema Complejo (Flocking / Boids Reynolds)")]
    public int cantidadBoids = 36;
    public float velocidadMaxBoids = 9.8f;
    public float fuerzaMaxBoids = 12.5f;
    public float radioSeparacion = 1.9f;
    public float radioVecinos = 5.5f;

    // Estados
    public bool EsculturaActiva { get; private set; } = false;
    public bool EstallidoOcurrido { get; private set; } = false;

    private Transform jugador;
    private GameObject goEsculturaRoot;
    private GameObject goModelo3D;
    private Light luzAura;
    private AudioSource audioSource;
    private AudioClip clipEstallido;
    private AudioClip clipCrack;
    private AudioClip clipResonancia;

    private Material materialEscultura;
    private Material materialRocaReal;
    private Material materialHumoPolvo;
    private PhysicsMaterial materialFisicoRebote;

    private List<GameObject> rocasBalisticas = new List<GameObject>();
    private List<BoidFlock> enjambreBoids = new List<BoidFlock>();
    private Transform centroEnjambreTarget;

    void Awake()
    {
        CargarRecursosYTexturas();
        ConfigurarMaterialFisicoRebote();
    }

    void Start()
    {
        var cam = Camera.main;
        if (cam != null) jugador = cam.transform;

        if (probarDirecto)
        {
            IniciarContemplacionEscultura();
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        // Atajo en Unity Editor: presionar tecla '4' o 'E' para probar la escultura y el gran estallido épico
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.digit4Key.wasPressedThisFrame)
            {
                if (jugador != null)
                {
                    var cc = jugador.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    jugador.position = new Vector3(0f, 0f, -42.6f);
                    if (cc != null) cc.enabled = true;
                }
                IniciarContemplacionEscultura();
            }
        }
#endif

        if (!EsculturaActiva || EstallidoOcurrido) return;

        AnimarContemplacion();
        VerificarGestoPaz();
    }

    private void CargarRecursosYTexturas()
    {
        clipEstallido = Resources.Load<AudioClip>("Audio/cubo_disolver_estallido");
        clipCrack = Resources.Load<AudioClip>("Audio/soga_corte_snap");
        clipResonancia = Resources.Load<AudioClip>("Audio/ojo_resonancia_loop");

        // 1. Cargar el Material y Textura Rocosa Real de Grete Stern
        materialRocaReal = Resources.Load<Material>("Mat_CuevaRoca");
        Texture2D texRoca = Resources.Load<Texture2D>("TexturaRocaGrete");

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        if (materialRocaReal == null)
        {
            materialRocaReal = new Material(litShader);
            if (texRoca != null)
            {
                if (materialRocaReal.HasProperty("_BaseMap")) materialRocaReal.SetTexture("_BaseMap", texRoca);
                else materialRocaReal.mainTexture = texRoca;
            }
            else
            {
                Color cPiedra = new Color(0.48f, 0.46f, 0.44f);
                if (materialRocaReal.HasProperty("_BaseColor")) materialRocaReal.SetColor("_BaseColor", cPiedra);
                else materialRocaReal.color = cPiedra;
            }
        }

        // Material para el modelo 3D de la mujer con la roca
        Texture2D texModelo = Resources.Load<Texture2D>("EsculturaGrete3D");
        if (litShader != null)
        {
            materialEscultura = new Material(litShader);
            if (texModelo != null)
            {
                if (materialEscultura.HasProperty("_BaseMap")) materialEscultura.SetTexture("_BaseMap", texModelo);
                else materialEscultura.mainTexture = texModelo;
            }
            else
            {
                materialEscultura = new Material(materialRocaReal);
            }
        }

        // Material translúcido para el polvo mineral (tonalidad ceniza cálida, sin tonos azules ni turquesas)
        Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        materialHumoPolvo = (unlitShader != null) ? new Material(unlitShader) : null;
    }

    private void ConfigurarMaterialFisicoRebote()
    {
        materialFisicoRebote = new PhysicsMaterial("MatRebotePiedraGrete")
        {
            bounciness = bouncinessRebotes,
            bounceCombine = PhysicsMaterialCombine.Maximum,
            dynamicFriction = 0.22f,
            staticFriction = 0.30f,
            frictionCombine = PhysicsMaterialCombine.Multiply
        };
    }

    public void IniciarContemplacionEscultura()
    {
        if (EsculturaActiva) return;
        EsculturaActiva = true;
        EstallidoOcurrido = false;

        Debug.Log("[EsculturaExplosivaFlocking] Contemplación de Escultura 3D iniciada (Orientada de frente con textura de roca real)");

        var cam = Camera.main;
        if (cam != null) jugador = cam.transform;

        Vector3 posBase = (jugador != null) ? jugador.position : new Vector3(0f, 0f, -42.6f);
        Vector3 posEscultura = posBase + Vector3.forward * posicionRelativa.z + Vector3.up * posicionRelativa.y;

        // Quitar la imagen gigante de Grete con la roca para contemplar únicamente la escultura 3D monumental
        var fm = GameObject.Find("FondoMontana");
        if (fm != null) fm.SetActive(false);

        GarantizarSueloFisico(posBase);

        // 1. Contenedor principal ORIENTADO EXACTAMENTE DE FRENTE HACIA EL JUGADOR
        goEsculturaRoot = new GameObject("EsculturaGrete3D_Root");
        goEsculturaRoot.transform.position = posEscultura;

        Vector3 dirHaciaJugador = (posBase - posEscultura);
        dirHaciaJugador.y = 0f;
        if (dirHaciaJugador.sqrMagnitude < 0.01f) dirHaciaJugador = -Vector3.forward;
        dirHaciaJugador.Normalize();
        goEsculturaRoot.transform.rotation = Quaternion.LookRotation(dirHaciaJugador, Vector3.up);

        // 2. Pedestal amplio con textura rocosa
        GameObject goPedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        goPedestal.name = "PedestalEscultura";
        goPedestal.transform.SetParent(goEsculturaRoot.transform, false);
        goPedestal.transform.localPosition = new Vector3(0f, -0.65f, 0f);
        goPedestal.transform.localScale = new Vector3(3.8f, 0.45f, 3.8f);
        if (materialRocaReal != null) goPedestal.GetComponent<Renderer>().material = materialRocaReal;

        // 3. Cargar e instanciar el modelo 3D Meshy AI ("EsculturaGrete3D")
        GameObject prefab3D = Resources.Load<GameObject>("EsculturaGrete3D");
        if (prefab3D != null)
        {
            goModelo3D = Instantiate(prefab3D, goEsculturaRoot.transform, false);
            goModelo3D.name = "ModeloMeshy_EsculturaGrete";
            goModelo3D.transform.localPosition = Vector3.zero;
            goModelo3D.transform.localRotation = Quaternion.Euler(0f, rotacionYModelo, 0f);

            foreach (var col in goModelo3D.GetComponentsInChildren<Collider>()) Destroy(col);
            if (materialEscultura != null)
            {
                foreach (var rend in goModelo3D.GetComponentsInChildren<Renderer>())
                {
                    rend.material = materialEscultura;
                }
            }

            NormalizarProporcionesUniforme(goModelo3D, alturaEsculturaDeseada);
        }
        else
        {
            goModelo3D = ConstruirEsculturaProcedural(goEsculturaRoot.transform);
        }

        // 4. Iluminación cenital teatral cálida y natural (sin tinte turquesa)
        GameObject goLuz = new GameObject("AuraEscultura");
        goLuz.transform.SetParent(goEsculturaRoot.transform, false);
        goLuz.transform.localPosition = new Vector3(0f, 3.2f, 0.4f);
        luzAura = goLuz.AddComponent<Light>();
        luzAura.type = LightType.Point;
        luzAura.range = 20f;
        luzAura.intensity = 3.8f;
        luzAura.color = new Color(1.0f, 0.96f, 0.88f); // Luz cálida dorada teatral

        // 5. Audio ambiental de resonancia envolvente
        audioSource = goEsculturaRoot.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.85f;
        if (clipResonancia != null)
        {
            audioSource.clip = clipResonancia;
            audioSource.loop = true;
            audioSource.volume = 0.7f;
            audioSource.Play();
        }

        // 6. Target dinámico para la órbita envolvente del Flocking
        GameObject goTarget = new GameObject("FlockingOrbitTarget");
        goTarget.transform.position = posBase + Vector3.up * 2.6f;
        centroEnjambreTarget = goTarget.transform;
    }

    private void NormalizarProporcionesUniforme(GameObject modelo, float alturaDeseada)
    {
        Renderer[] rends = modelo.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        float alturaOriginal = b.size.y;
        if (alturaOriginal > 0.05f)
        {
            float factor = alturaDeseada / alturaOriginal;
            modelo.transform.localScale = Vector3.one * factor;

            float centroYOffset = b.center.y - modelo.transform.position.y;
            modelo.transform.localPosition = new Vector3(0f, -centroYOffset * factor + (alturaDeseada * 0.45f), 0f);
        }
    }

    private void GarantizarSueloFisico(Vector3 posJugador)
    {
        GameObject goSuelo = GameObject.Find("SueloFisicoRebotes");
        if (goSuelo == null)
        {
            goSuelo = new GameObject("SueloFisicoRebotes");
            goSuelo.transform.position = new Vector3(posJugador.x, -1.25f, posJugador.z + 5f);
            var box = goSuelo.AddComponent<BoxCollider>();
            box.size = new Vector3(180f, 0.5f, 180f);
            box.material = materialFisicoRebote;
        }
    }

    private GameObject ConstruirEsculturaProcedural(Transform parent)
    {
        GameObject go = new GameObject("EsculturaGrete_Procedural");
        go.transform.SetParent(parent, false);

        GameObject goRoca = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goRoca.name = "RocaSifisoEscultura";
        goRoca.transform.SetParent(go.transform, false);
        goRoca.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        goRoca.transform.localScale = new Vector3(1.7f, 1.6f, 1.5f);
        Destroy(goRoca.GetComponent<Collider>());
        if (materialRocaReal != null) goRoca.GetComponent<Renderer>().material = materialRocaReal;

        GameObject goTorso = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        goTorso.name = "SiluetaMujerEscultura";
        goTorso.transform.SetParent(go.transform, false);
        goTorso.transform.localPosition = new Vector3(0.5f, 0.7f, -0.3f);
        goTorso.transform.localRotation = Quaternion.Euler(25f, 0f, 15f);
        goTorso.transform.localScale = new Vector3(0.45f, 0.75f, 0.45f);
        Destroy(goTorso.GetComponent<Collider>());
        if (materialRocaReal != null) goTorso.GetComponent<Renderer>().material = materialRocaReal;

        return go;
    }

    private void AnimarContemplacion()
    {
        if (goEsculturaRoot != null)
        {
            float levitacion = Mathf.Sin(Time.time * 1.6f) * 0.035f;
            goEsculturaRoot.transform.position += new Vector3(0f, levitacion * Time.deltaTime, 0f);

            if (luzAura != null)
            {
                luzAura.intensity = 2.6f + Mathf.Sin(Time.time * 2.5f) * 0.5f;
            }
        }
    }

    private void VerificarGestoPaz()
    {
        bool gestoPaz = VRHandVisual.EsSimboloDePaz || OSCHandReceiver.IsPeaceSign;

#if UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.vKey.isPressed || kb.pKey.isPressed || kb.spaceKey.isPressed) gestoPaz = true;
        }
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        {
            gestoPaz = true;
        }
#endif

        if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed)
        {
            gestoPaz = true;
        }

        if (gestoPaz)
        {
            StartCoroutine(SecuenciaEstallidoEpico());
        }
    }

    private IEnumerator SecuenciaEstallidoEpico()
    {
        EstallidoOcurrido = true;
        Debug.Log("[EsculturaExplosivaFlocking] ¡Gesto de Paz! Iniciando gran estallido épico: 340 rocas al cielo y nube difuminada");

        Vector3 posOriginal = (goEsculturaRoot != null) ? goEsculturaRoot.transform.position : Vector3.zero;

        // 1. FASE DE CARGA SÍSMICA Y CRUJIDO PREVIO (1.8s de tensión cinematográfica creciente)
        if (audioSource != null)
        {
            audioSource.pitch = 1.25f;
            if (clipCrack != null) audioSource.PlayOneShot(clipCrack, 0.95f);
        }

        float tCarga = 0f;
        float durCarga = 1.8f;
        while (tCarga < durCarga)
        {
            tCarga += Time.deltaTime;
            float p = tCarga / durCarga;
            float vibracion = Mathf.Pow(p, 2.4f) * 0.18f;

            if (goEsculturaRoot != null)
            {
                goEsculturaRoot.transform.position = posOriginal + Random.insideUnitSphere * vibracion;
            }
            if (luzAura != null)
            {
                luzAura.intensity = Mathf.Lerp(2.8f, 16.0f, p);
                luzAura.color = Color.Lerp(new Color(1.0f, 0.96f, 0.88f), Color.white, p);
            }
            yield return null;
        }

        Vector3 centroDetonacion = posOriginal + Vector3.up * 1.1f;

        // 2. DETONACIÓN MASIVA:
        if (goModelo3D != null) goModelo3D.SetActive(false);
        if (goEsculturaRoot != null)
        {
            foreach (var r in goEsculturaRoot.GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        // Audio masivo en capas
        if (audioSource != null)
        {
            audioSource.Stop();
            if (clipEstallido != null) audioSource.PlayOneShot(clipEstallido, 1.0f);
            if (clipCrack != null) audioSource.PlayOneShot(clipCrack, 1.0f);
        }

        // Temblor sísmico enérgico de cámara
        StartCoroutine(CorrutinaTemblorCamara(0.95f, 0.25f));

        // NUBE VOLUMÉTRICA DIFUSA Y ASIMÉTRICA (Sin forma circular, desdibujada y orgánica)
        GenerarNubePolvoDifuminadaOrganica(centroDetonacion);

        // Destello lumínico cálido expansivo
        if (luzAura != null)
        {
            luzAura.range = 50f;
            luzAura.intensity = 20f;
            luzAura.color = new Color(1.0f, 0.98f, 0.92f);
            StartCoroutine(DisiparLuzAura());
        }

        // 3. FÍSICA BALÍSTICA ÉPICA: 340 ROCAS TEXTURIZADAS LANZADAS AL CIELO EN ARCOS VISIBLES
        GenerarRocasHaciaElCielo(centroDetonacion);

        // 4. SISTEMA COMPLEJO: 36 ESQUIRLAS DE ROCA VOLADORAS CON FLOCKING REYNOLDS (SIN ESTELAS BLANCAS)
        GenerarFlockingRocasVoladoras(centroDetonacion);
        StartCoroutine(ControlarOrbitaFlocking());

        // Caída paulatina de todo el enjambre: tras contemplar el vuelo flocking, todas las rocas van cayendo en cascada con física y rebotes
        StartCoroutine(CascadaCaidaPaulatinaBoids(3.8f));

        // 5. Duración de contemplación completa (11.5 segundos): las rocas caen, rebotan y quedan caídas asentadas en la superficie
        yield return new WaitForSeconds(11.5f);

        // Al culminar la contemplación del estallido y las rocas caídas en la superficie,
        // silenciamos cualquier audio residual antes de pasar a la música del Nivel 3
        AudioSource[] audiosViejos = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var asrc in audiosViejos)
        {
            if (asrc != null) asrc.Stop();
        }

        Debug.Log("[EsculturaExplosivaFlocking] Estallido culminado con rocas asentadas en la superficie. Activando Nivel 3 (Puerta y Llave Gigante)");
        var n3 = FindAnyObjectByType<Nivel3_PuertaLlave>();
        if (n3 == null)
        {
            var goN3 = new GameObject("ControladorNivel3_PuertaLlave");
            n3 = goN3.AddComponent<Nivel3_PuertaLlave>();
        }
        n3.IniciarNivel3();
    }

    /// <summary>
    /// Lanza 340 rocas texturizadas directamente hacia el cielo en arcos parabólicos calculados
    /// para que asciendan ante la vista del jugador, alcancen el cenit y caigan al suelo rebotando con retumbos.
    /// </summary>
    private void GenerarRocasHaciaElCielo(Vector3 centro)
    {
        Debug.Log($"[Física Épica] Lanzando {totalFragmentosRoca} rocas texturizadas AL CIELO en arcos visibles ante el espectador");

        for (int i = 0; i < totalFragmentosRoca; i++)
        {
            float r = Random.value;
            Vector3 dim;
            float masa;
            float bounciness;
            float vyMin, vyMax;
            float vhMin, vhMax;

            if (r < 0.12f)
            {
                // Gran Peñasco Pesado (arcos de 7 a 13m de altura, caída contundente)
                float s = Random.Range(0.55f, 0.95f);
                dim = new Vector3(s * Random.Range(0.85f, 1.25f), s * Random.Range(0.75f, 1.15f), s * Random.Range(0.85f, 1.25f));
                masa = Random.Range(18f, 35f);
                bounciness = 0.65f;
                vyMin = 11.0f; vyMax = 17.0f;
                vhMin = 1.5f;  vhMax = 4.5f;
            }
            else if (r < 0.48f)
            {
                // Roca Mediana (arcos de 10 a 18m de altura, alta visibilidad)
                float s = Random.Range(0.22f, 0.50f);
                dim = new Vector3(s * Random.Range(0.80f, 1.30f), s * Random.Range(0.70f, 1.20f), s * Random.Range(0.80f, 1.30f));
                masa = Random.Range(3.5f, 8.5f);
                bounciness = bouncinessRebotes;
                vyMin = 14.0f; vyMax = 22.0f;
                vhMin = 2.0f;  vhMax = 6.0f;
            }
            else
            {
                // Gravilla y Astillas (arcos altos de 14 a 25m, lluvia densa de cascotes)
                float s = Random.Range(0.06f, 0.18f);
                dim = new Vector3(s * Random.Range(0.75f, 1.35f), s * Random.Range(0.70f, 1.35f), s * Random.Range(0.75f, 1.35f));
                masa = Random.Range(0.5f, 1.8f);
                bounciness = 0.90f;
                vyMin = 16.0f; vyMax = 26.0f;
                vhMin = 2.5f;  vhMax = 7.5f;
            }

            CrearRocaBalistica(centro, dim, masa, bounciness, vyMin, vyMax, vhMin, vhMax, i);
        }
    }

    private void CrearRocaBalistica(Vector3 centro, Vector3 dimensiones, float masa, float bounciness, float vyMin, float vyMax, float vhMin, float vhMax, int semilla)
    {
        GameObject roca = new GameObject($"RocaGrete_{semilla}");
        rocasBalisticas.Add(roca);

        Vector3 offset = Random.insideUnitSphere * 1.1f;
        roca.transform.position = centro + offset;
        roca.transform.rotation = Random.rotation;

        MeshFilter mf = roca.AddComponent<MeshFilter>();
        mf.sharedMesh = GenerarMallaRocaFacetada(dimensiones, semilla);

        MeshRenderer mr = roca.AddComponent<MeshRenderer>();
        if (materialRocaReal != null) mr.material = materialRocaReal;

        BoxCollider boxCol = roca.AddComponent<BoxCollider>();
        boxCol.size = dimensiones * 1.85f;
        boxCol.material = materialFisicoRebote;

        Rigidbody rb = roca.AddComponent<Rigidbody>();
        rb.mass = masa;
        rb.linearDamping = 0.03f;
        rb.angularDamping = 0.15f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Trayectoria balística calculada: lanzamiento hacia arriba en cono visible
        float angulo = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float vHorizontal = Random.Range(vhMin, vhMax);
        float vx = Mathf.Cos(angulo) * vHorizontal;
        float vz = Mathf.Sin(angulo) * vHorizontal;
        float vy = Random.Range(vyMin, vyMax);

        rb.linearVelocity = new Vector3(vx, vy, vz);
        rb.angularVelocity = Random.insideUnitSphere * Random.Range(6f, 20f);

        var detector = roca.AddComponent<FragmentoFisicoRebote>();
        detector.Inicializar(clipCrack, clipEstallido, masa);
    }

    private Mesh GenerarMallaRocaFacetada(Vector3 dimension, int semilla)
    {
        Random.InitState(semilla);
        Mesh m = new Mesh();
        m.name = $"MallaRocaReal_{semilla}";

        Vector3[] vertsBase = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            float rx = ((i & 1) == 0 ? -1f : 1f) * dimension.x * Random.Range(0.60f, 1.45f);
            float ry = ((i & 2) == 0 ? -1f : 1f) * dimension.y * Random.Range(0.55f, 1.50f);
            float rz = ((i & 4) == 0 ? -1f : 1f) * dimension.z * Random.Range(0.60f, 1.45f);
            vertsBase[i] = new Vector3(rx, ry, rz);
        }

        int[] indicesBase = new int[]
        {
            0, 2, 3,  0, 3, 1, // Frente
            5, 7, 6,  5, 6, 4, // Atrás
            4, 6, 2,  4, 2, 0, // Izquierda
            1, 3, 7,  1, 7, 5, // Derecha
            2, 6, 7,  2, 7, 3, // Arriba
            4, 0, 1,  4, 1, 5  // Abajo
        };

        Vector3[] flatVerts = new Vector3[36];
        Vector2[] uvs = new Vector2[36];
        int[] tris = new int[36];

        for (int i = 0; i < 36; i++)
        {
            flatVerts[i] = vertsBase[indicesBase[i]];
            tris[i] = i;
            uvs[i] = new Vector2((flatVerts[i].x + dimension.x) / (dimension.x * 2.2f), (flatVerts[i].y + dimension.y) / (dimension.y * 2.2f));
        }

        m.vertices = flatVerts;
        m.triangles = tris;
        m.uv = uvs;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>
    /// <summary>
    /// Genera una nube de polvo mineral asimétrica, difuminada y orgánica:
    /// Utiliza mallas poliédricas deformadas (cero esferas ni círculos) y un desvanecimiento
    /// exponencial rápido para que el polvo se evapore limpiamente sin dejar ningún rastro circular.
    /// </summary>
    private void GenerarNubePolvoDifuminadaOrganica(Vector3 centro)
    {
        int cantidadPuffs = 44;

        for (int i = 0; i < cantidadPuffs; i++)
        {
            GameObject goPuff = new GameObject($"PuffPolvoOrganico_{i}");

            // Malla de nube asimétrica irregular con facetas suaves (cero círculos ni esferas primitivas)
            MeshFilter mf = goPuff.AddComponent<MeshFilter>();
            mf.sharedMesh = GenerarMallaNubeAsimetrica(700 + i);

            MeshRenderer mr = goPuff.AddComponent<MeshRenderer>();

            Vector3 posInicial;
            Vector3 dirDeriva;
            Vector3 escalaInicial;
            Vector3 escalaFinal;
            // Desaparece antes para no dejar formas residuales (2.0s a 3.4s)
            float duracionPuff = Random.Range(2.0f, 3.4f);

            if (i < 16)
            {
                // CAPA 1: Manto Terrestre Rastrero (polvo aplastado y rasante que se escurre por el suelo)
                posInicial = new Vector3(centro.x + Random.Range(-2.2f, 2.2f), -1.15f, centro.z + Random.Range(-2.2f, 2.2f));
                float s = Random.Range(1.8f, 3.2f);
                escalaInicial = new Vector3(s * 1.6f, s * 0.45f, s * 1.6f);
                float sFin = Random.Range(6.5f, 11.0f);
                escalaFinal = new Vector3(sFin * 1.8f, sFin * 0.40f, sFin * 1.8f);
                dirDeriva = new Vector3(Random.Range(-3.0f, 3.0f), Random.Range(0.2f, 0.5f), Random.Range(-3.0f, 3.0f));
            }
            else if (i < 32)
            {
                // CAPA 2: Penacho Ascendente Desgarrado (columna asimétrica arrastrada por el viento de montaña)
                posInicial = centro + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.2f, 2.0f), Random.Range(-1.2f, 1.2f));
                float s = Random.Range(1.2f, 2.2f);
                escalaInicial = new Vector3(s * 0.85f, s * 1.5f, s * 0.85f);
                float sFin = Random.Range(6.0f, 10.0f);
                escalaFinal = new Vector3(sFin * 0.90f, sFin * 1.5f, sFin * 0.90f);
                dirDeriva = new Vector3(Random.Range(1.5f, 3.8f), Random.Range(2.5f, 4.5f), Random.Range(-0.8f, 1.8f));
            }
            else
            {
                // CAPA 3: Bocanadas Difusas Dispersas (manchas vaporosas que flotan y se difuminan en el aire)
                posInicial = centro + Random.insideUnitSphere * Random.Range(1.2f, 3.0f);
                float s = Random.Range(1.0f, 2.5f);
                escalaInicial = new Vector3(s * Random.Range(0.8f, 1.4f), s * Random.Range(0.8f, 1.4f), s * Random.Range(0.8f, 1.4f));
                float sFin = Random.Range(5.5f, 9.0f);
                escalaFinal = new Vector3(sFin * Random.Range(0.8f, 1.5f), sFin * Random.Range(0.7f, 1.3f), sFin * Random.Range(0.8f, 1.5f));
                dirDeriva = Random.onUnitSphere * Random.Range(1.5f, 3.2f) + Vector3.up * 1.2f;
            }

            goPuff.transform.position = posInicial;
            goPuff.transform.localScale = escalaInicial;
            goPuff.transform.rotation = Random.rotation;

            Material matIndividual = (materialHumoPolvo != null) ? new Material(materialHumoPolvo) : null;
            if (matIndividual != null)
            {
                Color colHumo = new Color(0.66f, 0.63f, 0.58f, 0.85f);
                matIndividual.color = colHumo;
                mr.material = matIndividual;
            }

            StartCoroutine(AnimarBocanadaPolvoIndividual(goPuff, matIndividual, escalaInicial, escalaFinal, dirDeriva, duracionPuff));
        }
    }

    /// <summary>
    /// Genera una malla de nube esponjosa con curvas orgánicas continuas y suaves (Icosfera subdividida):
    /// Cero vértices punzantes ni caras triangulares duras. Las normales compartidas
    /// calculan un sombreado suave continuo (Gouraud) que asemeja bocanadas algodonosas de humo real.
    /// </summary>
    private Mesh GenerarMallaNubeAsimetrica(int semilla)
    {
        Random.InitState(semilla);
        Mesh m = new Mesh();
        m.name = $"MallaHumoSuave_{semilla}";

        // 1. Vértices base de un icosaedro regular (12 vértices con curvatura simétrica esférica)
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        List<Vector3> vertsBase = new List<Vector3>()
        {
            new Vector3(-1f,  t,  0f).normalized,
            new Vector3( 1f,  t,  0f).normalized,
            new Vector3(-1f, -t,  0f).normalized,
            new Vector3( 1f, -t,  0f).normalized,

            new Vector3( 0f, -1f,  t).normalized,
            new Vector3( 0f,  1f,  t).normalized,
            new Vector3( 0f, -1f, -t).normalized,
            new Vector3( 0f,  1f, -t).normalized,

            new Vector3( t,  0f, -1f).normalized,
            new Vector3( t,  0f,  1f).normalized,
            new Vector3(-t,  0f, -1f).normalized,
            new Vector3(-t,  0f,  1f).normalized
        };

        // 20 caras triangulares base
        int[] trisBase = new int[]
        {
            0, 11, 5,   0, 5, 1,   0, 1, 7,   0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,  11, 10, 2, 10, 7, 6,    7, 1, 8,
            3, 9, 4,    3, 4, 2,   3, 2, 6,   3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,  6, 2, 10,  8, 6, 7,    9, 8, 1
        };

        // 2. Subdivisión geodésica para lograr 42 vértices suaves y 80 caras con curvatura completamente continua
        Dictionary<long, int> puntoMedioCache = new Dictionary<long, int>();
        int ObtenerPuntoMedio(int p1, int p2)
        {
            long menor = Mathf.Min(p1, p2);
            long mayor = Mathf.Max(p1, p2);
            long clave = (menor << 32) + mayor;

            if (puntoMedioCache.TryGetValue(clave, out int ret)) return ret;

            Vector3 pm = ((vertsBase[p1] + vertsBase[p2]) * 0.5f).normalized;
            vertsBase.Add(pm);
            int indice = vertsBase.Count - 1;
            puntoMedioCache.Add(clave, indice);
            return indice;
        }

        List<int> trisSub = new List<int>();
        for (int i = 0; i < trisBase.Length; i += 3)
        {
            int v1 = trisBase[i];
            int v2 = trisBase[i + 1];
            int v3 = trisBase[i + 2];

            int a = ObtenerPuntoMedio(v1, v2);
            int b = ObtenerPuntoMedio(v2, v3);
            int c = ObtenerPuntoMedio(v3, v1);

            trisSub.Add(v1); trisSub.Add(a); trisSub.Add(c);
            trisSub.Add(v2); trisSub.Add(b); trisSub.Add(a);
            trisSub.Add(v3); trisSub.Add(c); trisSub.Add(b);
            trisSub.Add(a);  trisSub.Add(b); trisSub.Add(c);
        }

        // 3. Modulación armónica suave (ondulaciones algodonosas suaves, CERO picos punzantes ni aristas triangulares)
        float semillaOffset = semilla * 0.19f;
        float stretchX = Random.Range(1.10f, 1.40f);
        float stretchY = Random.Range(0.80f, 1.15f);
        float stretchZ = Random.Range(1.05f, 1.35f);

        Vector3[] vertsFinales = new Vector3[vertsBase.Count];
        Vector2[] uvs = new Vector2[vertsBase.Count];

        for (int i = 0; i < vertsBase.Count; i++)
        {
            Vector3 v = vertsBase[i];

            // Ondulaciones senoidales suaves y de baja frecuencia (curvas redondeadas como volutas de humo)
            float onda1 = Mathf.Sin(v.x * 2.2f + v.y * 1.6f + semillaOffset) * 0.12f;
            float onda2 = Mathf.Cos(v.z * 2.0f + v.x * 1.4f) * 0.09f;
            float radioSuave = 1.0f + onda1 + onda2;

            vertsFinales[i] = new Vector3(v.x * radioSuave * stretchX, v.y * radioSuave * stretchY, v.z * radioSuave * stretchZ);
            uvs[i] = new Vector2((v.x + 1f) * 0.5f, (v.y + 1f) * 0.5f);
        }

        m.vertices = vertsFinales;
        m.triangles = trisSub.ToArray();
        m.uv = uvs;

        // Normales compartidas continuas que garantizan curvas perfectamente redondeadas y difusas
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private IEnumerator AnimarBocanadaPolvoIndividual(GameObject goPuff, Material mat, Vector3 escalaIni, Vector3 escalaFin, Vector3 dirDeriva, float duracion)
    {
        float t = 0f;
        Vector3 rotAxis = Random.onUnitSphere;
        float rotSpeed = Random.Range(15f, 45f);

        while (t < duracion)
        {
            if (goPuff == null) yield break;

            t += Time.deltaTime;
            float p = t / duracion;

            // Expansión asimétrica no lineal
            goPuff.transform.localScale = Vector3.Lerp(escalaIni, escalaFin, Mathf.Sqrt(p));

            // Deriva orgánica y rotación continua para desdibujar bordes
            goPuff.transform.position += dirDeriva * Time.deltaTime;
            goPuff.transform.Rotate(rotAxis * (rotSpeed * Time.deltaTime), Space.Self);

            // Desvanecimiento exponencial rápido (se evapora antes para que no queden círculos residuales)
            if (mat != null)
            {
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - p), 2.5f) * 0.85f;
                mat.color = new Color(0.66f, 0.63f, 0.58f, alpha);

                if (alpha < 0.02f)
                {
                    Destroy(goPuff);
                    yield break;
                }
            }

            yield return null;
        }

        if (goPuff != null) Destroy(goPuff);
    }

    private void GenerarFlockingRocasVoladoras(Vector3 centro)
    {
        Debug.Log($"[Sistemas Complejos] Liberando {cantidadBoids} esquirlas de roca voladoras puras con Flocking de Reynolds (sin estelas)");

        for (int i = 0; i < cantidadBoids; i++)
        {
            GameObject goBoid = new GameObject($"BoidRoca_Reynolds_{i}");

            MeshFilter mf = goBoid.AddComponent<MeshFilter>();
            mf.sharedMesh = GenerarMallaRocaFacetada(new Vector3(0.18f, 0.16f, 0.20f), 600 + i);

            MeshRenderer mr = goBoid.AddComponent<MeshRenderer>();
            if (materialRocaReal != null) mr.material = materialRocaReal;

            goBoid.transform.position = centro + Random.insideUnitSphere * 0.9f;
            goBoid.transform.localScale = Vector3.one * Random.Range(0.85f, 1.45f);

            BoidFlock boid = goBoid.AddComponent<BoidFlock>();
            boid.Inicializar(this, Random.onUnitSphere * Random.Range(5.5f, 11.0f));
            enjambreBoids.Add(boid);
        }
    }

    private IEnumerator CorrutinaTemblorCamara(float duracion, float intensidad)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        Transform camT = cam.transform;
        Vector3 posOrig = camT.localPosition;

        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = 1f - (t / duracion);
            float offsetX = (Mathf.PerlinNoise(Time.time * 40f, 0f) - 0.5f) * intensidad * p;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * 40f) - 0.5f) * intensidad * p;
            camT.localPosition = posOrig + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }
        camT.localPosition = posOrig;
    }

    private IEnumerator ControlarOrbitaFlocking()
    {
        float angulo = 0f;
        float radioOrbita = 5.2f;
        Vector3 posBase = (jugador != null) ? jugador.position : new Vector3(0f, 0f, -42.6f);

        float tiempo = 0f;
        while (tiempo < 11.5f)
        {
            tiempo += Time.deltaTime;
            angulo += Time.deltaTime * 1.4f;

            float x = Mathf.Cos(angulo) * radioOrbita;
            float z = Mathf.Sin(angulo) * radioOrbita;
            // Los primeros 3.8s asciende y muestra el enjambre girando en el cielo;
            // luego la órbita desciende suavemente acompañando la lluvia paulatina de rocas
            float y = (tiempo < 3.8f)
                ? Mathf.Lerp(2.2f, 5.8f, tiempo / 3.8f)
                : Mathf.Lerp(5.8f, 1.8f, (tiempo - 3.8f) / 7.0f);

            if (centroEnjambreTarget != null)
            {
                centroEnjambreTarget.position = posBase + new Vector3(x, y, z);
            }

            yield return null;
        }
    }

    /// <summary>
    /// Cascada de caída paulatina: las rocas del enjambre van desprendiéndose progresivamente
    /// del vuelo aéreo para precipitarse al suelo bajo la fuerza de la gravedad,
    /// colisionar contra el terreno y rebotar con sonido de rocas.
    /// </summary>
    private IEnumerator CascadaCaidaPaulatinaBoids(float retardoInicial)
    {
        yield return new WaitForSeconds(retardoInicial);

        Debug.Log("[Flocking] Iniciando lluvia paulatina de rocas del enjambre hacia el suelo con gravedad y rebotes...");

        // Copiamos la lista de boids y la desordenamos aleatoriamente para una caída natural y orgánica
        List<BoidFlock> boidsParaCaer = new List<BoidFlock>(enjambreBoids);
        for (int i = 0; i < boidsParaCaer.Count; i++)
        {
            int rnd = Random.Range(i, boidsParaCaer.Count);
            var temp = boidsParaCaer[i];
            boidsParaCaer[i] = boidsParaCaer[rnd];
            boidsParaCaer[rnd] = temp;
        }

        // Dejar caer de a 1 o 2 boids en pequeños intervalos (cascada continua)
        for (int i = 0; i < boidsParaCaer.Count; i++)
        {
            if (boidsParaCaer[i] != null && boidsParaCaer[i].EnVuelo)
            {
                boidsParaCaer[i].ActivarCaidaFisica(materialFisicoRebote, clipCrack, clipEstallido);
            }

            // Cada 1 o 2 rocas liberadas, hacemos una pausa orgánica escalonada
            if (i % 2 == 1 || Random.value < 0.35f)
            {
                yield return new WaitForSeconds(Random.Range(0.12f, 0.22f));
            }
        }

        Debug.Log("[Flocking] ¡Todas las rocas del enjambre han caído hacia el terreno y rebotado!");
    }

    private IEnumerator DisiparLuzAura()
    {
        float t = 0f;
        while (t < 2.5f)
        {
            t += Time.deltaTime;
            if (luzAura != null)
            {
                luzAura.intensity = Mathf.Lerp(20f, 0f, t / 2.5f);
            }
            yield return null;
        }
    }

    public List<BoidFlock> ObtenerVecinos() => enjambreBoids;
    public Transform ObtenerTargetFlock() => centroEnjambreTarget;
}

/// <summary>
/// Agente individual del Sistema Complejo de Flocking implementando el algoritmo de Craig Reynolds (1987).
/// Esquirlas de roca voladoras que se orientan y giran de forma natural en el aire,
/// y que posteriormente reciben gravedad y física de rebotes paulatina para caer al suelo.
/// </summary>
public class BoidFlock : MonoBehaviour
{
    private EsculturaExplosivaFlocking controlador;
    private Vector3 velocidad;
    private Vector3 ejeRotacion;
    private float velocidadRotacion;
    private bool enVueloFlocking = true;

    public bool EnVuelo => enVueloFlocking;

    public void Inicializar(EsculturaExplosivaFlocking ctrl, Vector3 velInicial)
    {
        controlador = ctrl;
        velocidad = velInicial;
        ejeRotacion = Random.onUnitSphere;
        velocidadRotacion = Random.Range(180f, 380f);
        enVueloFlocking = true;
    }

    void Update()
    {
        if (!enVueloFlocking || controlador == null) return;

        Vector3 fuerzaFlocking = CalcularFuerzasReynolds();
        velocidad += fuerzaFlocking * Time.deltaTime;
        velocidad = Vector3.ClampMagnitude(velocidad, controlador.velocidadMaxBoids);

        transform.position += velocidad * Time.deltaTime;
        transform.Rotate(ejeRotacion * (velocidadRotacion * Time.deltaTime), Space.Self);
    }

    /// <summary>
    /// Transición física paulatina: el boid abandona el enjambre aéreo,
    /// se activa PhysX con gravedad newtoniana, colisión contra el terreno
    /// y detección de rebotes con retumbos de roca.
    /// </summary>
    public void ActivarCaidaFisica(PhysicsMaterial matRebote, AudioClip crack, AudioClip boom)
    {
        if (!enVueloFlocking) return;
        enVueloFlocking = false;

        // 1. Collider de impacto físico
        BoxCollider boxCol = gameObject.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(0.35f, 0.35f, 0.35f);
        boxCol.material = matRebote;

        // 2. Rigidbody con gravedad activada
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        float masa = Random.Range(2.0f, 5.5f);
        rb.mass = masa;
        rb.useGravity = true;
        rb.linearDamping = 0.04f;
        rb.angularDamping = 0.18f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Transición de velocidad: conserva parte del impulso horizontal de vuelo
        // y adquiere un empuje vertical descendente para precipitarse al suelo
        Vector3 impulsoAbajo = Vector3.down * Random.Range(2.5f, 6.0f);
        Vector3 dispersion = Random.insideUnitSphere * 1.4f;
        rb.linearVelocity = (velocidad * 0.65f) + impulsoAbajo + dispersion;
        rb.angularVelocity = Random.insideUnitSphere * Random.Range(8f, 22f);

        // 3. Audio de rebotes de roca
        var detector = gameObject.AddComponent<FragmentoFisicoRebote>();
        detector.Inicializar(crack, boom, masa);
    }

    private Vector3 CalcularFuerzasReynolds()
    {
        Vector3 separacion = Vector3.zero;
        Vector3 alineacion = Vector3.zero;
        Vector3 cohesion = Vector3.zero;

        int conteoSeparacion = 0;
        int conteoVecinos = 0;
        Vector3 centroMasa = Vector3.zero;

        var vecinos = controlador.ObtenerVecinos();
        for (int i = 0; i < vecinos.Count; i++)
        {
            BoidFlock otro = vecinos[i];
            if (otro == null || otro == this || !otro.enVueloFlocking) continue;

            float dist = Vector3.Distance(transform.position, otro.transform.position);

            // 1. REGLA 1: SEPARACIÓN (Evitar colisión con vecinos inmediatos)
            if (dist > 0.001f && dist < controlador.radioSeparacion)
            {
                Vector3 huida = (transform.position - otro.transform.position).normalized / dist;
                separacion += huida;
                conteoSeparacion++;
            }

            // 2. REGLA 2 y 3: ALINEACIÓN Y COHESIÓN (con compañeros en radio sensorial)
            if (dist < controlador.radioVecinos)
            {
                alineacion += otro.velocidad;
                centroMasa += otro.transform.position;
                conteoVecinos++;
            }
        }

        Vector3 steerSeparacion = Vector3.zero;
        if (conteoSeparacion > 0)
        {
            separacion /= conteoSeparacion;
            steerSeparacion = Vector3.ClampMagnitude(separacion.normalized * controlador.velocidadMaxBoids - velocidad, controlador.fuerzaMaxBoids);
        }

        Vector3 steerAlineacion = Vector3.zero;
        Vector3 steerCohesion = Vector3.zero;
        if (conteoVecinos > 0)
        {
            alineacion /= conteoVecinos;
            steerAlineacion = Vector3.ClampMagnitude(alineacion.normalized * controlador.velocidadMaxBoids - velocidad, controlador.fuerzaMaxBoids);

            centroMasa /= conteoVecinos;
            Vector3 deseoCohesion = (centroMasa - transform.position).normalized * controlador.velocidadMaxBoids;
            steerCohesion = Vector3.ClampMagnitude(deseoCohesion - velocidad, controlador.fuerzaMaxBoids);
        }

        // Atracción hacia el vórtice dinámico que orbita en 3D
        Vector3 steerTarget = Vector3.zero;
        var target = controlador.ObtenerTargetFlock();
        if (target != null)
        {
            Vector3 haciaTarget = (target.position - transform.position).normalized * controlador.velocidadMaxBoids;
            steerTarget = Vector3.ClampMagnitude(haciaTarget - velocidad, controlador.fuerzaMaxBoids * 0.90f);
        }

        return (steerSeparacion * 2.1f) + (steerAlineacion * 1.15f) + (steerCohesion * 1.0f) + (steerTarget * 1.45f);
    }
}

/// <summary>
/// Componente físico para cada roca individual:
/// Registra las colisiones contra el terreno con PhysX y reproduce retumbos tronantes según la masa.
/// </summary>
public class FragmentoFisicoRebote : MonoBehaviour
{
    private AudioSource audioSource;
    private AudioClip clipCrack;
    private AudioClip clipBoom;
    private float masaRoca = 1f;
    private int rebotesRealizados = 0;
    private const int MAX_REBOTES_SONIDO = 6;

    public void Inicializar(AudioClip snap, AudioClip boom, float masa)
    {
        clipCrack = snap;
        clipBoom = boom;
        masaRoca = masa;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 2.5f;
        audioSource.maxDistance = 45.0f;
        audioSource.playOnAwake = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        float fuerzaImpacto = collision.relativeVelocity.magnitude;

        if (fuerzaImpacto > 0.6f && rebotesRealizados < MAX_REBOTES_SONIDO)
        {
            rebotesRealizados++;

            if (audioSource != null)
            {
                if (masaRoca > 8.0f)
                {
                    // Peñasco pesado: RETUMBO TRONANTE DE ROCA (Boom bajo + Snap seco)
                    audioSource.pitch = Random.Range(0.65f, 0.90f);
                    float vol = Mathf.Clamp01(fuerzaImpacto / 12.0f) * 0.95f;
                    if (clipBoom != null) audioSource.PlayOneShot(clipBoom, vol);
                    if (clipCrack != null) audioSource.PlayOneShot(clipCrack, vol * 0.8f);
                }
                else
                {
                    // Esquirla mediana o gravilla: Golpe seco de piedra
                    audioSource.pitch = Random.Range(1.1f, 2.3f);
                    float vol = Mathf.Clamp01(fuerzaImpacto / 10.0f) * 0.55f;
                    if (clipCrack != null) audioSource.PlayOneShot(clipCrack, vol);
                }
            }
        }
    }
}
