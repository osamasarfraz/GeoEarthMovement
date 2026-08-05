using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Inspector test panel for GeoOrbitalDirector.
/// Works in edit mode (snap + manual preview push) and in play mode (animated fly).
/// </summary>
[CustomEditor(typeof(GeoOrbitalDirector))]
public class GeoOrbitalDirectorEditor : Editor
{
    static readonly string[] CityNames =
    {
        "London", "Karachi", "New York", "Tokyo",
        "Sydney", "Cape Town", "Rio", "Dubai",
        "Reykjavik", "Singapore", "Anchorage", "Null Island"
    };
    static readonly double[] CityLat =
    { 51.5074, 24.8607, 40.7128, 35.6762, -33.8688, -33.9249, -22.9068, 25.2048, 64.1466, 1.3521, 61.2181, 0.0 };
    static readonly double[] CityLon =
    { -0.1278, 67.0011, -74.0060, 139.6503, 151.2093, 18.4241, -43.1729, 55.2708, -21.9426, 103.8198, -149.9003, 0.0 };

    SerializedProperty pLat, pLon;

    void OnEnable()
    {
        pLat = serializedObject.FindProperty("testLat");
        pLon = serializedObject.FindProperty("testLon");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var dir = (GeoOrbitalDirector)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Coordinate Test Panel", EditorStyles.boldLabel);

        if (dir.Globe == null)
        {
            EditorGUILayout.HelpBox("No GeoSphere assigned or found in the scene.", MessageType.Warning);
            return;
        }

        serializedObject.Update();
        EditorGUILayout.PropertyField(pLat, new GUIContent("Latitude"));
        EditorGUILayout.PropertyField(pLon, new GUIContent("Longitude"));
        serializedObject.ApplyModifiedProperties();

        double lat = pLat.doubleValue;
        double lon = pLon.doubleValue;

        // live readout of the axis values this coordinate resolves to
        float h, v;
        if (dir.TryGetAxes(lat, lon, out h, out v))
        {
            EditorGUILayout.LabelField("Resolves to",
                string.Format("H {0:F2}째    V {1:F2}째", h, v));
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap"))
        {
            dir.SnapTo(lat, lon);
            PushPreview(dir);
        }
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button(Application.isPlaying ? "Fly" : "Fly (play mode only)"))
                dir.GoTo(lat, lon);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Presets", EditorStyles.miniBoldLabel);

        int perRow = 3;
        for (int i = 0; i < CityNames.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + perRow, CityNames.Length); j++)
            {
                if (GUILayout.Button(CityNames[j], EditorStyles.miniButton))
                {
                    pLat.doubleValue = CityLat[j];
                    pLon.doubleValue = CityLon[j];
                    serializedObject.ApplyModifiedProperties();

                    if (Application.isPlaying) dir.GoTo(CityLat[j], CityLon[j]);
                    else { dir.SnapTo(CityLat[j], CityLon[j]); PushPreview(dir); }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox(
                "Edit mode: Cinemachine does not tick, so the preview is pushed onto the Main Camera manually. " +
                "Enter play mode for real damping and animated flights.", MessageType.Info);
    }

    /// <summary>
    /// Cinemachine's brain doesn't run in edit mode, so evaluate the vcam and
    /// place the Main Camera ourselves to get a live preview.
    /// </summary>
    static void PushPreview(GeoOrbitalDirector dir)
    {
        if (Application.isPlaying) return;

        var vcam = dir.GetComponent<CinemachineCamera>();
        if (vcam == null) return;

        vcam.InternalUpdateCameraState(Vector3.up, -1f);
        var state = vcam.State;

        var cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam.transform, "Geo preview");
            cam.transform.position = state.GetFinalPosition();
            cam.transform.rotation = state.GetFinalOrientation();
        }

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
