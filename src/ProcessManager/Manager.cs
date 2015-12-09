/*
 * Copyright 2015 Huysentruit Wouter
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace ProcessManager
{
    /// <summary>
    /// The <see cref="Manager"/> class.
    /// </summary>
    public class Manager : IDisposable
    {
        private readonly ManualResetEvent disposing = new ManualResetEvent(false);
        private readonly AutoResetEvent processQueued = new AutoResetEvent(false);
        private readonly AutoResetEvent processFinished = new AutoResetEvent(false);
        private readonly ConcurrentQueue<ProcessInfo> queuedProcesses = new ConcurrentQueue<ProcessInfo>();
        private readonly ConcurrentDictionary<Guid, System.Diagnostics.Process> runningProcesses =
            new ConcurrentDictionary<Guid, System.Diagnostics.Process>();
        private Thread managingThread;

        /// <summary>
        /// Constructs a new <see cref="Manager"/> instance.
        /// </summary>
        /// <param name="maximumSimultanousProcesses">Maximum number of simultanous processes. Defaults to 4.</param>
        public Manager(int maximumSimultanousProcesses = 4)
        {
            if (maximumSimultanousProcesses < 1)
                throw new ArgumentOutOfRangeException("maximumSimultanousProcesses", "Should allow at least one process");

            this.MaximumSimultanousProcesses = maximumSimultanousProcesses;
            this.managingThread = new Thread(this.Manage);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~Manager()
        {
            this.Dispose();
        }

        /// <summary>
        /// Gets tthe maximum number of simultanous processes.
        /// </summary>
        public int MaximumSimultanousProcesses { get; private set; }

        /// <summary>
        /// Gets the number of running processes.
        /// </summary>
        public int RunningProcessCount { get { return this.runningProcesses.Count; } }

        /// <summary>
        /// Cleanup used resources.
        /// </summary>
        public void Dispose()
        {
            if (this.managingThread != null)
            {
                this.disposing.Set();
                this.managingThread.Join();
                this.managingThread = null;

                foreach (var process in this.runningProcesses.Values)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }

                this.runningProcesses.Clear();
            }
        }

        /// <summary>
        /// Start the process manager.
        /// </summary>
        public void Start()
        {
            if (this.disposing.WaitOne(0, false))
                throw new ObjectDisposedException("Manager");

            this.managingThread.Start();
        }

        /// <summary>
        /// Queue a process.
        /// </summary>
        /// <param name="processInfo">The <see cref="ProcessInfo"/> object describing the process.</param>
        public void Queue(ProcessInfo processInfo)
        {
            if (this.disposing.WaitOne(0, false))
                throw new ObjectDisposedException("Manager");

            this.queuedProcesses.Enqueue(processInfo);
            this.processQueued.Set();
        }

        /// <summary>
        /// Event fired when the process queue is empty and the last running process has finished.
        /// </summary>
        public event EventHandler AllProcessesFinished;
        private void OnAllProcessesFinished()
        {
            var handler = Interlocked.CompareExchange(ref this.AllProcessesFinished, null, null);
            if (handler != null)
                handler(this, new EventArgs());
        }

        /// <summary>
        /// Event fired when a queued process has started.
        /// </summary>
        public event EventHandler<ProcessStartedEventArgs> ProcessStarted;
        private void OnProcessStarted(ProcessInfo processInfo)
        {
            var handler = Interlocked.CompareExchange(ref this.ProcessStarted, null, null);
            if (handler != null)
                handler(this, new ProcessStartedEventArgs(processInfo));
        }

        /// <summary>
        /// Event fired when a queued process failed to start.
        /// </summary>
        public event EventHandler<ProcessFailedToStartEventArgs> ProcessFailedToStart;
        private void OnProcessFailedToStart(ProcessInfo processInfo, Exception exception)
        {
            var handler = Interlocked.CompareExchange(ref this.ProcessFailedToStart, null, null);
            if (handler != null)
                handler(this, new ProcessFailedToStartEventArgs(processInfo, exception));
        }

        /// <summary>
        /// Event fired when a queued process has finished.
        /// </summary>
        public event EventHandler<ProcessFinishedEventArgs> ProcessFinished;
        private void OnProcessFinished(ProcessInfo processInfo)
        {
            var handler = Interlocked.CompareExchange(ref this.ProcessFinished, null, null);
            if (handler != null)
                handler(this, new ProcessFinishedEventArgs(processInfo));
        }

        /// <summary>
        /// Event fired when a queued process outputs data to StandardOutput.
        /// </summary>
        public event EventHandler<ProcessDataReceivedEventArgs> ProcessOutputDataReceived;
        private void OnProcessOutputDataReceived(ProcessInfo processInfo, string data)
        {
            var handler = Interlocked.CompareExchange(ref this.ProcessOutputDataReceived, null, null);
            if (handler != null)
                handler(this, new ProcessDataReceivedEventArgs(processInfo, data));
        }

        /// <summary>
        /// Event fired when a queued process outputs data to StandardError.
        /// </summary>
        public event EventHandler<ProcessDataReceivedEventArgs> ProcessErrorDataReceived;
        private void OnProcessErrorDataReceived(ProcessInfo processInfo, string data)
        {
            var handler = Interlocked.CompareExchange(ref this.ProcessErrorDataReceived, null, null);
            if (handler != null)
                handler(this, new ProcessDataReceivedEventArgs(processInfo, data));
        }

        private void Manage()
        {
            var waitHandles = new WaitHandle[] { this.disposing, this.processQueued, this.processFinished };
            while (WaitHandle.WaitAny(waitHandles) != 0)
            {
                while (this.runningProcesses.Count < this.MaximumSimultanousProcesses)
                {
                    ProcessInfo processInfo;
                    if (!this.queuedProcesses.TryDequeue(out processInfo))
                        break;

                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = processInfo.FileName,
                            Arguments = processInfo.Arguments,
                            UseShellExecute = false,
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                        },
                        EnableRaisingEvents = true
                    };

                    process.OutputDataReceived += (sender, e) => this.OnProcessOutputDataReceived(processInfo, e.Data);
                    process.ErrorDataReceived += (sender, e) => this.OnProcessErrorDataReceived(processInfo, e.Data);
                    process.Exited += (sender, e) =>
                    {
                        Debug.Assert(sender == process);
                        if (this.disposing.WaitOne(0, false))
                            return;

                        System.Diagnostics.Process garbage;
                        this.runningProcesses.TryRemove(processInfo.Key, out garbage);
                        Debug.Assert(sender == garbage);
                        this.processFinished.Set();
                        this.OnProcessFinished(processInfo);

                        if (this.queuedProcesses.Count == 0 && this.runningProcesses.Count == 0)
                            this.OnAllProcessesFinished();
                    };

                    try
                    {
                        if (!process.Start())
                            throw new Exception("Process failed to start");
                    }
                    catch (Exception ex)
                    {
                        this.OnProcessFailedToStart(processInfo, ex);
                        continue;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    this.runningProcesses.TryAdd(processInfo.Key, process);
                    this.OnProcessStarted(processInfo);
                }
            }
        }
    }
}
