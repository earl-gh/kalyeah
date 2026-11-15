using System.Collections.Generic;
using System.Text;

namespace Kalyeah.Net
{
    /// <summary>
    /// The standard Wi-Fi QR text (`WIFI:T:WPA;S:&lt;name&gt;;P:&lt;password&gt;;;`), written for the game hotspot and read back from a
    /// scan or an NFC tap (port of wifi_qr.gd). `\`, `;`, `,`, `:` and `"` are escaped with `\`. A network is (ssid, password,
    /// security "WPA" | "SAE" | "nopass"); WEP is refused.
    /// </summary>
    public static class WifiQr
    {
        public const string Prefix = "WIFI:";
        const string Special = "\\;,:\"";

        public sealed class Network
        {
            public string Ssid = "";
            public string Password = "";
            public string Security = "WPA";
        }

        public static string Build(Network network)
        {
            string security = network.Security ?? "WPA";
            var text = new StringBuilder(Prefix + "T:" + security + ";S:" + Escape(network.Ssid ?? "") + ";");
            if (security != "nopass") text.Append("P:" + Escape(network.Password ?? "") + ";");
            return text.Append(';').ToString();
        }

        /// <summary>The network in a scanned or tapped text, or null when it is not a Wi-Fi code Kalyeah can join.</summary>
        public static Network Parse(string text)
        {
            string clean = RoomService.StripEdges(text ?? "");
            if (!clean.ToUpperInvariant().StartsWith(Prefix, System.StringComparison.Ordinal)) return null;
            var fields = new Dictionary<string, string>();
            foreach (string field in Split(clean.Substring(Prefix.Length), ';'))
            {
                int colon = UnescapedColon(field);
                if (colon <= 0) continue;
                fields[field.Substring(0, colon).ToUpperInvariant()] = Unescape(field.Substring(colon + 1));
            }
            fields.TryGetValue("S", out string ssid);
            if (string.IsNullOrEmpty(ssid)) return null;
            fields.TryGetValue("P", out string password);
            password ??= "";
            fields.TryGetValue("T", out string kind);
            kind = (kind ?? "").ToUpperInvariant();
            string security;
            switch (kind)
            {
                case "WPA":
                case "WPA2":
                case "WPA/WPA2":
                case "WPA2/WPA3":
                    security = "WPA";
                    break;
                case "SAE":
                case "WPA3":
                    security = "SAE";
                    break;
                case "NOPASS":
                case "":
                    security = password.Length == 0 ? "nopass" : "WPA";
                    break;
                default:
                    return null;
            }
            if (security != "nopass" && (password.Length < 8 || password.Length > 63)) return null;
            return new Network { Ssid = ssid, Password = security != "nopass" ? password : "", Security = security };
        }

        static string Escape(string value)
        {
            var output = new StringBuilder();
            foreach (char c in value)
            {
                if (Special.IndexOf(c) >= 0) output.Append('\\');
                output.Append(c);
            }
            return output.ToString();
        }

        static string Unescape(string value)
        {
            var output = new StringBuilder();
            bool escaped = false;
            foreach (char c in value)
            {
                if (escaped)
                {
                    output.Append(c);
                    escaped = false;
                }
                else if (c == '\\') escaped = true;
                else output.Append(c);
            }
            return output.ToString();
        }

        /// <summary>`text` cut at each unescaped `separator` (the escapes are kept, for Unescape).</summary>
        static List<string> Split(string text, char separator)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            bool escaped = false;
            foreach (char c in text)
            {
                if (escaped)
                {
                    current.Append(c);
                    escaped = false;
                }
                else if (c == '\\')
                {
                    current.Append(c);
                    escaped = true;
                }
                else if (c == separator)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(c);
            }
            if (current.Length > 0) parts.Add(current.ToString());
            return parts;
        }

        static int UnescapedColon(string field)
        {
            bool escaped = false;
            for (int i = 0; i < field.Length; i++)
            {
                if (escaped) escaped = false;
                else if (field[i] == '\\') escaped = true;
                else if (field[i] == ':') return i;
            }
            return -1;
        }
    }
}
