using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representación visual 3D articulada de la mano en VR.
/// - Funciona de inmediato creando una mano 3D articulada con palma y 5 dedos (3 falanges por dedo)
///   que responden en tiempo real a los datos de MediaPipe (pinza + flexión individual de cada dedo).
/// - Además, es 100% compatible con paquetes de la Asset Store como "Free Pack - VR Hands":
///   si colocás un modelo de mano riggeada como hijo de Sphere (o lo asignás en customRiggedHand),
///   detecta automáticamente los huesos de los dedos y los anima con MediaPipe.
/// </summary>
public class VRHandVisual : MonoBehaviour
{
    [Header("Modelo Personalizado (Opcional - ej. Free Pack VR Hands)")]
    [Tooltip("Si importás 'Free Pack - VR Hands' y arrastrás el prefab acá (o como hijo de Sphere), se usará ese modelo.")]
    public Transform customRiggedHand;

    [Header("Colores de Estado")]
    public Color colorManoAbierta = new Color(0.95f, 0.82f, 0.72f, 1f);
    public Color colorManoPinza = new Color(0.2f, 0.95f, 0.45f, 1f);

    // Articulaciones procedurales [5 dedos][3 falanges]
    private Transform[,] articulacionesProcedurales = new Transform[5, 3];
    private List<Renderer> renderersProcedurales = new List<Renderer>();
    private Material materialMano;

    // Articulaciones de modelo riggeado (Free Pack - VR Hands)
    private List<Transform>[] huesosRiggeados = new List<Transform>[5];
    private List<Quaternion>[] rotacionesBaseRig = new List<Quaternion>[5];
    private Renderer[] renderersRiggeados;
    private Color[] coloresOriginalesRig;
    private bool usandoModeloRiggeado = false;

    // Valores objetivo recibidos desde MediaPipe (0 = extendido, 1 = cerrado)
    // Inicializados en 0.5f (mano relajada en reposo, dedos semi-curvados naturalmente)
    private float[] curlObjetivo = new float[5] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
    private float[] curlActual = new float[5] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
    private float pinchObjetivo = 0f;
    private float pinchActual = 0f;
    private bool dedosRecibidos = false;

