using System.Collections;
using UnityEngine;

public class Nivel3_PuertaLlave : MonoBehaviour
{
    [Header("Configuración y Pruebas")]
    [Tooltip("Marcar únicamente para probar directamente Nivel 3 en Play Mode sin tener que pasar por Nivel 1 y 2")]
    public bool probarNivel3Directo = false;

    [Header("Materiales Precompilados (Garantía Sin Rosa en Android)")]
    public Material materialFondoPuerta;
    public Material materialMaderaPuerta;
    public Material materialMarcoPuerta;
    public Material materialCamino;
    public Material materialTerreno;
    public Material materialMetalLlave;
    public Material materialCerradura;

    [Header("Referencias Generadas")]
    public Transform fondoPuerta;
    public Transform puertaRoot;
    public Transform hojaPuerta;
    public Transform cerradura;
    public Transform llaveGigante;
    public Transform pedestalLlave;

    [Header("Audio de Nivel 3 (Música de la Llave y la Puerta)")]
    [Tooltip("Música de fondo para Nivel 3 post-estallido. Carga automáticamente 'Audio/music-grete-n3'")]
    public AudioClip clipMusicaNivel3;
    public AudioClip clipClickCerradura;
    private AudioSource audioSourceMusicaN3;
    private AudioSource audioSourceSFX;

    // Estados narrativos de Nivel 3
    public bool Nivel3Iniciado { get; private set; } = false;
    public bool LlaveAgarrada { get; private set; } = false;
    public bool PuertaAbierta { get; private set; } = false;
    public bool EscenaCompletada { get; private set; } = false;

    private Transform jugador;
    private Transform esferaMano;
    private Vector3 offsetAgarreLlave = new Vector3(0f, -0.05f, 0.15f);
    private Vector3 posOriginalLlave;
    private Quaternion rotOriginalLlave;
    private Light luzAuraLlave;
    private Light luzSpotPedestal;
    private Light luzCerradura;
    private BoxCollider colHojaPuerta;

    // Vectores de orientación espacial frontal
    private Vector3 baseJugador = new Vector3(0f, 0f, -42.6f);
    private Vector3 dirCamino = Vector3.forward;
    private Vector3 dirCostadoCamino = Vector3.right;

    void Awake()
    {
        CargarTexturasYMateriales();
    }

    void Start()
    {
        var cam = Camera.main;
        if (cam != null) jugador = cam.transform;

        var goEsfera = GameObject.Find("Sphere");
        if (goEsfera != null)
        {
            esferaMano = goEsfera.transform;
            var sc = goEsfera.GetComponent<SphereCollider>();
            if (sc != null) sc.isTrigger = true;
        }

        if (probarNivel3Directo)
        {
            IniciarNivel3();
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        // Atajo en Unity Editor: presionar tecla '3' para saltar instantáneamente a Nivel 3 y probar
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (jugador != null)
            {
                var cc = jugador.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                jugador.position = new Vector3(0f, 0f, -42.6f);
                if (cc != null) cc.enabled = true;
            }
            IniciarNivel3();
        }
#endif

        if (!Nivel3Iniciado) return;

        ActualizarInteraccionLlave();
        AnimarElementos();
    }

    private void AnimarElementos()
    {
        if (!LlaveAgarrada && llaveGigante != null)
        {
            llaveGigante.position = posOriginalLlave + Vector3.up * (Mathf.Sin(Time.time * 2.0f) * 0.04f);
            llaveGigante.Rotate(Vector3.up * (Time.deltaTime * 18f), Space.World);
        }
    }

    private void CargarTexturasYMateriales()
    {
        if (materialFondoPuerta == null) materialFondoPuerta = Resources.Load<Material>("Mat_N3_FondoGrete");
        if (materialMaderaPuerta == null) materialMaderaPuerta = Resources.Load<Material>("Mat_N3_PuertaGris");
        if (materialMarcoPuerta == null) materialMarcoPuerta = Resources.Load<Material>("Mat_N3_MarcoGris");
        if (materialCamino == null) materialCamino = Resources.Load<Material>("Mat_N3_CaminoGris");
        if (materialTerreno == null) materialTerreno = Resources.Load<Material>("Mat_N3_TerrenoGris");
        if (materialMetalLlave == null) materialMetalLlave = Resources.Load<Material>("Mat_N3_LlaveMetal");
        if (materialCerradura == null) materialCerradura = Resources.Load<Material>("Mat_N3_Cerradura");

        // Cargar explícitamente la textura original de Grete Stern: "La mujer y la llave gigante"
        Texture2D texPuertaLlave = Resources.Load<Texture2D>("grete_puerta_llave");
        if (materialFondoPuerta == null)
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            materialFondoPuerta = new Material(unlit);
        }
        if (materialFondoPuerta != null && texPuertaLlave != null)
        {
            materialFondoPuerta.mainTexture = texPuertaLlave;
            materialFondoPuerta.SetTexture("_BaseMap", texPuertaLlave);
            materialFondoPuerta.SetColor("_BaseColor", Color.white);
            materialFondoPuerta.color = Color.white;
            if (materialFondoPuerta.HasProperty("_EmissionColor"))
            {
                materialFondoPuerta.EnableKeyword("_EMISSION");
                materialFondoPuerta.SetColor("_EmissionColor", Color.white);
            }
        }

