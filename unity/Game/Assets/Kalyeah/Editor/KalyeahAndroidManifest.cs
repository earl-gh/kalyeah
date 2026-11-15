using System;
using System.IO;
using System.Xml;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

/// <summary>
/// Declares the exact 12 Android manifest permissions, optional hardware features, backup policy, and NFC HCE ShareService
/// required for Online play, Local Hotspot rooms (LocalOnlyHotspot + WifiNetworkSpecifier + NFC Tap-to-Join), haptics, and
/// QR/ID camera scan (.github/scripts/inspect-apk.sh &amp; Assets/Plugins/Android/kalyeah_local.androidlib/AndroidManifest.xml).
/// </summary>
public sealed class KalyeahAndroidManifest
#if UNITY_ANDROID
    : IPostGenerateGradleAndroidProject
#endif
{
    public const string AndroidNs = "http://schemas.android.com/apk/res/android";

    /// <summary>Sorted canonical permission set enforced by .github/scripts/inspect-apk.sh.</summary>
    public static readonly string[] Permissions =
    {
        "android.permission.ACCESS_COARSE_LOCATION",
        "android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_NETWORK_STATE",
        "android.permission.ACCESS_WIFI_STATE",
        "android.permission.CAMERA",
        "android.permission.CHANGE_NETWORK_STATE",
        "android.permission.CHANGE_WIFI_MULTICAST_STATE",
        "android.permission.CHANGE_WIFI_STATE",
        "android.permission.INTERNET",
        "android.permission.NEARBY_WIFI_DEVICES",
        "android.permission.NFC",
        "android.permission.VIBRATE",
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
        ApplyToDocument(doc);
        doc.Save(manifestPath);
    }

    /// <summary>Applies Kalyeah's exact Android permissions, optional hardware features, allowBackup=false, and NFC HCE service to <paramref name="doc"/>.</summary>
    public static void ApplyToDocument(XmlDocument doc)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        XmlElement manifest = doc.DocumentElement;
        if (manifest == null)
        {
            manifest = doc.CreateElement("manifest");
            manifest.SetAttribute("xmlns:android", AndroidNs);
            doc.AppendChild(manifest);
        }

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
        EnsurePermission(doc, manifest, "android.permission.NFC");

        EnsureFeature(doc, manifest, "android.hardware.camera", "false");
        EnsureFeature(doc, manifest, "android.hardware.wifi", "false");
        EnsureFeature(doc, manifest, "android.hardware.location", "false");
        EnsureFeature(doc, manifest, "android.hardware.nfc", "false");
        EnsureFeature(doc, manifest, "android.hardware.nfc.hce", "false");

        XmlElement application = FindFirstChildElement(manifest, "application");
        if (application == null)
        {
            application = doc.CreateElement("application");
            manifest.AppendChild(application);
        }
        application.SetAttribute("allowBackup", AndroidNs, "false");
        EnsureShareService(doc, application);
    }

    static void EnsurePermission(XmlDocument doc, XmlElement manifest, string name, string extraAttr = null, string extraValue = null)
    {
        XmlElement elem = FindElementByAndroidName(manifest, "uses-permission", name);
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
        XmlElement elem = FindElementByAndroidName(manifest, "uses-feature", name);
        if (elem == null)
        {
            elem = doc.CreateElement("uses-feature");
            elem.SetAttribute("name", AndroidNs, name);
            manifest.PrependChild(elem);
        }
        if (required != null)
            elem.SetAttribute("required", AndroidNs, required);
    }

    static void EnsureShareService(XmlDocument doc, XmlElement application)
    {
        const string serviceName = "com.kalyeah.local.ShareService";
        XmlElement service = FindElementByAndroidName(application, "service", serviceName);
        if (service == null)
        {
            service = doc.CreateElement("service");
            service.SetAttribute("name", AndroidNs, serviceName);
            application.AppendChild(service);
        }
        service.SetAttribute("exported", AndroidNs, "true");
        service.SetAttribute("permission", AndroidNs, "android.permission.BIND_NFC_SERVICE");

        if (FindFirstChildElement(service, "intent-filter") == null)
        {
            XmlElement filter = doc.CreateElement("intent-filter");
            XmlElement action = doc.CreateElement("action");
            action.SetAttribute("name", AndroidNs, "android.nfc.cardemulation.action.HOST_APDU_SERVICE");
            filter.AppendChild(action);
            service.AppendChild(filter);
        }

        XmlElement meta = FindElementByAndroidName(service, "meta-data", "android.nfc.cardemulation.host_apdu_service");
        if (meta == null)
        {
            meta = doc.CreateElement("meta-data");
            meta.SetAttribute("name", AndroidNs, "android.nfc.cardemulation.host_apdu_service");
            service.AppendChild(meta);
        }
        meta.SetAttribute("resource", AndroidNs, "@xml/kalyeah_apdu");
    }

    static XmlElement FindFirstChildElement(XmlElement parent, string tag)
    {
        foreach (XmlNode node in parent.ChildNodes)
        {
            if (node is XmlElement element && element.Name == tag)
                return element;
        }
        return null;
    }

    static XmlElement FindElementByAndroidName(XmlElement parent, string tag, string name)
    {
        foreach (XmlNode node in parent.ChildNodes)
        {
            if (node is XmlElement element && element.Name == tag && element.GetAttribute("name", AndroidNs) == name)
                return element;
        }
        return null;
    }
}