    void Start()
    {
        // Ocultar el MeshRenderer de la esfera base pero mantener su SphereCollider para tocar el portal_ojo
        MeshRenderer sphereRenderer = GetComponent<MeshRenderer>();
        if (sphereRenderer != null)
        {
            sphereRenderer.enabled = false;
        }

        // Verificar si el usuario ya colocó un modelo de mano de Asset Store como hijo
        if (customRiggedHand == null && transform.childCount > 0)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name != "VRHand_ProceduralRoot")
                {
                    customRiggedHand = child;
                    break;
                }
            }
        }

        if (customRiggedHand != null)
        {
            ConfigurarManoRiggeada(customRiggedHand);
        }
        else
        {
            ConstruirManoArticulada3D();
        }
    }

    public void ActualizarDedos(float thumb, float index, float middle, float ring, float pinky, float pinchAmount)
    {
        dedosRecibidos = true;
        curlObjetivo[0] = Mathf.Clamp01(thumb);
        curlObjetivo[1] = Mathf.Clamp01(index);
        curlObjetivo[2] = Mathf.Clamp01(middle);
        curlObjetivo[3] = Mathf.Clamp01(ring);
        curlObjetivo[4] = Mathf.Clamp01(pinky);
        pinchObjetivo = Mathf.Clamp01(pinchAmount);
    }

    // Estado de Gestos (detectado por Python OSC o directamente por flexión de dedos)
    public static bool EsSimboloDePaz { get; private set; } = false;
    public static bool EsPulgarArriba { get; private set; } = false;
    public static bool EsManoAbierta => EsPulgarArriba;
    public static bool EsPunoCerrado { get; private set; } = false;
    public static bool IsPinching => OSCHandReceiver.IsPinching;

    void LateUpdate()
    {
        // Respuesta instantánea 1:1 sin delay en los dedos
        float dt = Mathf.Clamp01(Time.deltaTime * 45f);
        for (int i = 0; i < 5; i++)
        {
            curlActual[i] = Mathf.Lerp(curlActual[i], curlObjetivo[i], dt);
        }
        pinchActual = Mathf.Lerp(pinchActual, pinchObjetivo, dt);

        if (dedosRecibidos)
        {
            // Detección anatómica del símbolo de paz (índice y mayor extendidos, anular y meñique doblados)
            bool pazDedos = (curlActual[1] < 0.48f && curlActual[2] < 0.48f && curlActual[3] > 0.42f && curlActual[4] > 0.42f);
            EsSimboloDePaz = OSCHandReceiver.IsPeaceSign || pazDedos;

            // Detección clara de pulgar para arriba (4 dedos cerrados en puño, pulgar estirado)
            bool pulgarDedos = (!EsSimboloDePaz && curlActual[0] < 0.38f && curlActual[1] > 0.60f && curlActual[2] > 0.60f && curlActual[3] > 0.60f && curlActual[4] > 0.60f);
            EsPulgarArriba = OSCHandReceiver.EsPulgarArriba || pulgarDedos;

            // Detección clara de puño cerrado (todos los dedos flexionados tight, pulgar recogido)
            bool punoDedos = (!EsSimboloDePaz && !EsPulgarArriba && curlActual[0] > 0.50f && curlActual[1] > 0.65f && curlActual[2] > 0.65f && curlActual[3] > 0.65f && curlActual[4] > 0.65f);
            EsPunoCerrado = OSCHandReceiver.EsPunoCerrado || punoDedos;
        }
        else
        {
            // Reposo absoluto por defecto: el jugador NO se mueve solo
            EsSimboloDePaz = OSCHandReceiver.IsPeaceSign;
            EsPulgarArriba = OSCHandReceiver.EsPulgarArriba;
            EsPunoCerrado = OSCHandReceiver.EsPunoCerrado;
        }

        if (usandoModeloRiggeado)
        {
            AnimarManoRiggeada();
        }
        else
        {
            AnimarManoProcedural();
        }
    }

    private void ConstruirManoArticulada3D()
    {
        Transform root = new GameObject("VRHand_ProceduralRoot").transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        root.localScale = Vector3.one * 0.42f;

        Material matBaseMano = Resources.Load<Material>("Mat_GreteBN");
        if (matBaseMano != null)
        {
            materialMano = new Material(matBaseMano);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader != null) materialMano = new Material(shader);
        }
        if (materialMano != null) materialMano.color = colorManoAbierta;

        CrearPieza(root, "Palma", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.85f, 0.22f, 0.95f));
        CrearPieza(root, "Muneca", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, -0.58f), new Vector3(0.55f, 0.22f, 0.35f), Quaternion.Euler(90f, 0f, 0f));

        Vector3[] basesDedos = new Vector3[]
        {
            new Vector3(-0.48f, 0.02f, -0.10f),
            new Vector3(-0.30f, 0.02f,  0.48f),
            new Vector3(-0.08f, 0.03f,  0.52f),
            new Vector3( 0.14f, 0.02f,  0.49f),
            new Vector3( 0.34f, 0.00f,  0.42f)
        };

        float[] largosFalange = new float[] { 0.30f, 0.36f, 0.40f, 0.36f, 0.29f };
        float[] grosores = new float[] { 0.19f, 0.16f, 0.165f, 0.155f, 0.14f };

        for (int d = 0; d < 5; d++)
        {
            Transform parentActual = root;
            Vector3 posLocal = basesDedos[d];
            float largoBase = largosFalange[d];
            float grosor = grosores[d];

            for (int f = 0; f < 3; f++)
            {
                float factorLargo = (f == 0) ? 1.0f : (f == 1) ? 0.8f : 0.68f;
                float largoSeg = largoBase * factorLargo;
                float grosorSeg = grosor * (1f - f * 0.1f);

                GameObject pivote = new GameObject($"Dedo_{d}_Falange_{f}");
                pivote.transform.SetParent(parentActual, false);
                pivote.transform.localPosition = posLocal;

                if (d == 0 && f == 0)
                {
                    pivote.transform.localRotation = Quaternion.Euler(10f, -38f, -20f);
                }
                else
                {
                    pivote.transform.localRotation = Quaternion.identity;
                }

                CrearPieza(pivote.transform, "Nudillo", PrimitiveType.Sphere, Vector3.zero, Vector3.one * (grosorSeg * 1.15f));
                CrearPieza(
                    pivote.transform,
                    "Segmento",
                    PrimitiveType.Capsule,
                    new Vector3(0f, 0f, largoSeg * 0.5f),
                    new Vector3(grosorSeg, largoSeg * 0.55f, grosorSeg),
                    Quaternion.Euler(90f, 0f, 0f)
                );

                articulacionesProcedurales[d, f] = pivote.transform;
                parentActual = pivote.transform;
                posLocal = new Vector3(0f, 0f, largoSeg);
            }
        }
    }

    private GameObject CrearPieza(Transform parent, string nombre, PrimitiveType tipo, Vector3 localPos, Vector3 localScale, Quaternion? localRot = null)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot ?? Quaternion.identity;
        go.transform.localScale = localScale;

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null && materialMano != null)
        {
            rend.sharedMaterial = materialMano;
            renderersProcedurales.Add(rend);
        }
        return go;
    }

    private void AnimarManoProcedural()
    {
        if (articulacionesProcedurales[0, 0] == null) return;

        if (materialMano != null)
        {
            Color c = Color.Lerp(colorManoAbierta, colorManoPinza, OSCHandReceiver.IsPinching ? 1f : pinchActual * 0.7f);
            materialMano.color = c;
        }

        float thumbFlex = Mathf.Max(curlActual[0], pinchActual);
        articulacionesProcedurales[0, 0].localRotation = Quaternion.Euler(
            Mathf.Lerp(10f, 38f, thumbFlex),
            Mathf.Lerp(-38f, 22f, thumbFlex),
            Mathf.Lerp(-20f, -5f, thumbFlex)
        );
        articulacionesProcedurales[0, 1].localRotation = Quaternion.Euler(Mathf.Lerp(5f, 35f, thumbFlex), Mathf.Lerp(0f, 25f, thumbFlex), 0f);
        articulacionesProcedurales[0, 2].localRotation = Quaternion.Euler(Mathf.Lerp(5f, 40f, thumbFlex), Mathf.Lerp(0f, 15f, thumbFlex), 0f);

        float indexFlex = Mathf.Max(curlActual[1], pinchActual * 0.85f);
        articulacionesProcedurales[1, 0].localRotation = Quaternion.Euler(Mathf.Lerp(4f, 55f, indexFlex), Mathf.Lerp(-3f, -10f, pinchActual), 0f);
        articulacionesProcedurales[1, 1].localRotation = Quaternion.Euler(Mathf.Lerp(6f, 65f, indexFlex), 0f, 0f);
        articulacionesProcedurales[1, 2].localRotation = Quaternion.Euler(Mathf.Lerp(4f, 45f, indexFlex), 0f, 0f);

        for (int d = 2; d < 5; d++)
        {
            float flex = curlActual[d];
            articulacionesProcedurales[d, 0].localRotation = Quaternion.Euler(Mathf.Lerp(5f, 75f, flex), 0f, 0f);
            articulacionesProcedurales[d, 1].localRotation = Quaternion.Euler(Mathf.Lerp(8f, 85f, flex), 0f, 0f);
            articulacionesProcedurales[d, 2].localRotation = Quaternion.Euler(Mathf.Lerp(5f, 65f, flex), 0f, 0f);
        }
    }

    private void ConfigurarManoRiggeada(Transform rigRoot)
    {
        // CRÍTICO: Desactivar el Animator del prefab (VR Hands_Idle.anim) que estaba pisando
        // las posiciones y rotaciones de nuestros dedos en cada frame
        Animator[] animators = rigRoot.GetComponentsInChildren<Animator>(true);
        foreach (Animator anim in animators)
        {
            anim.enabled = false;
        }

        for (int i = 0; i < 5; i++)
        {
            huesosRiggeados[i] = new List<Transform>();
            rotacionesBaseRig[i] = new List<Quaternion>();
        }

        // Tamaño 3.3x: más pequeña, compacta y estilizada frente a la vista Cardboard
        rigRoot.localPosition = Vector3.zero;
        rigRoot.localRotation = Quaternion.identity;
        rigRoot.localScale = Vector3.one * 3.3f;

        Transform rootBone = rigRoot.Find("Root");
        if (rootBone != null)
        {
            rootBone.localPosition = Vector3.zero;
            rootBone.localRotation = Quaternion.identity;
        }

        // Ocultar la mano izquierda del prefab
        Transform jLeft = rigRoot.Find("Root/J_Left");
        if (jLeft != null) jLeft.localScale = Vector3.zero;

        // Centrar la mano derecha de frente hacia la vista para ver los 5 dedos con postura y movilidad natural
        Transform jRight = rigRoot.Find("Root/J_Right");
        if (jRight != null)
        {
            jRight.localPosition = new Vector3(0f, -0.04f, 0f);
            jRight.localRotation = Quaternion.Euler(-10f, -6f, 0f) * Quaternion.Euler(180f, 0f, 0f);
        }

        Transform searchRoot = rigRoot.Find("Root/J_Right/J_Right_Hand");
        if (searchRoot == null) searchRoot = rigRoot;
        else searchRoot.localRotation = Quaternion.identity;

        Transform[] todos = searchRoot.GetComponentsInChildren<Transform>(true);
        string[][] patrones = new string[][]
        {
            new string[] { "thumb1", "thumb2", "thumb3", "pulgar" },
            new string[] { "index1", "index2", "index3", "indice" },
            new string[] { "middle1", "middle2", "middle3", "mayor", "medio" },
            new string[] { "ring1", "ring2", "ring3", "anular" },
            new string[] { "pinky1", "pinky2", "pinky3", "little", "menique" }
        };

        foreach (Transform t in todos)
        {
            if (t == searchRoot) continue;
            string n = t.name.ToLowerInvariant();
            if (n.EndsWith("4") || n.Contains("end") || n.Contains("tip") || n.Contains(" nub")) continue;

            for (int d = 0; d < 5; d++)
            {
                foreach (string p in patrones[d])
                {
                    if (n.Contains(p))
                    {
                        huesosRiggeados[d].Add(t);
                        rotacionesBaseRig[d].Add(t.localRotation);
                        break;
                    }
                }
            }
        }

        // Guardar los renderers para iluminar la mano cuando toque el ojo de escape o haga pinza
        renderersRiggeados = rigRoot.GetComponentsInChildren<Renderer>(true);
        coloresOriginalesRig = new Color[renderersRiggeados.Length];
        for (int i = 0; i < renderersRiggeados.Length; i++)
        {
            if (renderersRiggeados[i] != null && renderersRiggeados[i].material != null && renderersRiggeados[i].material.HasProperty("_BaseColor"))
            {
                coloresOriginalesRig[i] = renderersRiggeados[i].material.GetColor("_BaseColor");
            }
            else if (renderersRiggeados[i] != null && renderersRiggeados[i].material != null)
            {
                coloresOriginalesRig[i] = renderersRiggeados[i].material.color;
            }
        }

        int totalHuesos = 0;
        for (int d = 0; d < 5; d++) totalHuesos += huesosRiggeados[d].Count;

        if (totalHuesos > 0)
        {
            usandoModeloRiggeado = true;
            Debug.Log($"[VRHandVisual] Free Pack - VR Hands conectado sin Animator ({totalHuesos} articulaciones en control directo, escala 9x).");
        }
        else
        {
            ConstruirManoArticulada3D();
        }
    }

    private void AnimarManoRiggeada()
    {
        // Feedback visual claro en la mano:
        // - Color natural normal
        // - Se ilumina en tono celeste/dorado cuando estás tocando el ojo de escape ("colapso con el ojo escape")
        // - Se ilumina en verde intenso al cerrar la pinza
        if (renderersRiggeados != null)
        {
            for (int i = 0; i < renderersRiggeados.Length; i++)
            {
                Renderer r = renderersRiggeados[i];
                if (r == null || r.material == null) continue;
                Color baseCol = (i < coloresOriginalesRig.Length) ? coloresOriginalesRig[i] : Color.white;
                Color targetCol = baseCol;

                if (EsSimboloDePaz)
                {
                    // Dorado resplandeciente cuando estás haciendo el Símbolo de Paz
                    targetCol = Color.Lerp(baseCol, new Color(1.0f, 0.90f, 0.25f, 1f), 0.70f);
                }
                else if (EscapeOjo.ManoSobreElOjo)
                {
                    targetCol = Color.Lerp(baseCol, new Color(0.4f, 0.9f, 1.0f, 1f), 0.55f);
                }
                else if (OSCHandReceiver.IsPinching)
                {
                    targetCol = Color.Lerp(baseCol, new Color(0.3f, 1.0f, 0.5f, 1f), 0.65f);
                }

                if (r.material.HasProperty("_BaseColor"))
                    r.material.SetColor("_BaseColor", targetCol);
                else
                    r.material.color = targetCol;
            }
        }

        // 0: Pulgar (gran movilidad: abre amplio en reposo y se flexiona hacia la palma y el índice)
        float thumbFlex = Mathf.Max(curlActual[0], pinchActual);
        for (int j = 0; j < huesosRiggeados[0].Count; j++)
        {
            Transform h = huesosRiggeados[0][j];
            if (h == null) continue;
            if (j == 0)
            {
                float pitch = Mathf.Lerp(-15f, 42f, thumbFlex);
                float yaw   = Mathf.Lerp(-15f, 32f, thumbFlex);
                float roll  = Mathf.Lerp(12f, -24f, thumbFlex);
                h.localRotation = rotacionesBaseRig[0][j] * Quaternion.Euler(pitch, yaw, roll);
            }
            else
            {
                float flexAngle = Mathf.Lerp(-10f, 65f, thumbFlex);
                h.localRotation = rotacionesBaseRig[0][j] * Quaternion.Euler(flexAngle, 0f, 0f);
            }
        }

        // 1: Índice (control individual ultra-sensible con articulación de 3 falanges)
        float indexFlex = Mathf.Max(curlActual[1], pinchActual * 0.95f);
        float[] angulosIndice = new float[] { 70f, 85f, 60f };
        for (int j = 0; j < huesosRiggeados[1].Count; j++)
        {
            Transform h = huesosRiggeados[1][j];
            if (h == null) continue;
            float maxAng = (j < angulosIndice.Length) ? angulosIndice[j] : 65f;
            float ang = Mathf.Lerp(-15f, maxAng, indexFlex);
            // Sutil apertura lateral al extender el dedo
            float spread = (j == 0) ? Mathf.Lerp(4f, 0f, indexFlex) : 0f;
            h.localRotation = rotacionesBaseRig[1][j] * Quaternion.Euler(ang, spread, 0f);
        }

        // 2, 3, 4: Mayor, Anular y Meñique (control 1:1 completamente independiente para cada dedo)
        float[][] angulosPorDedo = new float[][] {
            null,
            null,
            new float[] { 72f, 88f, 62f }, // Mayor
            new float[] { 74f, 90f, 65f }, // Anular
            new float[] { 78f, 92f, 70f }  // Meñique
        };

        float[] spreadPorDedo = new float[] { 0f, 0f, 0f, -4f, -10f }; // Apertura en abanico natural

        for (int d = 2; d < 5; d++)
        {
            float flex = curlActual[d];
            float[] configDedo = angulosPorDedo[d];
            float spreadBase = spreadPorDedo[d];

            for (int j = 0; j < huesosRiggeados[d].Count; j++)
            {
                Transform hueso = huesosRiggeados[d][j];
                if (hueso != null)
                {
                    float maxAng = (j < configDedo.Length) ? configDedo[j] : 70f;
                    float ang = Mathf.Lerp(-14f, maxAng, flex);
                    float spread = (j == 0) ? Mathf.Lerp(spreadBase, 0f, flex) : 0f;
                    hueso.localRotation = rotacionesBaseRig[d][j] * Quaternion.Euler(ang, spread, 0f);
                }
            }
        }
    }
}

