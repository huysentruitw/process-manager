﻿/*
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

namespace ProcessManager
{
    /// <summary>
    /// Event arguments used with ProcessFailedToStart handler.
    /// </summary>
    public sealed class ProcessFailedToStartEventArgs : ProcessEventArgs
    {
        internal ProcessFailedToStartEventArgs(ProcessInfo processInfo, Exception exception)
            : base(processInfo)
        {
            Exception = exception;
        }

        /// <summary>
        /// The Exception that was thrown during Process.Start().
        /// </summary>
        public Exception Exception { get; private set; }
    }
}
