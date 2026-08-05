using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// On-screen test panel. Works in play mode AND in a build, which is the point:
/// once you ship the .exe you can still verify coordinates without the sender app.
///
/// F1 toggles it.
///
/// "Send as UDP" pushes a real packet at 127.0.0.1:port, so it exercises the whole
/// receive path (socket -> background thread -> queue -> parse -> camera) rather
/// than shortcutting straight to the director.
/// </summary>
public class GeoDebugHUD : MonoBehaviour
{
    public GeoOrbitalDirector director;
    public KeyCode toggleKey = KeyCode.F1;
    public bool visibleOnStart = true;

    [Header("Size")]
    [Tooltip("Manual multiplier on top of the automatic screen-height scaling.")]
    [Range(0.6f, 3f)] public float uiScale = 1.4f;
    [Tooltip("Grow the panel on tall / hi-DPI displays so it stays readable.")]
    public bool autoScaleWithScreen = true;

    [Header("UDP loopback test")]
    public int port = 9000;

    static readonly string[] CityNames = { "London", "Karachi", "New York", "Tokyo", "Sydney", "Cape Town" };
    static readonly double[] CityLat = { 51.5074, 24.8607, 40.7128, 35.6762, -33.8688, -33.9249 };
    static readonly double[] CityLon = { -0.1278, 67.0011, -74.0060, 139.6503, 151.2093, 18.4241 };

    bool show;
    string latStr = "51.5074";
    string lonStr = "-0.1278";
    string status = "";
    Rect win;
    bool placed;

    // cached styles, rebuilt only when the scale changes
    float builtScale = -1f;
    GUIStyle sWindow, sLabel, sReadout, sField, sButton, sPreset, sStatus;
    float rowH, fieldH, pad;

    void Awake()
    {
        show = visibleOnStart;
        if (director == null) director = FindAnyObjectByType<GeoOrbitalDirector>();
    }

    void Update()
    {
        if (GeoInput.KeyDown(toggleKey)) show = !show;
    }

    float Scale
    {
        get
        {
            float auto = autoScaleWithScreen ? Mathf.Clamp(Screen.height / 900f, 1f, 2.5f) : 1f;
            return Mathf.Max(0.5f, uiScale * auto);
        }
    }

    void BuildStyles(float s)
    {
        int font = Mathf.RoundToInt(14f * s);
        rowH = 34f * s;
        fieldH = 30f * s;
        pad = 10f * s;

        sWindow = new GUIStyle(GUI.skin.window);
        sWindow.fontSize = Mathf.RoundToInt(15f * s);
        sWindow.fontStyle = FontStyle.Bold;
        sWindow.padding = new RectOffset((int)pad, (int)pad, Mathf.RoundToInt(28f * s), (int)pad);

        sLabel = new GUIStyle(GUI.skin.label);
        sLabel.fontSize = font;

        sReadout = new GUIStyle(GUI.skin.label);
        sReadout.fontSize = font;
        sReadout.fontStyle = FontStyle.Bold;

        sField = new GUIStyle(GUI.skin.textField);
        sField.fontSize = font;
        sField.alignment = TextAnchor.MiddleLeft;

        sButton = new GUIStyle(GUI.skin.button);
        sButton.fontSize = font;

        sPreset = new GUIStyle(GUI.skin.button);
        sPreset.fontSize = Mathf.RoundToInt(13f * s);

        sStatus = new GUIStyle(GUI.skin.label);
        sStatus.fontSize = Mathf.RoundToInt(12f * s);
        sStatus.wordWrap = true;

        float w = 400f * s;
        if (!placed) { win = new Rect(16f, 16f, w, 100f); placed = true; }
        else win.width = w;

        builtScale = s;
    }

    void OnGUI()
    {
        if (!show) return;

        float s = Scale;
        if (sWindow == null || !Mathf.Approximately(s, builtScale)) BuildStyles(s);

        win = GUILayout.Window(9137, win, DrawWindow,
                               "Geo Test   (" + toggleKey + " to hide)", sWindow,
                               GUILayout.Width(400f * s));

        win.x = Mathf.Clamp(win.x, 0f, Mathf.Max(0f, Screen.width - win.width));
        win.y = Mathf.Clamp(win.y, 0f, Mathf.Max(0f, Screen.height - 60f));
    }

    void DrawWindow(int id)
    {
        if (director == null)
        {
            GUILayout.Label("No GeoOrbitalDirector found.", sLabel);
            GUI.DragWindow();
            return;
        }

        float labW = 46f * builtScale;

        GUILayout.BeginHorizontal();
        GUILayout.Label("Lat", sLabel, GUILayout.Width(labW));
        latStr = GUILayout.TextField(latStr, sField, GUILayout.Height(fieldH));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Lon", sLabel, GUILayout.Width(labW));
        lonStr = GUILayout.TextField(lonStr, sField, GUILayout.Height(fieldH));
        GUILayout.EndHorizontal();

        double lat = 0.0, lon = 0.0;
        bool parsed = double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                   && double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lon);

        GUILayout.Space(4f * builtScale);
        if (parsed)
        {
            float h, v;
            if (director.TryGetAxes(lat, lon, out h, out v))
                GUILayout.Label(string.Format("axes    H {0:F2}    V {1:F2}", h, v), sReadout);
        }
        else GUILayout.Label("invalid numbers", sReadout);

        GUILayout.Space(6f * builtScale);
        GUI.enabled = parsed;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fly", sButton, GUILayout.Height(rowH))) director.GoTo(lat, lon);
        if (GUILayout.Button("Snap", sButton, GUILayout.Height(rowH))) director.SnapTo(lat, lon);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Send as UDP  ->  127.0.0.1:" + port, sButton, GUILayout.Height(rowH)))
            SendLoopback(lat, lon);

        GUI.enabled = true;

        GUILayout.Space(8f * builtScale);
        GUILayout.Label("Presets", sLabel);

        for (int i = 0; i < CityNames.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 2, CityNames.Length); j++)
            {
                if (GUILayout.Button(CityNames[j], sPreset, GUILayout.Height(rowH)))
                {
                    latStr = CityLat[j].ToString(CultureInfo.InvariantCulture);
                    lonStr = CityLon[j].ToString(CultureInfo.InvariantCulture);
                    director.GoTo(CityLat[j], CityLon[j]);
                }
            }
            GUILayout.EndHorizontal();
        }

        if (!string.IsNullOrEmpty(status))
        {
            GUILayout.Space(6f * builtScale);
            GUILayout.Label(status, sStatus);
        }

        GUI.DragWindow();
    }

    void SendLoopback(double lat, double lon)
    {
        try
        {
            using (var c = new UdpClient())
            {
                string msg = lat.ToString("F6", CultureInfo.InvariantCulture) + "," +
                             lon.ToString("F6", CultureInfo.InvariantCulture) + ",0,0";
                byte[] b = Encoding.ASCII.GetBytes(msg);
                c.Send(b, b.Length, new IPEndPoint(IPAddress.Loopback, port));
                status = "sent " + msg;
            }
        }
        catch (System.Exception e)
        {
            status = "send failed: " + e.Message;
        }
    }
}
