#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Declares the exact Android manifest permissions and optional hardware features required for Online play, LAN/Hotspot discovery
/// (ConnectivityManager, WifiManager, LocalOnlyHotspot, WifiNetworkSpecifier, NEARBY_WIFI_DEVICES on API 33+, coarse/fine location
/// capped at API 32), haptics, and QR/photo camera scan (.github/scripts/inspect-apk.sh &amp; android/kalyeah_local/src/main/AndroidManifest.xml).
/// </summary>
public sealed class KalyeahAndroidManifest : IPostGenerateGradleAndroidProject
{
    const string AndroidNs = "http://schemas.android.com/apk/res/android";

    public static readonly string[] Permissions =
    {
        "android.permission.INTERNET",
        "android.permission.VIBRATE",
        "android.permission.CHANGE_WIFI_MULTICAST_STATE",
        "android.permission.CAMERA",
        "android.permission.ACCESS_NETWORK_STATE",
        "android.permission.ACCESS_WIFI_STATE",
        "android.permission.CHANGE_WIFI_STATE",
        "android.permission.CHANGE_NETWORK_STATE",
        "android.permission.NEARBY_WIFI_DEVICES",
        "android.permission.ACCESS_COARSE_LOCATION",
        "android.permission.ACCESS_FINE_LOCATION",
    };

    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogError($"KalyeahAndroidManifest: no manifest at {manifestPath}");
            return;
        }
        var doc = new XmlDocument();
        doc.Load(manifestPath);
        XmlElement manifest = doc.DocumentElement;

        EnsurePermission(doc, manifest, "android.permission.INTERNET");
        EnsurePermission(doc, manifest, "android.permission.VIBRATE");
        EnsurePermission(doc, manifest, "android.permission.CHANGE_WIFI_MULTICAST_STATE");
        EnsurePermission(doc, manifest, "android.permission.CAMERA");
        EnsurePermission(doc, manifest, "android.permission.ACCESS_NETWORK_STATE");
        EnsurePermission(doc, manifest, "android.permission.ACCESS_WIFI_STATE");
        EnsurePermission(doc, manifest, "android.permission.CHANGE_WIFI_STATE");
        EnsurePermission(doc, manifest, "android.permission.CHANGE_NETWORK_STATE");
        EnsurePermission(doc, manifest, "android.permission.NEARBY_WIFI_DEVICES", "usesPermissionFlags", "neverForLocation");
        EnsurePermission(doc, manifest, "android.permission.ACCESS_COARSE_LOCATION", "maxSdkVersion", "32");
        EnsurePermission(doc, manifest, "android.permission.ACCESS_FINE_LOCATION", "maxSdkVersion", "32");

        EnsureFeature(doc, manifest, "android.hardware.camera", "false");
        EnsureFeature(doc, manifest, "android.hardware.wifi", "false");
        EnsureFeature(doc, manifest, "android.hardware.location", "false");
        doc.Save(manifestPath);
    }

    static void EnsurePermission(XmlDocument doc, XmlElement manifest, string name, string extraAttr = null, string extraValue = null)
    {
        XmlElement elem = FindElement(manifest, "uses-permission", name);
        if (elem == null)
        {
            elem = doc.CreateElement("uses-permission");
            elem.SetAttribute("name", AndroidNs, name);
            manifest.PrependChild(elem);
        }
        if (extraAttr != null && extraValue != null)
            elem.SetAttribute(extraAttr, AndroidNs, extraValue);
    }

    static void EnsureFeature(XmlDocument doc, XmlElement manifest, string name, string required)
    {
        XmlElement elem = FindElement(manifest, "uses-feature", name);
        if (elem == null)
        {
            elem = doc.CreateElement("uses-feature");
            elem.SetAttribute("name", AndroidNs, name);
            manifest.PrependChild(elem);
        }
        if (required != null)
            elem.SetAttribute("required", AndroidNs, required);
    }

    static XmlElement FindElement(XmlElement manifest, string tag, string name)
    {
        foreach (XmlNode node in manifest.ChildNodes)
            if (node is XmlElement element && element.Name == tag && element.GetAttribute("name", AndroidNs) == name)
                return element;
        return null;
    }
}
#endif
