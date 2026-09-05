using System.Reflection;
using System.Windows;
using LECG.Views.Base;

namespace LECG.Tests.Windows;

public sealed class WindowRecoveryTests
{
    [Fact]
    public void OffScreenRecoverySetsVisibleCoordinatesWithoutWpfOwner()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new LecgWindow { Left = 20000, Top = 20000, Width = 900, Height = 820 };
                try
                {
                    typeof(LecgWindow).GetMethod("EnsureVisible", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                    Rect work = SystemParameters.WorkArea;
                    Assert.InRange(window.Left, work.Left, work.Right - window.Width);
                    Assert.InRange(window.Top, work.Top, work.Bottom - window.Height);
                }
                finally { window.Close(); }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Window recovery check timed out");
        Assert.Null(failure);
    }
}
