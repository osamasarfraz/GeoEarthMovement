using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Drives a CinemachineOrbitalFollow (Sphere orbit style, World Space binding) so the camera
/// swings around to face a real-world lat/lon on the Earth sphere, then zooms in.
///
/// Axis maths, verified against this rig:
///   cameraOffset = Quaternion.Euler(Vertical, Horizontal, 0) * (0, 0, -Radius * Radial)
/// so for a desired outward direction d (unit, world space):
///   Vertical   = asin(d.y)
///   Horizontal = atan2(-d.x, -d.z)
/// </summary>
[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class GeoOrbitalDirector : MonoBehaviour
{
    [Header("References")]
    public GeoSphere globe;

    [Header("Zoom (multiplies OrbitalFollow.Radius)")]
    [Tooltip("Radial value when idle / fully pulled back.")]
    public float farRadial = 1.0f;
    [Tooltip("Radial value once the camera has arrived over the target.")]
    public float nearRadial = 0.42f;
    [Tooltip("Extra pull-back during the swing, so the camera arcs out and back in.")]
    public float arcPullBack = 0.25f;

    [Header("Flight")]
    public float flyDuration = 2.2f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Limits")]
    [Tooltip("Clamp latitude reach. 90 is the pole, where the orbit gimbal-locks.")]
    [Range(45f, 89f)] public float maxVerticalDeg = 85f;

    [Header("Idle")]
    public float idleSpinDegPerSec = 0f;

    [Header("Debug / test")]
    public bool logTargets = true;
    public double testLat = 51.5074;   // London
    public double testLon = -0.1278;
    public KeyCode testKey = KeyCode.T;

    CinemachineOrbitalFollow orbit;
    Coroutine flight;
    bool hasTarget;
    double curLat, curLon;

    public double CurrentLat { get { return curLat; } }
    public double CurrentLon { get { return curLon; } }

    /// <summary>Lazily resolved so editor tooling works outside play mode.</summary>
    public CinemachineOrbitalFollow Orbit
    {
        get { if (orbit == null) orbit = GetComponent<CinemachineOrbitalFollow>(); return orbit; }
    }

    public GeoSphere Globe
    {
        get { if (globe == null) globe = FindAnyObjectByType<GeoSphere>(); return globe; }
    }

    void Awake()
    {
        orbit = GetComponent<CinemachineOrbitalFollow>();
        if (globe == null) globe = FindAnyObjectByType<GeoSphere>();
    }

    /// <summary>Compute the orbital axis values that put the camera over this lat/lon.</summary>
    public bool TryGetAxes(double lat, double lon, out float horizontal, out float vertical)
    {
        horizontal = vertical = 0f;
        if (Globe == null) return false;

        Vector3 d = Globe.GeoUpWorld(lat, lon);
        vertical = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg,
                               -maxVerticalDeg, maxVerticalDeg);
        horizontal = Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg;
        return true;
    }

    /// <summary>Snap instantly, no animation. Useful for calibration.</summary>
    public void SnapTo(double lat, double lon)
    {
        float h, v;
        if (!TryGetAxes(lat, lon, out h, out v)) return;
        if (flight != null) { StopCoroutine(flight); flight = null; }
        Orbit.HorizontalAxis.Value = h;
        Orbit.VerticalAxis.Value = v;
        Orbit.RadialAxis.Value = nearRadial;
        curLat = lat; curLon = lon; hasTarget = true;
    }

    /// <summary>Fly the camera round to this lat/lon.</summary>
    public void GoTo(double lat, double lon)
    {
        float h, v;
        if (!TryGetAxes(lat, lon, out h, out v)) return;

        curLat = lat; curLon = lon; hasTarget = true;
        if (logTargets)
            Debug.Log(string.Format("[GeoOrbitalDirector] {0:F4}, {1:F4} -> H={2:F2} V={3:F2}", lat, lon, h, v));

        if (flight != null) StopCoroutine(flight);
        flight = StartCoroutine(Fly(h, v));
    }

    IEnumerator Fly(float targetH, float targetV)
    {
        float h0 = Orbit.HorizontalAxis.Value;
        float v0 = Orbit.VerticalAxis.Value;
        float r0 = Orbit.RadialAxis.Value;

        // Shortest way round the globe rather than the long way through 180.
        float dH = Mathf.DeltaAngle(h0, targetH);

        float t = 0f;
        while (t < 1f)
        {
            t = flyDuration <= 0f ? 1f : Mathf.Min(1f, t + Time.deltaTime / flyDuration);
            float e = ease.Evaluate(t);

            Orbit.HorizontalAxis.Value = h0 + dH * e;
            Orbit.VerticalAxis.Value = Mathf.Lerp(v0, targetV, e);
            Orbit.RadialAxis.Value = Mathf.Lerp(r0, nearRadial, e)
                                   + Mathf.Sin(t * Mathf.PI) * arcPullBack;
            yield return null;
        }

        Orbit.HorizontalAxis.Value = targetH;
        Orbit.VerticalAxis.Value = targetV;
        Orbit.RadialAxis.Value = nearRadial;
        flight = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(testKey)) GoTo(testLat, testLon);

        if (flight == null && hasTarget && idleSpinDegPerSec != 0f)
            Orbit.HorizontalAxis.Value += idleSpinDegPerSec * Time.deltaTime;
    }
}
