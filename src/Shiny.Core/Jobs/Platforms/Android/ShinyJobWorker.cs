#if ANDROID9
using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Runtime;
using AndroidX.Work;
using Log = Android.Util.Log;

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

            var jobManager = ShinyHost.Resolve<IJobManager>();
            var failed = false;

            foreach (var tag in this.Tags)
            {
                try
                {
                    var job = Task.Run(() => jobManager.GetJob(tag)).Result;
                    if (job is null)
                    {
                        continue;
                    }

                    var result = Task.Run(() => jobManager.Run(job.Identifier, this.cancelSrc.Token)).Result;
                    if (!result.Success)
                    {
                        failed = true;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(tag, e.Message);

                    failed = false;
                }
            }

            return failed ? Result.InvokeFailure() : Result.InvokeSuccess();
        }

        public override void OnStopped()
        {
            this.cancelSrc?.Cancel();
        }
    }
}
#endif