        Material matBaseSeguro = Resources.Load<Material>("Mat_GreteBN");
        if (matBaseSeguro != null)
        {
            if (materialMaderaPuerta == null)
            {
                materialMaderaPuerta = new Material(matBaseSeguro);
                materialMaderaPuerta.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.23f));
            }
            if (materialMarcoPuerta == null)
            {
                materialMarcoPuerta = new Material(matBaseSeguro);
                materialMarcoPuerta.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.13f));
            }
            if (materialCamino == null)
            {
                materialCamino = new Material(matBaseSeguro);
                materialCamino.SetColor("_BaseColor", new Color(0.52f, 0.52f, 0.55f));
            }
            if (materialTerreno == null)
            {
                materialTerreno = new Material(matBaseSeguro);
                materialTerreno.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.16f));
            }
            if (materialMetalLlave == null)
            {
                materialMetalLlave = new Material(matBaseSeguro);
            }
            if (materialCerradura == null)
            {
                materialCerradura = new Material(matBaseSeguro);
                materialCerradura.SetColor("_BaseColor", new Color(0.10f, 0.10f, 0.11f));
            }
        }

        // Configuración de brillo y color dorado resplandeciente para la llave
        if (materialMetalLlave == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            materialMetalLlave = new Material(litShader);
        }
        if (materialMetalLlave != null)
        {
            Color cDorado = new Color(1.0f, 0.88f, 0.45f, 1.0f);
            if (materialMetalLlave.HasProperty("_BaseColor")) materialMetalLlave.SetColor("_BaseColor", cDorado);
            if (materialMetalLlave.HasProperty("_Color")) materialMetalLlave.SetColor("_Color", cDorado);
            materialMetalLlave.color = cDorado;

            if (materialMetalLlave.HasProperty("_Metallic")) materialMetalLlave.SetFloat("_Metallic", 0.85f);
            if (materialMetalLlave.HasProperty("_Smoothness")) materialMetalLlave.SetFloat("_Smoothness", 0.75f);
            if (materialMetalLlave.HasProperty("_EmissionColor"))
            {
                materialMetalLlave.EnableKeyword("_EMISSION");
                materialMetalLlave.SetColor("_EmissionColor", new Color(0.28f, 0.22f, 0.10f));
            }
        }

        // Cargar audio de la llave y música de Nivel 3
        if (clipMusicaNivel3 == null)
        {
            clipMusicaNivel3 = Resources.Load<AudioClip>("Audio/music-grete-n3");
        }
        if (clipClickCerradura == null)
        {
            clipClickCerradura = Resources.Load<AudioClip>("Audio/soga_corte_snap");
        }
    }

    /// <summary>
    /// Paso 1: Contemplación de la llave, 3 segundos.
    /// Al caer la última roca, inicia el último nivel. Observo sobre el pedestal la llave.
    /// Me detengo a contemplarla durante 3 segundos. Posteriormente, se habilita el avance para agarrarla con el gesto pinza.
    /// </summary>
    public void IniciarNivel3()
    {
        if (Nivel3Iniciado) return;
        Nivel3Iniciado = true;
        Debug.Log("[Nivel3_PuertaLlave] Paso 1: Contemplación de la llave, 3 segundos.");

        // Destruir completamente objetos residuales de la escultura y montaña de Nivel 2
        var escRoot = GameObject.Find("EsculturaGrete3D_Root");
        if (escRoot != null) Destroy(escRoot);
        var rocasViejas = FindObjectsByType<FragmentoFisicoRebote>(FindObjectsInactive.Include);
        foreach (var r in rocasViejas) if (r != null) Destroy(r.gameObject);
        var boidsViejos = FindObjectsByType<BoidFlock>(FindObjectsInactive.Include);
        foreach (var b in boidsViejos) if (b != null) Destroy(b.gameObject);
        var fm = GameObject.Find("FondoMontana");
        if (fm != null) Destroy(fm);
        var sm = GameObject.Find("SueloMontana");
        if (sm != null) Destroy(sm);
        var esc = GameObject.Find("EsculturaRoot");
        if (esc != null) Destroy(esc);
        var esc3D = GameObject.Find("EsculturaGrete3D");
        if (esc3D != null) Destroy(esc3D);

        var cam = Camera.main;
        if (cam != null)
        {
            jugador = cam.transform;
            CharacterController cc = cam.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Posicionar al jugador en el punto de partida en plano general mirando hacia adelante (+Z)
            cam.transform.position = new Vector3(0f, 0f, -42.6f);
            baseJugador = new Vector3(0f, 0f, -42.6f);

            var mov = cam.GetComponent<Movimiento>();
            if (mov != null) mov.ResetearRotacion(Quaternion.identity);

            if (cc != null) cc.enabled = true;
        }
        else
        {
            baseJugador = new Vector3(0f, 0f, -42.6f);
        }

        var goEsfera = GameObject.Find("Sphere");
        if (goEsfera != null)
        {
            esferaMano = goEsfera.transform;
            var sc = goEsfera.GetComponent<SphereCollider>();
            if (sc != null) sc.isTrigger = true;
        }

        dirCamino = Vector3.forward;
        dirCostadoCamino = Vector3.right;

        // Luz solar exterior para Nivel 3
        GameObject goLuzN3 = new GameObject("LuzAmbientalNivel3");
        goLuzN3.transform.SetParent(transform, false);
        goLuzN3.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
        Light luzSol = goLuzN3.AddComponent<Light>();
        luzSol.type = LightType.Directional;
        luzSol.intensity = 1.35f;
        luzSol.color = new Color(1.0f, 0.98f, 0.94f);

        CargarTexturasYMateriales();
        ConstruirFondoYCamino();
        ConstruirPuertaAislada();
        ConstruirLlaveGigante();

        // Silenciar audios residuales
        AudioSource[] todosLosAudios = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
        foreach (var asrc in todosLosAudios)
        {
            if (asrc != null && asrc != audioSourceMusicaN3 && asrc != audioSourceSFX)
            {
                asrc.Stop();
            }
        }

        // Música de fondo de Nivel 3 (music-grete-n3)
        if (audioSourceMusicaN3 == null)
        {
            audioSourceMusicaN3 = gameObject.AddComponent<AudioSource>();
            audioSourceMusicaN3.spatialBlend = 0.0f; // 2D envolvente
            audioSourceMusicaN3.loop = true;
            audioSourceMusicaN3.volume = 0.85f;
            audioSourceMusicaN3.playOnAwake = false;
        }

        if (audioSourceSFX == null)
        {
            audioSourceSFX = gameObject.AddComponent<AudioSource>();
            audioSourceSFX.spatialBlend = 0.0f; // 2D estéreo directo
            audioSourceSFX.volume = 1.0f;
            audioSourceSFX.playOnAwake = false;
        }

        if (clipMusicaNivel3 != null)
        {
            audioSourceMusicaN3.clip = clipMusicaNivel3;
            audioSourceMusicaN3.Play();
            Debug.Log("[Nivel3_PuertaLlave] Reproduciendo música de Grete Stern: music-grete-n3");
        }

        // "Contemplación de la llave, 3 segundos."
        StartCoroutine(SecuenciaContemplacionInicialLlave());
    }

    /// <summary>
    /// Paso 1: Contemplación solemne de la llave sobre el pedestal durante 3 segundos.
    /// El jugador mira libremente pero sus pies se mantienen detenidos durante 3 segundos.
    /// Al cumplirse, se habilita el avance hacia el pedestal.
    /// </summary>
    private IEnumerator SecuenciaContemplacionInicialLlave()
    {
        EscapeOjo.PuedeMoverse = false;
        Debug.Log("[Nivel3_PuertaLlave] Paso 1: Me detengo a contemplar la llave durante 3 segundos...");

        yield return new WaitForSeconds(3.0f);

        EscapeOjo.PuedeMoverse = true;
        Debug.Log("[Nivel3_PuertaLlave] Paso 1 cumplido. Ahora me acerco para agarrar la llave con el gesto pinza.");
    }

    private void ConstruirFondoYCamino()
    {
        // Sendero de piedra continuo que se extiende a lo largo de todo el recorrido
        GameObject goCamino = GameObject.CreatePrimitive(PrimitiveType.Plane);
        goCamino.name = "CaminoSinuosoNivel3";
        goCamino.transform.SetParent(transform, false);
        goCamino.transform.position = baseJugador + dirCamino * 11.0f + Vector3.down * 1.25f;
        goCamino.transform.rotation = Quaternion.identity;
        goCamino.transform.localScale = new Vector3(0.55f, 1f, 4.0f);

        var colCamino = goCamino.GetComponent<Collider>();
        if (colCamino != null) Destroy(colCamino);

        var rendC = goCamino.GetComponent<Renderer>();
        if (rendC != null && materialCamino != null) rendC.material = materialCamino;

        // Terrenos laterales en gris oscuro
        GameObject goTerrenoIzq = GameObject.CreatePrimitive(PrimitiveType.Plane);
        goTerrenoIzq.name = "TerrenoLateralNorte";
        goTerrenoIzq.transform.SetParent(transform, false);
        goTerrenoIzq.transform.position = baseJugador + dirCamino * 11.0f - dirCostadoCamino * 5.5f + Vector3.down * 1.24f;
        goTerrenoIzq.transform.rotation = Quaternion.identity;
        goTerrenoIzq.transform.localScale = new Vector3(0.65f, 1f, 4.0f);
        Destroy(goTerrenoIzq.GetComponent<Collider>());
        var rendTI = goTerrenoIzq.GetComponent<Renderer>();
        if (rendTI != null && materialTerreno != null) rendTI.material = materialTerreno;

        GameObject goTerrenoDer = GameObject.CreatePrimitive(PrimitiveType.Plane);
        goTerrenoDer.name = "TerrenoLateralSur";
        goTerrenoDer.transform.SetParent(transform, false);
        goTerrenoDer.transform.position = baseJugador + dirCamino * 11.0f + dirCostadoCamino * 5.5f + Vector3.down * 1.24f;
        goTerrenoDer.transform.rotation = Quaternion.identity;
        goTerrenoDer.transform.localScale = new Vector3(0.65f, 1f, 4.0f);
        Destroy(goTerrenoDer.GetComponent<Collider>());
        var rendTD = goTerrenoDer.GetComponent<Renderer>();
        if (rendTD != null && materialTerreno != null) rendTD.material = materialTerreno;

        // Gigantografía final 2D: La mujer y la llave gigante en la montaña
        // Elevada de modo que su base descanse sobre el sendero (Y = -1.25m) y su altura total quede 100% visible
        float alturaFoto = 5.6f;
        float anchoFoto = alturaFoto * (952f / 1024f); // 5.206m
        float centerY = -1.25f + (alturaFoto * 0.5f); // 1.55m -> base exactamente en -1.25m (nivel del suelo)

        GameObject goFondo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        goFondo.name = "FondoGretePuerta";
        goFondo.transform.SetParent(transform, false);
        // Situada a 15.0m del inicio (a 6.5m detrás de la puerta aislada, Z = -27.6m)
        goFondo.transform.position = baseJugador + dirCamino * 15.0f + Vector3.up * centerY;
        goFondo.transform.rotation = Quaternion.identity;
        goFondo.transform.localScale = new Vector3(anchoFoto, alturaFoto, 1.0f);
        
        // La imagen de Grete no debe poder atravesarse: Collider físico sólido
        var colAntiguo = goFondo.GetComponent<Collider>();
        if (colAntiguo != null) Destroy(colAntiguo);
        var colFondo = goFondo.AddComponent<BoxCollider>();
        colFondo.size = new Vector3(1f, 1f, 0.8f);
        colFondo.center = new Vector3(0f, 0f, 0.4f);
        colFondo.isTrigger = false;

        var rendF = goFondo.GetComponent<Renderer>();
        if (rendF != null)
        {
            Material matBase = Resources.Load<Material>("Mat_N3_FondoGrete");
            Material matFondo = null;
            if (matBase != null)
            {
                matFondo = new Material(matBase);
            }
            else
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
                matFondo = new Material(unlit);
            }

            Texture2D texPuertaLlave = Resources.Load<Texture2D>("grete_puerta_llave");
            if (texPuertaLlave != null)
            {
                matFondo.mainTexture = texPuertaLlave;
                if (matFondo.HasProperty("_BaseMap")) matFondo.SetTexture("_BaseMap", texPuertaLlave);
                if (matFondo.HasProperty("_MainTex")) matFondo.SetTexture("_MainTex", texPuertaLlave);
                if (matFondo.HasProperty("_EmissionMap")) matFondo.SetTexture("_EmissionMap", texPuertaLlave);
                if (matFondo.HasProperty("_EmissionColor"))
                {
                    matFondo.EnableKeyword("_EMISSION");
                    matFondo.SetColor("_EmissionColor", Color.white);
                }
            }
            if (matFondo.HasProperty("_BaseColor")) matFondo.SetColor("_BaseColor", Color.white);
            if (matFondo.HasProperty("_Color")) matFondo.SetColor("_Color", Color.white);
            matFondo.color = Color.white;
            rendF.material = matFondo;

            // Duplicado trasero para visibilidad perfecta sin culling
            GameObject goFondoBack = GameObject.CreatePrimitive(PrimitiveType.Quad);
            goFondoBack.name = "FondoGretePuerta_Back";
            goFondoBack.transform.SetParent(goFondo.transform, false);
            goFondoBack.transform.localPosition = Vector3.zero;
            goFondoBack.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            goFondoBack.transform.localScale = Vector3.one;
            Destroy(goFondoBack.GetComponent<Collider>());
            var rendBack = goFondoBack.GetComponent<Renderer>();
            if (rendBack != null) rendBack.material = matFondo;
        }

        // Iluminación dedicada suave frontal sobre la obra de arte
        GameObject goLuzFondo = new GameObject("LuzGigantografiaGrete");
        goLuzFondo.transform.SetParent(goFondo.transform, false);
        goLuzFondo.transform.localPosition = new Vector3(0f, 0f, -2.5f);
        Light luzObra = goLuzFondo.AddComponent<Light>();
        luzObra.type = LightType.Point;
        luzObra.range = 14.0f;
        luzObra.intensity = 2.2f;
        luzObra.color = Color.white;

        fondoPuerta = goFondo.transform;

        // La gigantografía está SIEMPRE ACTIVA en el mundo detrás de la puerta
        goFondo.SetActive(true);
    }

    private void ConstruirPuertaAislada()
    {
        // Puerta situada sobre el camino a 8.5m de distancia del punto inicial
        GameObject goRoot = new GameObject("PuertaAisladaGrete");
        goRoot.transform.SetParent(transform, false);
        goRoot.transform.position = baseJugador + dirCamino * 8.5f + Vector3.down * 1.25f;
        goRoot.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        puertaRoot = goRoot.transform;

        // Marco Robusto en Gris Grafito Oscuro (sin colisión)
        CrearBloque("Marco_PosteIzq", goRoot.transform, new Vector3(-1.05f, 1.4f, 0f), new Vector3(0.18f, 2.8f, 0.25f), materialMarcoPuerta, false);
        CrearBloque("Marco_PosteDer", goRoot.transform, new Vector3(1.05f, 1.4f, 0f), new Vector3(0.18f, 2.8f, 0.25f), materialMarcoPuerta, false);
        CrearBloque("Marco_Dintel", goRoot.transform, new Vector3(0f, 2.8f, 0f), new Vector3(2.3f, 0.20f, 0.25f), materialMarcoPuerta, false);
        CrearBloque("Marco_Umbral", goRoot.transform, new Vector3(0f, 0.05f, 0f), new Vector3(2.3f, 0.10f, 0.35f), materialMarcoPuerta, false);

        // Pivote de la Bisagra de la Puerta
        GameObject goPivotHoja = new GameObject("PivoteBisagraPuerta");
        goPivotHoja.transform.SetParent(goRoot.transform, false);
        goPivotHoja.transform.localPosition = new Vector3(-0.96f, 0.10f, 0f);
        goPivotHoja.transform.localRotation = Quaternion.identity;
        hojaPuerta = goPivotHoja.transform;

        // Hoja Batiente de la Puerta (sin colisiones que traben al jugador ni a la mano)
        GameObject goHojaMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        goHojaMesh.name = "HojaPuertaMesh";
        goHojaMesh.transform.SetParent(goPivotHoja.transform, false);
        goHojaMesh.transform.localPosition = new Vector3(0.96f, 1.30f, 0f);
        goHojaMesh.transform.localScale = new Vector3(1.92f, 2.60f, 0.10f);

        // isTrigger para que no actúe jamás como obstáculo físico impidiendo el paso
        colHojaPuerta = goHojaMesh.GetComponent<BoxCollider>();
        if (colHojaPuerta != null) colHojaPuerta.isTrigger = true;

        var rendH = goHojaMesh.GetComponent<Renderer>();
        if (rendH != null && materialMaderaPuerta != null) rendH.material = materialMaderaPuerta;

        // Molduras decorativas
        CrearBloque("Panel_Superior_Grete", goPivotHoja.transform, new Vector3(0.96f, 1.95f, 0.052f), new Vector3(1.58f, 0.95f, 0.015f), materialMarcoPuerta, false);
        CrearBloque("Panel_Inferior_Grete", goPivotHoja.transform, new Vector3(0.96f, 0.70f, 0.052f), new Vector3(1.58f, 1.05f, 0.015f), materialMarcoPuerta, false);

        // Cerradura de hierro forjado gris oscuro
        GameObject goCerradura = GameObject.CreatePrimitive(PrimitiveType.Cube);
        goCerradura.name = "CerraduraPuerta";
        goCerradura.transform.SetParent(goPivotHoja.transform, false);
        goCerradura.transform.localPosition = new Vector3(1.72f, 1.25f, 0.08f);
        goCerradura.transform.localScale = new Vector3(0.24f, 0.45f, 0.14f);

        var colCer = goCerradura.GetComponent<Collider>();
        if (colCer != null) colCer.isTrigger = true;

        var rendCer = goCerradura.GetComponent<Renderer>();
        if (rendCer != null && materialCerradura != null) rendCer.material = materialCerradura;
        cerradura = goCerradura.transform;

        // Bocallave
        GameObject goOjoCer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        goOjoCer.name = "Bocallave";
        goOjoCer.transform.SetParent(goCerradura.transform, false);
        goOjoCer.transform.localPosition = new Vector3(0f, 0f, 0.55f);
        goOjoCer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        goOjoCer.transform.localScale = new Vector3(0.40f, 0.10f, 0.40f);
        Destroy(goOjoCer.GetComponent<Collider>());

        var rendOjo = goOjoCer.GetComponent<Renderer>();
        if (rendOjo != null && materialMetalLlave != null) rendOjo.material = materialMetalLlave;

        // Luz indicadora en la cerradura
        GameObject goLuzC = new GameObject("LuzCerradura");
        goLuzC.transform.SetParent(goCerradura.transform, false);
        goLuzC.transform.localPosition = new Vector3(0f, 0f, 0.6f);
        luzCerradura = goLuzC.AddComponent<Light>();
        luzCerradura.type = LightType.Point;
        luzCerradura.range = 3.0f;
        luzCerradura.intensity = 1.6f;
        luzCerradura.color = new Color(1.0f, 0.95f, 0.7f);
    }

    private void ConstruirLlaveGigante()
    {
        // Pedestal cilíndrico robusto ubicado a 3.6m del jugador para contemplación frontal óptima
        // Base en Y = -1.25m (suelo del sendero). Altura del cilindro = 0.90m -> Centro Y = -0.80m, Superficie superior en Y = -0.35m
        Vector3 posPedestal = baseJugador + dirCamino * 3.6f + dirCostadoCamino * 0.20f + Vector3.up * (-1.25f + 0.45f);
        GameObject goPedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        goPedestal.name = "PedestalLlave";
        goPedestal.transform.SetParent(transform, false);
        goPedestal.transform.position = posPedestal;
        goPedestal.transform.localScale = new Vector3(0.85f, 0.45f, 0.85f);
        Destroy(goPedestal.GetComponent<Collider>());

        var rendPed = goPedestal.GetComponent<Renderer>();
        if (rendPed != null && materialMarcoPuerta != null) rendPed.material = materialMarcoPuerta;
        pedestalLlave = goPedestal.transform;

        // Plinto superior decorativo del pedestal (anillo de tope)
        GameObject goTopePedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        goTopePedestal.name = "TopePedestal";
        goTopePedestal.transform.SetParent(goPedestal.transform, false);
        goTopePedestal.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        goTopePedestal.transform.localScale = new Vector3(1.08f, 0.08f, 1.08f);
        Destroy(goTopePedestal.GetComponent<Collider>());
        var rendTope = goTopePedestal.GetComponent<Renderer>();
        if (rendTope != null && materialMarcoPuerta != null) rendTope.material = materialMarcoPuerta;

        // POSICIÓN DE LA LLAVE: Claramente SOBRE EL PEDESTAL (a Y = +0.05m, 40cm por encima del plinto)
        // Flota a la vista directa del espectador, majestuosa, dorada e imposible de perder
        Vector3 posLlave = new Vector3(posPedestal.x, 0.05f, posPedestal.z);
        GameObject goLlave = new GameObject("LlaveGiganteGrete");
        goLlave.transform.SetParent(transform, false);
        goLlave.transform.position = posLlave;
        goLlave.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
        llaveGigante = goLlave.transform;

        posOriginalLlave = posLlave;
        rotOriginalLlave = goLlave.transform.rotation;

        // Cargar el modelo 3D Meshy AI ("Old_Key")
        GameObject prefabLlave3D = Resources.Load<GameObject>("Old_Key");
        if (prefabLlave3D != null)
        {
            GameObject modeloInstanciado = Instantiate(prefabLlave3D, goLlave.transform, false);
            modeloInstanciado.name = "ModeloMeshy_OldKey";
            modeloInstanciado.transform.localPosition = Vector3.zero;
            // Inclinación de 30 grados para apreciar la silueta completa de la llave antigua de frente
            modeloInstanciado.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            modeloInstanciado.transform.localScale = Vector3.one * 0.55f; // Longitud de 1.05m majestuosa

            foreach (var col in modeloInstanciado.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }
            if (materialMetalLlave != null)
            {
                foreach (var rend in modeloInstanciado.GetComponentsInChildren<Renderer>())
                {
                    rend.material = materialMetalLlave;
                }
            }
        }
        else
        {
            // Respaldo procedural si el modelo 3D no estuviera cargado
            GameObject goAro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goAro.name = "AroLlave";
            goAro.transform.SetParent(goLlave.transform, false);
            goAro.transform.localPosition = new Vector3(-0.25f, 0.20f, 0f);
            goAro.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            goAro.transform.localScale = new Vector3(0.40f, 0.05f, 0.35f);
            Destroy(goAro.GetComponent<Collider>());
            var rendA = goAro.GetComponent<Renderer>();
            if (rendA != null && materialMetalLlave != null) rendA.material = materialMetalLlave;

            GameObject goVastago = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goVastago.name = "VastagoLlave";
            goVastago.transform.SetParent(goLlave.transform, false);
            goVastago.transform.localPosition = new Vector3(0.18f, 0f, 0f);
            goVastago.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            goVastago.transform.localScale = new Vector3(0.08f, 0.45f, 0.08f);
            Destroy(goVastago.GetComponent<Collider>());
            var rendV = goVastago.GetComponent<Renderer>();
            if (rendV != null && materialMetalLlave != null) rendV.material = materialMetalLlave;

            GameObject goDientes = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goDientes.name = "DientesLlave";
            goDientes.transform.SetParent(goLlave.transform, false);
            goDientes.transform.localPosition = new Vector3(0.45f, -0.12f, 0.04f);
            goDientes.transform.localScale = new Vector3(0.12f, 0.16f, 0.08f);
            Destroy(goDientes.GetComponent<Collider>());
            var rendD = goDientes.GetComponent<Renderer>();
            if (rendD != null && materialMetalLlave != null) rendD.material = materialMetalLlave;
        }

        // 1. Aura luminosa dorada centrada en la llave
        GameObject goLuzLlave = new GameObject("AuraLlave");
        goLuzLlave.transform.SetParent(goLlave.transform, false);
        goLuzLlave.transform.localPosition = Vector3.up * 0.15f;
        luzAuraLlave = goLuzLlave.AddComponent<Light>();
        luzAuraLlave.type = LightType.Point;
        luzAuraLlave.range = 4.5f;
        luzAuraLlave.intensity = 2.8f;
        luzAuraLlave.color = new Color(1.0f, 0.92f, 0.65f);

        // 2. Foco cenital que ilumina el pedestal y la llave desde arriba como en una galería de arte
        GameObject goSpot = new GameObject("SpotLuzPedestal");
        goSpot.transform.SetParent(transform, false);
        goSpot.transform.position = posLlave + Vector3.up * 2.8f;
        goSpot.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
        luzSpotPedestal = goSpot.AddComponent<Light>();
        luzSpotPedestal.type = LightType.Spot;
        luzSpotPedestal.spotAngle = 48f;
        luzSpotPedestal.range = 5.5f;
        luzSpotPedestal.intensity = 3.8f;
        luzSpotPedestal.color = new Color(1.0f, 0.98f, 0.88f);
    }

    private GameObject CrearBloque(string nombre, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool conCollider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        if (!conCollider)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
        var r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.material = mat;
        return go;
    }

    /// <summary>
    /// Helper unificado seguro para detectar interacciones (Gesto pinza, trigger de Cardboard, touch en pantalla o teclado/mouse).
    /// Protegido con try-catch para garantizar que JAMÁS arroje excepciones ni errores rojos en VR.
    /// </summary>
    public static bool EsAccionOToquePresionado()
    {
        try
        {
            // 1. Gesto pinza MediaPipe / VR Hand
            if (OSCHandReceiver.IsPinching || VRHandVisual.IsPinching)
            {
                return true;
            }

            // 2. Google Cardboard XR Trigger (botón físico o conductivo del visor Cardboard)
            if (Google.XR.Cardboard.Api.IsTriggerPressed || Google.XR.Cardboard.Api.IsTriggerHeldPressed)
            {
                return true;
            }

            // 3. Unity Input System Touchscreen
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null)
            {
                if (!ts.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(ts);
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].press.wasPressedThisFrame || touches[i].press.isPressed)
                    {
                        return true;
                    }
                }
            }

            // 4. Fallback Legacy Input
            if (Input.touchCount > 0)
            {
                return true;
            }

