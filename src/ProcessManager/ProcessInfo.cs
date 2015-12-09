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

namespace ProcessManager
{
    /// <summary>
    /// Describes a process that can be queued.
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Create a new <see cref="ProcessInfo"/> instance.
        /// </summary>
        /// <param name="fileName">The filename of the process.</param>
        /// <param name="arguments">The command-line arguments for the process.</param>
        public ProcessInfo(string fileName, string arguments)
        {
            this.Key = Guid.NewGuid();
            this.FileName = fileName;
            this.Arguments = arguments;
        }

        /// <summary>
        /// A unique key for this instance.
        /// </summary>
        public Guid Key { get; private set; }
        /// <summary>
        /// The filename of the process.
        /// </summary>
        public string FileName { get; private set; }
        /// <summary>
        /// The command-line arguments for the process.
        /// </summary>
        public string Arguments { get; private set; }
    }

    /// <summary>
    /// Describes a process with user data that can be queued.
    /// </summary>
    /// <typeparam name="T">The type of the user data.</typeparam>
    public class ProcessInfo<T> : ProcessInfo
    {
        /// <summary>
        /// Create a new <see cref="ProcessInfo&lt;T&gt;"/> instance.
        /// </summary>
        /// <param name="fileName">The filename of the process.</param>
        /// <param name="arguments">The command-line arguments for the process.</param>
        /// <param name="data">The user data.</param>
        public ProcessInfo(string fileName, string arguments, T data)
            : base(fileName, arguments)
        {
            this.Data = data;
        }

        /// <summary>
        /// User data.
        /// </summary>
        public T Data { get; private set; }
    }
}
