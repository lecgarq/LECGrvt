using System;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LECG.Services
{
    public interface IRenameRule
    {
        bool IsActive { get; set; }
        string Apply(string text, int index = 0);
    }

    public class ReplaceRule : ObservableObject, IRenameRule
    {
        private bool _isActive;
        private string _findText = "";
        private string _replaceText = "";
        private bool _matchCase;
        private bool _useRegex;

        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        public string FindText { get => _findText; set => SetProperty(ref _findText, value); }
        public string ReplaceText { get => _replaceText; set => SetProperty(ref _replaceText, value); }
        public bool MatchCase { get => _matchCase; set => SetProperty(ref _matchCase, value); }
        public bool UseRegex { get => _useRegex; set => SetProperty(ref _useRegex, value); }

        public string Apply(string text, int index = 0)
        {
            if (!IsActive || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(FindText)) return text;

            if (UseRegex)
            {
                var options = MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                try
                {
                    return Regex.Replace(text, FindText, ReplaceText ?? "", options);
                }
                catch
                {
                    return text; // Invalid Regex
                }
            }
            else
            {
                var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                // Simple string replace with comparison is needed.
                // string.Replace(string, string, comparison) is .NET 6+
                return text.Replace(FindText, ReplaceText ?? "", comparison);
            }
        }
    }

    public class RemoveRule : ObservableObject, IRenameRule
    {
        private bool _isActive;
        private int _firstN;
        private int _lastN;
        private int _fromPos;
        private int _count;

        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        public int FirstN { get => _firstN; set => SetProperty(ref _firstN, value); }
        public int LastN { get => _lastN; set => SetProperty(ref _lastN, value); }
        public int FromPos { get => _fromPos; set => SetProperty(ref _fromPos, value); }
        public int Count { get => _count; set => SetProperty(ref _count, value); }

        public string Apply(string text, int index = 0)
        {
            if (!IsActive || string.IsNullOrEmpty(text)) return text;
            string result = text;

            // 1. First N
            if (FirstN > 0)
            {
                if (FirstN >= result.Length) return "";
                result = result.Substring(FirstN);
            }

            // 2. Last N
            if (LastN > 0 && result.Length > 0)
            {
                if (LastN >= result.Length) return "";
                result = result.Substring(0, result.Length - LastN);
            }

            // 3. Remove from/count
            // FromPos is 0-based for user? UI usually 1-based. Let's assume 1-based API for user convenience, convert to 0 internally?
            // User requested "intelligent". Let's use 0-based internally for now and label UI.
            if (Count > 0 && result.Length > 0)
            {
                int start = FromPos; // 0-based
                int len = Count;
                if (start >= 0 && start < result.Length)
                {
                    if (start + len > result.Length) len = result.Length - start;
                    result = result.Remove(start, len);
                }
            }

            return result;
        }
    }

    public class AddRule : ObservableObject, IRenameRule
    {
        private bool _isActive;
        private string _prefix = "";
        private string _suffix = "";
        private string _insert = "";
        private int _atPos;

        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        public string Prefix { get => _prefix; set => SetProperty(ref _prefix, value); }
        public string Suffix { get => _suffix; set => SetProperty(ref _suffix, value); }
        public string Insert { get => _insert; set => SetProperty(ref _insert, value); }
        public int AtPos { get => _atPos; set => SetProperty(ref _atPos, value); }

        public string Apply(string text, int index = 0)
        {
            if (!IsActive) return text;
            string result = text ?? "";

            // 1. Insert
            if (!string.IsNullOrEmpty(Insert))
            {
                int pos = AtPos;
                if (pos < 0) pos = 0;
                if (pos > result.Length) pos = result.Length;
                result = result.Insert(pos, Insert);
            }

            // 2. Prefix
            if (!string.IsNullOrEmpty(Prefix))
            {
                result = Prefix + result;
            }

            // 3. Suffix
            if (!string.IsNullOrEmpty(Suffix))
            {
                result = result + Suffix;
            }

            return result;
        }
    }

    public enum CaseMode { Lower, Upper, Title, Capitalize }
    public enum NumberingMode { Suffix, Prefix }

    public class CaseRule : ObservableObject, IRenameRule
    {
        private bool _isActive;
        private CaseMode _mode;

        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        public CaseMode Mode { get => _mode; set => SetProperty(ref _mode, value); }

        public string Apply(string text, int index = 0)
        {
            if (!IsActive || string.IsNullOrEmpty(text)) return text;

            switch (Mode)
            {
                case CaseMode.Lower: return text.ToLowerInvariant();
                case CaseMode.Upper: return text.ToUpperInvariant();
                case CaseMode.Title: return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());
                case CaseMode.Capitalize:
                    if (text.Length > 0) return char.ToUpper(text[0]) + text.Substring(1);
                    return text;
                default: return text;
            }
        }
    }

    public class NumberingRule : ObservableObject, IRenameRule
    {
        private bool _isActive;
        private NumberingMode _mode = NumberingMode.Suffix;
        private int _startAt = 1;
        private int _increment = 1;
        private string _separator = "";
        private int _padding = 1;

        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        public NumberingMode Mode { get => _mode; set => SetProperty(ref _mode, value); }
        public int StartAt { get => _startAt; set => SetProperty(ref _startAt, value); }
        public int Increment { get => _increment; set => SetProperty(ref _increment, value); }
        public string Separator { get => _separator; set => SetProperty(ref _separator, value); }
        public int Padding { get => _padding; set => SetProperty(ref _padding, value); }

        public string Apply(string text, int index = 0)
        {
            // Note: index is the position in the list (0-based)
            if (!IsActive) return text;
            
            int val = StartAt + (index * Increment);
            string numStr = val.ToString().PadLeft(Padding, '0');
            
            string fullString = "";

            if (Mode == NumberingMode.Prefix)
            {
                // Separator usually goes AFTER number
                fullString = numStr + Separator; 
                return fullString + text;
            }
            else // Suffix
            {
                // Separator usually goes BEFORE number
                fullString = Separator + numStr;
                return text + fullString;
            }
        }
    }
}
