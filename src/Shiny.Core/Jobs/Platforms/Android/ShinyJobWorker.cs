#if ANDROID9
using System.Linq;
using System.Threading;
using Android.Runtime;
using System;
using System.Threading.Tasks;
using Android.Content;
using AndroidX.Work;


namespace Shiny.Jobs
{
    public class ShinyJobWorker : Worker
    {
        CancellationTokenSource cancelSrc;

        public ShinyJobWorker(Context context, WorkerParameters workerParams) : base(context, workerParams)
        {
        }

        protected ShinyJobWorker(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public override Result DoWork()
        {
            this.cancelSrc = new CancellationTokenSource();

            var results = Task.Run(() => ShinyHost
                .Resolve<IJobManager>()
                .RunAll(this.cancelSrc.Token)).GetAwaiter().GetResult().ToList();
            if (results.Any(x => !x.Success))
            {
                return Result.InvokeFailure();
            }

            return Result.InvokeSuccess();
        }

        public override void OnStopped()
        {
            this.cancelSrc?.Cancel();
        }
    }
}
#endif