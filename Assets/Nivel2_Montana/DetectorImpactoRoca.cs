using UnityEngine;

/// <summary>
/// Detector de impactos físicos para la roca de Sísifo rodando por la pendiente.
/// Emite audio 3D de choque al colisionar con la pendiente.
/// </summary>
public class DetectorImpactoRoca : MonoBehaviour
{
    public AudioClip clipImpacto;
    private AudioSource audioSrc;
    private float tiempoUltimoImpacto = 0f;

    void Awake()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 1.0f;
        audioSrc.minDistance = 1.0f;
        audioSrc.maxDistance = 18.0f;
        audioSrc.playOnAwake = false;
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.relativeVelocity.magnitude > 0.7f && Time.time - tiempoUltimoImpacto > 0.16f)
        {
            tiempoUltimoImpacto = Time.time;
            if (audioSrc != null && clipImpacto != null)
            {
                float volumen = Mathf.Clamp01(col.relativeVelocity.magnitude * 0.18f);
                audioSrc.pitch = Random.Range(0.8f, 1.15f);
                audioSrc.PlayOneShot(clipImpacto, volumen);
            }
        }
    }
}
