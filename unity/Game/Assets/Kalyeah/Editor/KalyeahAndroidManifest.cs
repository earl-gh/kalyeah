#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// The APK asks for exactly the permissions the Godot game's Android preset declares (tests/unit/test_android_build.gd): INTERNET (Online
/// and Hotspot sockets), VIBRATE (haptics), CHANGE_WIFI_MULTICAST_STATE (the host hears the guests' broadcast on a hotspot) and CAMERA
/// (the ID photo; the camera feature itself is optional). Unity adds INTERNET and VIBRATE only when code uses them, and never the other
/// two, so they are written into the generated unityLibrary manifest here.
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
        foreach (string permission in Permissions) Ensure(doc, manifest, "uses-permission", permission, null);
        Ensure(doc, manifest, "uses-feature", "android.hardware.camera", "false");
        doc.Save(manifestPath);
    }

    static void Ensure(XmlDocument doc, XmlElement manifest, string tag, string name, string required)
    {
        foreach (XmlNode node in manifest.ChildNodes)
            if (node is XmlElement element && element.Name == tag && element.GetAttribute("name", AndroidNs) == name) return;
        XmlElement added = doc.CreateElement(tag);
        added.SetAttribute("name", AndroidNs, name);
        if (required != null) added.SetAttribute("required", AndroidNs, required);
        manifest.PrependChild(added);
    }
}
#endif
