namespace DynamicIsland.Core;

/// <summary>
/// Chạy việc trên một luồng STA riêng. Dùng cho COM của Outlook: gọi Outlook có thể chậm,
/// không được để treo giao diện, và COM của Office cần luồng STA.
/// </summary>
public static class StaTask
{
    public static Task<T> Run<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(work()); }
            catch (Exception ex) { tcs.SetException(ex); }
        })
        { IsBackground = true, Name = "DynamicIsland.STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    public static Task Run(Action work) => Run(() => { work(); return true; });
}
