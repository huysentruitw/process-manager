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
        private readonly ConcurrentDictionary<Guid, Process> runningProcesses = new ConcurrentDictionary<Guid, Process>();
        private Thread managingThread;

        /// <summary>
        /// Constructs a new <see cref="Manager"/> instance.
        /// </summary>
        /// <param name="maximumSimultanousProcesses">Maximum number of simultaneous processes. Defaults to 4.</param>
        public Manager(int maximumSimultanousProcesses = 4)
        {
            if (maximumSimultanousProcesses < 1)
                throw new ArgumentOutOfRangeException("maximumSimultanousProcesses", "Should allow at least one process");

            MaximumSimultanousProcesses = maximumSimultanousProcesses;
            managingThread = new Thread(Manage);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~Manager()
        {
            Dispose();
        }

        /// <summary>
        /// Gets the maximum number of simultaneous processes.
        /// </summary>
        public int MaximumSimultanousProcesses { get; private set; }

        /// <summary>
        /// Gets the number of running processes.
        /// </summary>
        public int RunningProcessCount => runningProcesses.Count;

        /// <summary>
        /// Cleanup used resources.
        /// </summary>
        public void Dispose()
        {
            if (managingThread != null)
            {
                disposing.Set();
                managingThread.Join();
                managingThread = null;

                foreach (var process in runningProcesses.Values)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }

                runningProcesses.Clear();
            }
        }

        /// <summary>
        /// Start the process manager.
        /// </summary>
        public void Start()
        {
            if (disposing.WaitOne(0, false))
                throw new ObjectDisposedException($"Manager");

            managingThread.Start();
        }

        /// <summary>
        /// Queue a process.
        /// </summary>
        /// <param name="processInfo">The <see cref="ProcessInfo"/> object describing the process.</param>
        public void Queue(ProcessInfo processInfo)
        {
            if (disposing.WaitOne(0, false))
                throw new ObjectDisposedException("Manager");

            queuedProcesses.Enqueue(processInfo);
            processQueued.Set();
        }

        /// <summary>
        /// Event fired when the process queue is empty and the last running process has finished.
        /// </summary>
        public event EventHandler AllProcessesFinished;
        private void OnAllProcessesFinished()
        {
            Interlocked.CompareExchange(ref AllProcessesFinished, null, null)?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Event fired when a queued process has started.
        /// </summary>
        public event EventHandler<ProcessStartedEventArgs> ProcessStarted;
        private void OnProcessStarted(ProcessInfo processInfo)
        {
            Interlocked.CompareExchange(ref ProcessStarted, null, null)?.Invoke(this, new ProcessStartedEventArgs(processInfo));
        }

        /// <summary>
        /// Event fired when a queued process failed to start.
        /// </summary>
        public event EventHandler<ProcessFailedToStartEventArgs> ProcessFailedToStart;
        private void OnProcessFailedToStart(ProcessInfo processInfo, Exception exception)
        {
            Interlocked.CompareExchange(ref ProcessFailedToStart, null, null)?.Invoke(this, new ProcessFailedToStartEventArgs(processInfo, exception));
        }

        /// <summary>
        /// Event fired when a queued process has finished.
        /// </summary>
        public event EventHandler<ProcessFinishedEventArgs> ProcessFinished;
        private void OnProcessFinished(ProcessInfo processInfo)
        {
            Interlocked.CompareExchange(ref ProcessFinished, null, null)?.Invoke(this, new ProcessFinishedEventArgs(processInfo));
        }

        /// <summary>
        /// Event fired when a queued process outputs data to StandardOutput.
        /// </summary>
        public event EventHandler<ProcessDataReceivedEventArgs> ProcessOutputDataReceived;
        private void OnProcessOutputDataReceived(ProcessInfo processInfo, string data)
        {
            Interlocked.CompareExchange(ref ProcessOutputDataReceived, null, null)?.Invoke(this, new ProcessDataReceivedEventArgs(processInfo, data));
        }

        /// <summary>
        /// Event fired when a queued process outputs data to StandardError.
        /// </summary>
        public event EventHandler<ProcessDataReceivedEventArgs> ProcessErrorDataReceived;
        private void OnProcessErrorDataReceived(ProcessInfo processInfo, string data)
        {
            Interlocked.CompareExchange(ref ProcessErrorDataReceived, null, null)?.Invoke(this, new ProcessDataReceivedEventArgs(processInfo, data));
        }

        private void Manage()
        {
            WaitHandle[] waitHandles = new WaitHandle[] { disposing, processQueued, processFinished };
            while (WaitHandle.WaitAny(waitHandles) != 0)
            {
                while (runningProcesses.Count < MaximumSimultanousProcesses)
                {
                    ProcessInfo processInfo;
                    if (!queuedProcesses.TryDequeue(out processInfo))
                        break;

                    var process = new Process
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

                    process.OutputDataReceived += (sender, e) => OnProcessOutputDataReceived(processInfo, e.Data);
                    process.ErrorDataReceived += (sender, e) => OnProcessErrorDataReceived(processInfo, e.Data);
                    process.Exited += (sender, e) =>
                    {
                        Debug.Assert(sender == process);
                        if (disposing.WaitOne(0, false))
                            return;

                        Process garbage;
                        runningProcesses.TryRemove(processInfo.Key, out garbage);
                        Debug.Assert(sender == garbage);
                        processFinished.Set();
                        OnProcessFinished(processInfo);

                        if (queuedProcesses.Count == 0 && runningProcesses.Count == 0)
                            OnAllProcessesFinished();
                    };

                    try
                    {
                        if (!process.Start())
                            throw new Exception("Process failed to start");
                    }
                    catch (Exception ex)
                    {
                        OnProcessFailedToStart(processInfo, ex);
                        continue;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    runningProcesses.TryAdd(processInfo.Key, process);
                    OnProcessStarted(processInfo);
                }
            }
        }
    }
}