#if UNITY_EDITOR
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) return true;
            }
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
        }
        catch
        {
            // Silencioso para evitar cualquier overlay rojo en VR
        }
        return false;
    }

    /// <summary>
    /// Actualiza la interacción con la llave según los pasos requeridos:
    /// Paso 1: Agarre de la llave en el pedestal.
    /// Paso 2: Desplazamiento hacia la puerta -> Inserción, giro de cerradura, apertura y desaparición de la llave.
    /// </summary>
    private void ActualizarInteraccionLlave()
    {
        try
        {
            if (jugador == null && Camera.main != null)
            {
                jugador = Camera.main.transform;
            }

            // -------------------------------------------------------------
            // ESTADO A: Llave esperando sobre el pedestal (Paso 1)
            // -------------------------------------------------------------
            if (!LlaveAgarrada)
            {
                if (llaveGigante == null) return;

                float distJugadorALlave = (jugador != null) ? Vector3.Distance(jugador.position, llaveGigante.position) : 999f;
                float distManoALlave = (esferaMano != null) ? Vector3.Distance(esferaMano.position, llaveGigante.position) : 999f;

                // Interactuable cuando el jugador se acerca al pedestal
                bool cercaDelPedestal = (distJugadorALlave <= 2.8f) || (distManoALlave <= 1.8f);

                if (cercaDelPedestal && EscapeOjo.PuedeMoverse)
                {
                    if (luzAuraLlave != null)
                    {
                        luzAuraLlave.color = new Color(0.25f, 1.0f, 0.45f); // Verde invitando a interactuar
                        luzAuraLlave.intensity = 3.6f;
                    }

                    bool accionAgarrar = EsAccionOToquePresionado();

                    // Agarrar si se pinza, se toca la pantalla/visor, o por aproximación directa al pedestal (<= 1.6m)
                    if (accionAgarrar || distJugadorALlave <= 1.6f || distManoALlave <= 0.8f)
                    {
                        AgarrarLlave();
                    }
                }
                else
                {
                    if (luzAuraLlave != null)
                    {
                        luzAuraLlave.color = new Color(1.0f, 0.92f, 0.65f);
                        luzAuraLlave.intensity = 2.8f + Mathf.Sin(Time.time * 2.5f) * 0.4f;
                    }
                }
            }
            else if (!PuertaAbierta && llaveGigante != null)
            {
                // -------------------------------------------------------------
                // ESTADO B: Llave en mano acompañando al jugador hacia la puerta (Paso 2)
                // -------------------------------------------------------------
                bool manoRastreadaCerca = (esferaMano != null && jugador != null && Vector3.Distance(esferaMano.position, jugador.position) < 2.0f);

                if (manoRastreadaCerca)
                {
                    Vector3 posObjetivo = esferaMano.position + esferaMano.rotation * offsetAgarreLlave;
                    llaveGigante.position = Vector3.Lerp(llaveGigante.position, posObjetivo, Time.deltaTime * 18f);
                    llaveGigante.rotation = Quaternion.Slerp(llaveGigante.rotation, esferaMano.rotation * Quaternion.Euler(15f, 0f, 0f), Time.deltaTime * 18f);
                }
                else if (jugador != null)
                {
                    // En PC o visor Cardboard: flotando visiblemente en la esquina frontal derecha de la vista
                    Vector3 posFrente = jugador.position + jugador.forward * 0.70f + jugador.right * 0.26f + Vector3.down * 0.18f;
                    Quaternion rotFrente = jugador.rotation * Quaternion.Euler(20f, -25f, 15f);
                    llaveGigante.position = Vector3.Lerp(llaveGigante.position, posFrente, Time.deltaTime * 18f);
                    llaveGigante.rotation = Quaternion.Slerp(llaveGigante.rotation, rotFrente, Time.deltaTime * 18f);
                }

                // PASO 2: "Me acerco a la puerta y gira la cerradura para abrirse la puerta. Desaparece la llave."
                Vector3 posPuerta = (puertaRoot != null) ? puertaRoot.position : (baseJugador + dirCamino * 8.5f);
                float distZJugador = (jugador != null) ? Mathf.Abs(jugador.position.z - posPuerta.z) : 999f;
                float dist2DJugador = (jugador != null) ? Vector2.Distance(new Vector2(jugador.position.x, jugador.position.z), new Vector2(posPuerta.x, posPuerta.z)) : 999f;
                float distZLlave = (llaveGigante != null) ? Mathf.Abs(llaveGigante.position.z - posPuerta.z) : 999f;
                float distZMano = (esferaMano != null) ? Mathf.Abs(esferaMano.position.z - posPuerta.z) : 999f;

                bool frenteALaPuerta = (distZJugador <= 3.2f) || (dist2DJugador <= 3.2f) || (distZLlave <= 2.5f) || (distZMano <= 2.5f);
                bool accionAbrir = EsAccionOToquePresionado();

                // Abre inmediatamente tanto por proximidad directa (distancia <= 2.6m) como al pinzar o presionar trigger
                if (frenteALaPuerta && (distZJugador <= 2.6f || dist2DJugador <= 2.6f || distZLlave <= 2.0f || distZMano <= 2.0f || accionAbrir))
                {
                    InsertarLlaveYAbrirPuerta();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Nivel3_PuertaLlave] Aviso en interacción llave: " + ex.Message);
        }
    }

    private void AgarrarLlave()
    {
        LlaveAgarrada = true;
        Debug.Log("[Nivel3_PuertaLlave] Gesto pinza realizado. ¡Llave agarrada! Me acerco hacia la puerta.");

        if (audioSourceSFX != null && clipClickCerradura != null)
        {
            audioSourceSFX.pitch = 1.25f;
            audioSourceSFX.PlayOneShot(clipClickCerradura, 0.90f);
            audioSourceSFX.pitch = 1.0f;
        }

        if (luzAuraLlave != null)
        {
            luzAuraLlave.color = new Color(1.0f, 0.95f, 0.70f);
            luzAuraLlave.intensity = 3.5f;
        }
    }

    private void InsertarLlaveYAbrirPuerta()
    {
        if (PuertaAbierta) return;
        PuertaAbierta = true;
        Debug.Log("[Nivel3_PuertaLlave] Paso 2: Me acerco a la puerta, gira la cerradura y se abre la puerta.");

        StartCoroutine(SecuenciaAperturaPuerta());
    }

    /// <summary>
    /// Paso 2: Gira la cerradura, se abre la puerta, desaparece la llave.
    /// Al terminar la apertura, conduce de manera fluida y suave al Paso 3 y Paso 4.
    /// </summary>
    private IEnumerator SecuenciaAperturaPuerta()
    {
        // 1. Sonido inicial del chasquido mecánico
        if (audioSourceSFX != null && clipClickCerradura != null)
        {
            audioSourceSFX.pitch = 1.0f;
            audioSourceSFX.PlayOneShot(clipClickCerradura, 1.0f);
        }

        // 2. Desplazamiento y encastre de la llave en la cerradura
        if (cerradura != null && llaveGigante != null)
        {
            llaveGigante.SetParent(cerradura, true);
            Vector3 targetPos = cerradura.position + cerradura.forward * 0.15f;
            Quaternion targetRot = cerradura.rotation * Quaternion.Euler(0f, 90f, 0f);

            float tIns = 0f;
            float durIns = 0.40f;
            Vector3 posIni = llaveGigante.position;
            Quaternion rotIni = llaveGigante.rotation;

            while (tIns < durIns)
            {
                tIns += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, tIns / durIns);
                llaveGigante.position = Vector3.Lerp(posIni, targetPos, p);
                llaveGigante.rotation = Quaternion.Slerp(rotIni, targetRot, p);
                yield return null;
            }

            // 3. Gira la cerradura (90 grados) con segundo click
            if (audioSourceSFX != null && clipClickCerradura != null)
            {
                audioSourceSFX.pitch = 0.92f;
                audioSourceSFX.PlayOneShot(clipClickCerradura, 1.0f);
                audioSourceSFX.pitch = 1.0f;
            }

            float tGiro = 0f;
            float durGiro = 0.40f;
            Quaternion rotIniGiro = llaveGigante.localRotation;
            Quaternion rotFinGiro = rotIniGiro * Quaternion.Euler(0f, 0f, 90f);
            while (tGiro < durGiro)
            {
                tGiro += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, tGiro / durGiro);
                llaveGigante.localRotation = Quaternion.Slerp(rotIniGiro, rotFinGiro, p);
                yield return null;
            }

            // 4. "Desaparece la llave."
            Destroy(llaveGigante.gameObject);
            llaveGigante = null;
            Debug.Log("[Nivel3_PuertaLlave] Paso 2: Llave desaparecida.");
        }

        // Destello de luz en la cerradura
        if (luzCerradura != null)
        {
            luzCerradura.color = new Color(0.8f, 1.0f, 0.9f);
            luzCerradura.intensity = 4.0f;
            luzCerradura.range = 4.0f;
        }

        // Quitar todos los colliders de la puerta para que el umbral quede totalmente libre y transitable
        if (colHojaPuerta != null) Destroy(colHojaPuerta);
        if (cerradura != null)
        {
            foreach (var col in cerradura.GetComponentsInChildren<Collider>()) Destroy(col);
        }
        if (puertaRoot != null)
        {
            foreach (var col in puertaRoot.GetComponentsInChildren<Collider>())
            {
                if (col != null) Destroy(col);
            }
        }

        // Revelar y asegurar que la gigantografía esté 100% activa
        if (fondoPuerta != null)
        {
            fondoPuerta.gameObject.SetActive(true);
        }

        // 5. Se abre la puerta suavemente (-90 grados, duración 1.6 segundos)
        if (hojaPuerta != null)
        {
            float tApertura = 0f;
            float duracionApertura = 1.6f;
            Quaternion rotCerrada = Quaternion.identity;
            Quaternion rotAbierta = Quaternion.Euler(0f, -90f, 0f);

            while (tApertura < duracionApertura)
            {
                tApertura += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, tApertura / duracionApertura);
                hojaPuerta.localRotation = Quaternion.Slerp(rotCerrada, rotAbierta, p);
                yield return null;
            }
        }

        Debug.Log("[Nivel3_PuertaLlave] Paso 2 culminado: Puerta abierta de par en par.");

        // Iniciar Paso 3: Avanzar hacia la gigantografía y Paso 4: Contemplación durante 3 segundos
        StartCoroutine(SecuenciaAvanzarYContemplarGigantografia());
    }

    /// <summary>
    /// Paso 3: Avanzo hacia la gigantografía de Grete con la llave (cruzando el umbral de la puerta).
    /// Paso 4: La contemplo durante otros 3 segundos más y recién ahí aparece la leyenda del final.
    /// </summary>
    private IEnumerator SecuenciaAvanzarYContemplarGigantografia()
    {
        Debug.Log("[Nivel3_PuertaLlave] Paso 3: Avanzo hacia la gigantografía de Grete con la llave.");

        // Ocultar la mano procedural / cursor para que la contemplación visual sea 100% limpia y sin estorbos en pantalla
        if (esferaMano != null)
        {
            esferaMano.gameObject.SetActive(false);
        }

        // Pausa breve de 0.4s para apreciar la apertura de par en par de la puerta
        yield return new WaitForSeconds(0.4f);

        var cam = Camera.main;
        if (cam != null) jugador = cam.transform;

        float posZPuerta = (puertaRoot != null) ? puertaRoot.position.z : (baseJugador.z + 8.5f); // Z = -34.1f
        Vector3 posDestinoFrenteObra = new Vector3(0f, 0f, -31.5f); // 2.6m más allá del umbral de la puerta, frente a la gigantografía

        // Desactivar temporalmente CharacterController para desplazar cinemáticamente al jugador atravesando el umbral
        EscapeOjo.PuedeMoverse = false;
        CharacterController cc = (jugador != null) ? jugador.GetComponent<CharacterController>() : null;
        if (cc != null) cc.enabled = false;

        if (jugador != null)
        {
            Vector3 posInicio = jugador.position;
            float tAvance = 0f;
            float durAvance = 2.4f;
            while (tAvance < durAvance)
            {
                tAvance += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, tAvance / durAvance);
                jugador.position = Vector3.Lerp(posInicio, posDestinoFrenteObra, p);
                yield return null;
            }
            jugador.position = posDestinoFrenteObra;
        }

        if (cc != null) cc.enabled = true;
        // Habilitar rotación de cámara libre para contemplación
        EscapeOjo.PuedeMoverse = true;

        // =========================================================================
        // PASO 4: "La contemplo durante otros 3 segundos más y recién ahí aparece la leyenda del final."
        // =========================================================================
        Debug.Log("[Nivel3_PuertaLlave] Paso 4: Contemplando la gigantografía de Grete con la llave durante 3 segundos...");

        // 3 segundos exactos de contemplación pura, sin carteles, viendo la gigantografía de cuerpo entero
        yield return new WaitForSeconds(3.0f);

        // "...y recién ahí aparece la leyenda del final. Pero la gigantografía de la llave permanece."
        Debug.Log("[Nivel3_PuertaLlave] 3 segundos de contemplación cumplidos. Mostrando leyenda final con fondo negro y letras doradas simultáneas.");
        EscenaCompletada = true;

        MostrarBannerFinalSincronico();
    }

    private void MostrarBannerFinalSincronico()
    {
        var cam = Camera.main;
        if (cam == null) return;

        try
        {
            // Contenedor fijado a la mirada del jugador (a 1.35m de distancia para visibilidad nítida y óptima)
            GameObject goBannerFinal = new GameObject("PantallaFinalEmancipacion");
            goBannerFinal.transform.SetParent(cam.transform, false);
            goBannerFinal.transform.localPosition = new Vector3(0f, 0.05f, 1.35f);
            goBannerFinal.transform.localRotation = Quaternion.identity;

            // 1. Placa de fondo negro (orden 2: detrás del texto, delante de la foto de Grete)
            GameObject fondoPlaca = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fondoPlaca.name = "FondoNegroBannerFinal";
            fondoPlaca.transform.SetParent(goBannerFinal.transform, false);
            fondoPlaca.transform.localPosition = new Vector3(0f, 0f, 0.02f); // Justo detrás de las letras
            fondoPlaca.transform.localRotation = Quaternion.identity;
            fondoPlaca.transform.localScale = new Vector3(2.4f, 1.45f, 1.0f);
            Destroy(fondoPlaca.GetComponent<Collider>());

            Renderer rendFondo = fondoPlaca.GetComponent<Renderer>();
            Shader shaderFondo = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material matFondo = new Material(shaderFondo);
            matFondo.renderQueue = 3000; // Transparent: delante de la foto de Grete (queue 2000)

            Color cNegroIni = new Color(0.02f, 0.02f, 0.02f, 0f);
            if (matFondo.HasProperty("_Color")) matFondo.SetColor("_Color", cNegroIni);
            if (matFondo.HasProperty("_BaseColor")) matFondo.SetColor("_BaseColor", cNegroIni);
            matFondo.color = cNegroIni;
            if (rendFondo != null) rendFondo.material = matFondo;

            // 2. Texto dorado (orden 1: al frente de todo, visible sobre el recuadro negro)
            GameObject goTexto = new GameObject("TextoFinal");
            goTexto.transform.SetParent(goBannerFinal.transform, false);
            goTexto.transform.localPosition = Vector3.zero;

            TextMesh tm = goTexto.AddComponent<TextMesh>();
            tm.text = "GRETE STERN\n\nLos Sueños (1948 - 1951)\n\n★ LA EMANCIPACIÓN ★\nObra Completada";
            tm.fontSize = 42;
            tm.characterSize = 0.016f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1.0f, 0.92f, 0.35f, 0f);

            Renderer rendTexto = goTexto.GetComponent<Renderer>();
            if (rendTexto != null && rendTexto.material != null)
            {
                rendTexto.material.renderQueue = 3500; // Dibuja siempre al frente sobre el fondo negro
            }

            // 3. Fade In sincrónico y simultáneo
            StartCoroutine(SecuenciaFadeInSincronico(matFondo, tm));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Nivel3_PuertaLlave] Aviso en revelación final: " + ex.Message);
        }
    }

    private IEnumerator SecuenciaFadeInSincronico(Material matFondo, TextMesh tm)
    {
        // Al emerger la pantalla final, fijar la posición del jugador para contemplación
        EscapeOjo.PuedeMoverse = false;

        float t = 0f;
        float duracion = 0.85f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duracion);
            if (matFondo != null)
            {
                Color cFondo = new Color(0.02f, 0.02f, 0.02f, alpha * 0.94f);
                if (matFondo.HasProperty("_Color")) matFondo.SetColor("_Color", cFondo);
                if (matFondo.HasProperty("_BaseColor")) matFondo.SetColor("_BaseColor", cFondo);
                matFondo.color = cFondo;
            }
            if (tm != null)
            {
                tm.color = new Color(1.0f, 0.92f, 0.35f, alpha);
            }
            yield return null;
        }

        // Estado final permanente
        if (matFondo != null)
        {
            Color cFondo = new Color(0.02f, 0.02f, 0.02f, 0.94f);
            if (matFondo.HasProperty("_Color")) matFondo.SetColor("_Color", cFondo);
            if (matFondo.HasProperty("_BaseColor")) matFondo.SetColor("_BaseColor", cFondo);
            matFondo.color = cFondo;
        }
        if (tm != null)
        {
            tm.color = new Color(1.0f, 0.92f, 0.35f, 1.0f);
        }
    }
}
