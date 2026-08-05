using UnityEngine;

/// <summary>
/// Attach to the Earth sphere. Converts lat/lon into directions and points on its surface.
/// Works through the transform, so the Earth's own rotation and scale are handled automatically.
///
/// Convention (before calibration):
///   lon 0   -> sphere's local +Z
///   lon +90 -> local +X
///   lat +90 -> local +Y (north pole)
/// Use longitudeOffsetDeg / flipLongitude to line this up with your texture.
/// </summary>
[ExecuteAlways]
public class GeoSphere : MonoBehaviour
{
    [Header("Texture calibration")]
    [Range(-180f, 180f)] public float longitudeOffsetDeg = 0f;
    public bool flipLongitude = false;

    /// <summary>World radius. Unity's primitive sphere mesh has radius 0.5 before scaling.</summary>
    public float Radius { get { return transform.lossyScale.x * 0.5f; } }

    public Vector3 GeoToLocalDir(double latDeg, double lonDeg)
    {
        double lon = (flipLongitude ? -lonDeg : lonDeg) + longitudeOffsetDeg;
        float phi = (float)(latDeg * Mathf.Deg2Rad);
        float lam = (float)(lon * Mathf.Deg2Rad);
        float cosPhi = Mathf.Cos(phi);
        return new Vector3(cosPhi * Mathf.Sin(lam), Mathf.Sin(phi), cosPhi * Mathf.Cos(lam));
    }

    public Vector3 GeoNorthLocalDir(double latDeg, double lonDeg)
    {
        double lon = (flipLongitude ? -lonDeg : lonDeg) + longitudeOffsetDeg;
        float phi = (float)(latDeg * Mathf.Deg2Rad);
        float lam = (float)(lon * Mathf.Deg2Rad);
        float s = Mathf.Sin(phi), c = Mathf.Cos(phi);
        return new Vector3(-s * Mathf.Sin(lam), c, -s * Mathf.Cos(lam)).normalized;
    }

    /// <summary>World-space outward surface normal at this lat/lon. This is what the camera aims along.</summary>
    public Vector3 GeoUpWorld(double latDeg, double lonDeg)
    {
        return transform.TransformDirection(GeoToLocalDir(latDeg, lonDeg)).normalized;
    }

    public Vector3 GeoNorthWorld(double latDeg, double lonDeg)
    {
        return transform.TransformDirection(GeoNorthLocalDir(latDeg, lonDeg)).normalized;
    }

    /// <summary>World point on (or above) the surface.</summary>
    public Vector3 GeoToWorld(double latDeg, double lonDeg, float heightUnits = 0f)
    {
        return transform.position + GeoUpWorld(latDeg, lonDeg) * (Radius + heightUnits);
    }

    /// <summary>Upright rotation on the surface, facing a compass heading (0 = north, 90 = east).</summary>
    public Quaternion GeoRotationWorld(double latDeg, double lonDeg, float headingDeg = 0f)
    {
        Vector3 up = GeoUpWorld(latDeg, lonDeg);
        Vector3 north = GeoNorthWorld(latDeg, lonDeg);
        return Quaternion.AngleAxis(headingDeg, up) * Quaternion.LookRotation(north, up);
    }

#if UNITY_EDITOR
    [Header("Calibration marker (scene view)")]
    public bool drawTestMarker = true;
    public double testLat = 51.5074;
    public double testLon = -0.1278;

    void OnDrawGizmos()
    {
        if (!drawTestMarker) return;
        Vector3 p = GeoToWorld(testLat, testLon);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(p, Radius * 0.03f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(p, p + GeoNorthWorld(testLat, testLon) * Radius * 0.2f);
    }
#endif
}
