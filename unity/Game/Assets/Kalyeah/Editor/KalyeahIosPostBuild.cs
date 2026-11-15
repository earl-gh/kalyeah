using System;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
#if UNITY_IOS
using UnityEditor.Build;
#endif
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Post-processes the generated iOS Xcode project's <c>Info.plist</c> without external Xcode extensions or DTD network fetches,
/// stamping camera + local-network usage descriptions and fullscreen landscape flags matching <see cref="KalyeahAndroidManifest"/>.
/// </summary>
public sealed class KalyeahIosPostBuild
#if UNITY_IOS
    : IPostprocessBuildWithReport
#endif
{
    public const string CameraUsageDescription =
        "Kalyeah uses the camera to scan room QR codes and snap optional school-ID profile avatars.";

    public const string LocalNetworkUsageDescription =
        "Kalyeah uses your local Wi-Fi or Hotspot network to discover and join nearby LAN barangay rooms.";

    public static readonly (string Key, string Value)[] UsageDescriptions =
    {
        ("NSCameraUsageDescription", CameraUsageDescription),
        ("NSLocalNetworkUsageDescription", LocalNetworkUsageDescription),
    };

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.iOS)
            return;

        string plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
        if (!File.Exists(plistPath))
        {
            Debug.LogError($"KalyeahIosPostBuild: no Info.plist at {plistPath}");
            return;
        }

        var doc = new XmlDocument { XmlResolver = null };
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
        };
        using (XmlReader reader = XmlReader.Create(plistPath, readerSettings))
        {
            doc.Load(reader);
        }

        ApplyToDocument(doc);

        var writerSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = "\n",
        };
        using (XmlWriter writer = XmlWriter.Create(plistPath, writerSettings))
        {
            doc.Save(writer);
        }
    }

    /// <summary>Applies Kalyeah's iOS privacy usage descriptions and fullscreen landscape flags to an <c>Info.plist</c> XML document.</summary>
    public static void ApplyToDocument(XmlDocument doc)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        doc.XmlResolver = null;

        XmlElement plist = doc.DocumentElement;
        if (plist == null)
        {
            plist = doc.CreateElement("plist");
            plist.SetAttribute("version", "1.0");
            doc.AppendChild(plist);
        }

        XmlElement dict = FindFirstChildElement(plist, "dict");
        if (dict == null)
        {
            dict = doc.CreateElement("dict");
            plist.AppendChild(dict);
        }

        for (int i = 0; i < UsageDescriptions.Length; i++)
        {
            (string key, string value) = UsageDescriptions[i];
            SetString(doc, dict, key, value);
        }

        SetBool(doc, dict, "UIRequiresFullScreen", true);
        SetBool(doc, dict, "UIStatusBarHidden", true);
    }

    static void SetString(XmlDocument doc, XmlElement dict, string key, string value)
    {
        XmlElement keyElem = FindDictKeyElement(dict, key);
        if (keyElem == null)
        {
            keyElem = doc.CreateElement("key");
            keyElem.InnerText = key;
            dict.AppendChild(keyElem);

            XmlElement strElem = doc.CreateElement("string");
            strElem.InnerText = value;
            dict.AppendChild(strElem);
            return;
        }

        XmlElement currentVal = NextSiblingElement(keyElem);
        if (currentVal != null && currentVal.Name == "string")
        {
            currentVal.InnerText = value;
            return;
        }

        XmlElement replacement = doc.CreateElement("string");
        replacement.InnerText = value;
        if (currentVal != null)
            dict.ReplaceChild(replacement, currentVal);
        else
            dict.InsertAfter(replacement, keyElem);
    }

    static void SetBool(XmlDocument doc, XmlElement dict, string key, bool value)
    {
        string boolTag = value ? "true" : "false";
        XmlElement keyElem = FindDictKeyElement(dict, key);
        if (keyElem == null)
        {
            keyElem = doc.CreateElement("key");
            keyElem.InnerText = key;
            dict.AppendChild(keyElem);
            dict.AppendChild(doc.CreateElement(boolTag));
            return;
        }

        XmlElement currentVal = NextSiblingElement(keyElem);
        if (currentVal != null && currentVal.Name == boolTag)
            return;

        XmlElement replacement = doc.CreateElement(boolTag);
        if (currentVal != null)
            dict.ReplaceChild(replacement, currentVal);
        else
            dict.InsertAfter(replacement, keyElem);
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

    static XmlElement FindDictKeyElement(XmlElement dict, string key)
    {
        foreach (XmlNode node in dict.ChildNodes)
        {
            if (node is XmlElement element && element.Name == "key" && element.InnerText == key)
                return element;
        }
        return null;
    }

    static XmlElement NextSiblingElement(XmlNode node)
    {
        for (XmlNode next = node.NextSibling; next != null; next = next.NextSibling)
        {
            if (next is XmlElement element)
                return element;
        }
        return null;
    }
}
