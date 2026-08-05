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

    [Header("UDP loopback test")]
    public int port = 9000;

    static readonly string[] CityNames = { "London", "Karachi", "New York", "Tokyo", "Sydney", "Cape Town" };
    static readonly double[] CityLat = { 51.5074, 24.8607, 40.7128, 35.6762, -33.8688, -33.9249 };
    static readonly double[] CityLon = { -0.1278, 67.0011, -74.0060, 139.6503, 151.2093, 18.4241 };

    bool show;
    string latStr = "51.5074";
    string lonStr = "-0.1278";
    string status = "";
    Rect win = new Rect(12, 12, 320, 300);

    void Awake()
    {
        show = visibleOnStart;
        if (director == null) director = FindAnyObjectByType<GeoOrbitalDirector>();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) show = !show;
    }

    void OnGUI()
    {
        if (!show) return;
        win = GUILayout.Window(9137, win, DrawWindow, "Geo Test  (" + toggleKey + " to hide)");
    }

    void DrawWindow(int id)
    {
        if (director == null)
        {
            GUILayout.Label("No GeoOrbitalDirector found.");
            GUI.DragWindow();
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Lat", GUILayout.Width(30));
        latStr = GUILayout.TextField(latStr);
        GUILayout.Label("Lon", GUILayout.Width(30));
        lonStr = GUILayout.TextField(lonStr);
        GUILayout.EndHorizontal();

        double lat = 0.0, lon = 0.0;
        bool parsed = double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                   && double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lon);

        if (parsed)
        {
            float h, v;
            if (director.TryGetAxes(lat, lon, out h, out v))
                GUILayout.Label(string.Format("axes  H {0:F2}   V {1:F2}", h, v));
        }
        else GUILayout.Label("<invalid numbers>");

        GUI.enabled = parsed;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fly")) director.GoTo(lat, lon);
        if (GUILayout.Button("Snap")) director.SnapTo(lat, lon);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Send as UDP -> 127.0.0.1:" + port)) SendLoopback(lat, lon);
        GUI.enabled = true;

        GUILayout.Space(6);
        for (int i = 0; i < CityNames.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 2, CityNames.Length); j++)
            {
                if (GUILayout.Button(CityNames[j]))
                {
                    latStr = CityLat[j].ToString(CultureInfo.InvariantCulture);
                    lonStr = CityLon[j].ToString(CultureInfo.InvariantCulture);
                    director.GoTo(CityLat[j], CityLon[j]);
                }
            }
            GUILayout.EndHorizontal();
        }

        if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
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
                status = "sent \"" + msg + "\"";
            }
        }
        catch (System.Exception e)
        {
            status = "send failed: " + e.Message;
        }
    }
}
