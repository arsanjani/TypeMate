using System.Threading.Tasks;
using System.Windows;

namespace TypeMate
{
    public static class PopupWindowExtensions
    {
        public static async Task ShowAndWaitAsync(this PopupWindow popup)
        {
            var tcs = new TaskCompletionSource<bool>();
            bool closed = false;
            popup.Closed += (s, e) => { if (!closed) { closed = true; tcs.TrySetResult(true); } };
            popup.Show();
            await tcs.Task;
        }
    }
}
