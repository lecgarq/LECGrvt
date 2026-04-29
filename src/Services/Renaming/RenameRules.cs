using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LECG.Core.Rename;

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
            return RenameRuleEngine.ApplyReplace(
                text,
                new ReplaceRuleOptions(IsActive, FindText, ReplaceText, MatchCase, UseRegex),
                index);
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
            return RenameRuleEngine.ApplyRemove(
                text,
                new RemoveRuleOptions(IsActive, FirstN, LastN, FromPos, Count),
                index);
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
            return RenameRuleEngine.ApplyAdd(
                text,
                new AddRuleOptions(IsActive, Prefix, Suffix, Insert, AtPos),
                index);
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
            var coreMode = Mode switch
            {
                CaseMode.Lower => CoreCaseMode.Lower,
                CaseMode.Upper => CoreCaseMode.Upper,
                CaseMode.Title => CoreCaseMode.Title,
                CaseMode.Capitalize => CoreCaseMode.Capitalize,
                _ => CoreCaseMode.Lower
            };

            return RenameRuleEngine.ApplyCase(
                text,
                new CaseRuleOptions(IsActive, coreMode),
                index);
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
            var coreMode = Mode == NumberingMode.Prefix
                ? CoreNumberingMode.Prefix
                : CoreNumberingMode.Suffix;

            return RenameRuleEngine.ApplyNumbering(
                text,
                new NumberingRuleOptions(IsActive, coreMode, StartAt, Increment, Separator, Padding),
                index);
        }
    }
}
