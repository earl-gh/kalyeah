using System.Text;

namespace Kalyeah.UI
{
    /// <summary>
    /// The private room code as the join screen's eight boxes take it (ui_code_boxes.gd): capitals only, characters outside the code
    /// alphabet dropped, at most `Length` long; the next empty box is the lit one; paste fills from the clipboard and says how much fit.
    /// </summary>
    public sealed class CodeEntry
    {
        public readonly string Alphabet;
        public readonly int Length;
        public string Text { get; private set; } = "";
        /// <summary>The server refused this code: the boxes are red until it is edited.</summary>
        public bool Error;
        public string ErrorText = "";

        public CodeEntry(string alphabet, int length)
        {
            Alphabet = alphabet ?? "";
            Length = length;
        }

        /// <summary>`raw` upper-cased, without characters outside the alphabet, cut to `Length`.</summary>
        public string Clean(string raw)
        {
            var kept = new StringBuilder();
            foreach (char c in (raw ?? "").ToUpperInvariant())
            {
                if (kept.Length >= Length) break;
                if (Alphabet.IndexOf(c) >= 0) kept.Append(c);
            }
            return kept.ToString();
        }

        /// <summary>Takes what was typed; editing clears the error.</summary>
        public void Set(string raw)
        {
            string clean = Clean(raw);
            if (clean != Text)
            {
                Error = false;
                ErrorText = "";
            }
            Text = clean;
        }

        public bool Complete => Text.Length == Length;

        /// <summary>The box index to light (the next empty one; the last when full).</summary>
        public int Cursor => Text.Length >= Length ? Length - 1 : Text.Length;

        /// <summary>The character in box `i` ("" when empty).</summary>
        public string Box(int i) => i >= 0 && i < Text.Length ? Text[i].ToString() : "";

        /// <summary>Fills from `clipboard`; returns the toast to show.</summary>
        public string Paste(string clipboard)
        {
            string value = Clean(clipboard);
            if (value.Length == 0) return "Nothing to paste yet. Copy the room code first!";
            Set(value);
            Error = false;
            ErrorText = "";
            return value.Length == Length ? "Code pasted!" : "Pasted " + value.Length + " of " + Length + " characters";
        }

        public void Refuse(string reason)
        {
            Error = true;
            ErrorText = reason ?? "";
        }

        /// <summary>The words shown when Join is tapped with boxes still empty ("" when the code is whole).</summary>
        public string MissingText() => Complete ? "" : "Enter all " + Length + " characters!";
    }
}
