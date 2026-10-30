using System;
using System.Threading;
using System.Threading.Tasks;

namespace AmbientWeatherWigiDash
{
    // Every operation that can use widget resources owns a lease, including work
    // queued to the thread pool and calls into the host. Shutdown closes admission
    // immediately, then cleans up only after the final admitted operation exits.
    internal sealed class WidgetLifetime
    {
        private readonly object sync = new object();
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly CancellationToken shutdownToken;
        private readonly Action cleanup;
        private readonly TaskCompletionSource<bool> completion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int active;
        private bool stopping;
        private bool cleanupStarted;
        private string cleanupError;

        internal WidgetLifetime(Action cleanup)
        {
            if (cleanup == null) throw new ArgumentNullException("cleanup");
            this.cleanup = cleanup;
            shutdownToken = shutdown.Token;
        }

        internal bool IsStopping
        {
            get { lock (sync) return stopping; }
        }

        internal Task Completion { get { return completion.Task; } }

        internal string CleanupError
        {
            get { lock (sync) return cleanupError; }
        }

        internal bool TryEnter(out CancellationToken token)
        {
            lock (sync)
            {
                // Capture the token before the source can be disposed. Even a
                // rejected caller never needs to read CancellationTokenSource.Token.
                token = shutdownToken;
                if (stopping) return false;
                active++;
                return true;
            }
        }

        internal void Exit()
        {
            bool finish = false;
            lock (sync)
            {
                if (active == 0) throw new InvalidOperationException("No widget operation is active.");
                active--;
                if (stopping && active == 0 && !cleanupStarted)
                {
                    cleanupStarted = true;
                    finish = true;
                }
            }
            if (finish) FinishCleanup();
        }

        internal void Stop()
        {
            lock (sync)
            {
                if (stopping) return;
                stopping = true;
                // Cancel can synchronously finish operations or invoke callbacks
                // that call Stop again. This lease keeps cleanup away from the
                // cancellation source until Cancel has returned.
                active++;
            }

            // Do not hold a widget lock while invoking cancellation callbacks and
            // never wait for active work: the host may be disposing on its UI thread.
            try { shutdown.Cancel(); }
            catch (Exception error) { RecordCleanupError(error); }
            finally { Exit(); }
        }

        private void FinishCleanup()
        {
            // Exactly one Exit owns cleanup. Neither host/resource cleanup nor task
            // continuations run under sync, so reentrant Stop remains harmless.
            try { cleanup(); }
            catch (Exception error) { RecordCleanupError(error); }
            finally
            {
                try { shutdown.Dispose(); }
                catch (Exception error) { RecordCleanupError(error); }
                finally { completion.TrySetResult(true); }
            }
        }

        private void RecordCleanupError(Exception error)
        {
            // Completion is a successful shutdown signal, not an unobserved fault.
            // Retain only safe troubleshooting text, never exception/request data.
            string message = ConnectionErrors.GetMessage(error);
            lock (sync)
            {
                if (cleanupError == null) cleanupError = message;
            }
        }
    }
}
