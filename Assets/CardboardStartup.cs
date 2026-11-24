using System;
using Google.XR.Cardboard;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

public class CardboardStartup : MonoBehaviour
{
    #pragma warning disable 0414
    private TrackedPoseDriver trackedPoseDriver;
    private AttitudeSensor attitudeSensor;
    private bool nativeTrackingWorking = false;
    private bool trackingModeDetermined = false;
    private int checkFrameCount = 0;
    private const int FRAMES_TO_CHECK_NATIVE = 20;

    private float yawOffset = 0f;
    private bool autoRecentered = false;
    [System.NonSerialized]
    public float initialHeadingYaw = 0f;
    #pragma warning restore 0414

    void Awake()
    {
        var cam = GetComponent<Camera>();
        var uacd = GetComponent<UniversalAdditionalCameraData>();
#if UNITY_EDITOR
        if (cam != null) cam.stereoTargetEye = StereoTargetEyeMask.None;
        if (uacd != null) uacd.allowXRRendering = false;
#else
        if (cam != null) cam.stereoTargetEye = StereoTargetEyeMask.Both;
        if (uacd != null) uacd.allowXRRendering = true;
        // Desactivar consola de desarrollo y overlays de error en la vista estereoscópica
        Debug.developerConsoleVisible = false;
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
#endif

        trackedPoseDriver = GetComponent<TrackedPoseDriver>();
        if (trackedPoseDriver != null)
        {
            trackedPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
        }
        InitAttitudeSensor();
    }

    void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.brightness = 1.0f;
        InitAttitudeSensor();
    }

    private void InitAttitudeSensor()
    {
        try
        {
            if (attitudeSensor == null)
            {
                attitudeSensor = AttitudeSensor.current;
            }

            if (attitudeSensor != null && !attitudeSensor.enabled)
            {
                InputSystem.EnableDevice(attitudeSensor);
                Debug.Log("[CardboardTracking] AttitudeSensor habilitado.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CardboardTracking] Error habilitando sensor: {ex.Message}");
        }
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (Api.IsGearButtonPressed)
            {
                Api.ScanDeviceParams();
            }

            if (Api.IsCloseButtonPressed)
            {
                Application.Quit();
            }

            if (Api.HasNewDeviceParams())
            {
                Api.ReloadDeviceParams();
            }

            Api.UpdateScreenParams();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CardboardStartup] Api exception: " + ex.Message);
        }
#endif

        try
        {
            UpdateTracking();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CardboardStartup] UpdateTracking exception: " + ex.Message);
        }
    }

    private void UpdateTracking()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (attitudeSensor == null || !attitudeSensor.enabled)
        {
            InitAttitudeSensor();
        }

        // Evaluar durante los primeros frames si Cardboard nativo entrega rotación válida
        if (!trackingModeDetermined)
        {
            checkFrameCount++;
            var xrDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (xrDevice.isValid && xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion xrRot))
            {
                if (xrRot != Quaternion.identity && (Mathf.Abs(xrRot.x) > 0.001f || Mathf.Abs(xrRot.y) > 0.001f || Mathf.Abs(xrRot.z) > 0.001f))
                {
                    nativeTrackingWorking = true;
                    trackingModeDetermined = true;
                    Debug.Log("[CardboardTracking] Cardboard XR nativo ACTIVO (Hardware Gyro detected).");
                }
            }

            if (checkFrameCount >= FRAMES_TO_CHECK_NATIVE && !trackingModeDetermined)
            {
                trackingModeDetermined = true;
                nativeTrackingWorking = false;
                Debug.Log("[CardboardTracking] Cardboard XR sin rotación nativa. Activando Hybrid Fallback (AttitudeSensor).");

                if (trackedPoseDriver != null)
                {
                    trackedPoseDriver.enabled = false;
                }
            }
        }

        // Modo fallback para dispositivos sin giroscopio de hardware (Redmi Note 11, etc.)
        if (trackingModeDetermined && !nativeTrackingWorking)
        {
            if (trackedPoseDriver != null && trackedPoseDriver.enabled)
            {
                trackedPoseDriver.enabled = false;
            }

            if (attitudeSensor != null && attitudeSensor.enabled)
            {
                Quaternion rawAtt = attitudeSensor.attitude.ReadValue();

                if (rawAtt.w != 0f || rawAtt.x != 0f || rawAtt.y != 0f || rawAtt.z != 0f)
                {
                    Quaternion converted = GetConvertedRotation(rawAtt);

                    if (!autoRecentered)
                    {
                        RecenterYaw(converted);
                        autoRecentered = true;
                    }

                    transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f) * converted;
                }
            }
        }
#endif
    }

    private Quaternion GetConvertedRotation(Quaternion rawAtt)
    {
        // Conversión canónica de AttitudeSensor en Android (marco derecho ENU con Z hacia el cielo,
        // ya compensado a LandscapeLeft por CompensateRotationProcessor) al marco izquierdo de Unity (Y arriba, Z adelante):
        return Quaternion.Euler(90f, 0f, 0f) * new Quaternion(rawAtt.x, rawAtt.y, -rawAtt.z, -rawAtt.w);
    }

    public void RecenterAll()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Api.Recenter();

        if (attitudeSensor != null && attitudeSensor.enabled)
        {
            Quaternion rawAtt = attitudeSensor.attitude.ReadValue();
            Quaternion converted = GetConvertedRotation(rawAtt);
            RecenterYaw(converted);
        }
#endif
    }

    private void RecenterYaw(Quaternion converted)
    {
        Vector3 fwd = converted * Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.001f)
        {
            float currentYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            yawOffset = -currentYaw + initialHeadingYaw;
            Debug.Log($"[CardboardTracking] Recentered Yaw. Offset: {yawOffset:F1}° (InitialHeading: {initialHeadingYaw}°)");
        }
    }

    void LateUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (nativeTrackingWorking && initialHeadingYaw != 0f)
        {
            transform.localRotation = Quaternion.Euler(0f, initialHeadingYaw, 0f) * transform.localRotation;
        }
#endif
    }
}
