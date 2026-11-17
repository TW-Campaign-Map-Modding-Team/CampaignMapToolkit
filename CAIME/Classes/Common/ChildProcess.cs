using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CAIME
{
    /// <summary>Outcome of one <see cref="ChildProcess.Run"/> call.</summary>
    internal sealed class ChildProcessResult
    {
        public bool          TimedOut;
        public int           ExitCode;
        public DateTime      StartTime;
        public List<string>  StandardOutput;
        public List<string>  StandardError;
    }

    /// <summary>
    /// Runs an external tool and collects both output pipes.
    /// </summary>
    internal static class ChildProcess
    {
        /// <summary>
        /// Starts <paramref name="startInfo"/> and waits for it to finish.
        /// <para>Both pipes are read asynchronously: draining one to EOF before touching the other
        /// deadlocks as soon as the child fills the pipe the parent is not reading. The wait is
        /// bounded so a wedged child cannot hang the app forever.</para>
        /// </summary>
        /// <param name="startInfo">What to run. The redirection flags are set here.</param>
        /// <param name="timeoutMs">How long to wait before killing the child.</param>
        public static ChildProcessResult Run(ProcessStartInfo startInfo, int timeoutMs)
        {
            startInfo.UseShellExecute        = false;
            startInfo.CreateNoWindow         = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError  = true;

            // The Rome II, Attila and Warhammer data builders start a TCPConsole helper from their
            // binaries folder, MapDataBuilder's current directory, and refuse to load without it;
            // Windows skips the current directory when this is set.
            startInfo.EnvironmentVariables.Remove("NoDefaultCurrentDirectoryInExePath");

            var standardOutput = new List<string>();
            var standardError  = new List<string>();

            using (var process = new Process { StartInfo = startInfo })
            {
                // The handlers run on threadpool threads, hence the locks.
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) { lock (standardOutput) { standardOutput.Add(e.Data); } }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null) { lock (standardError) { standardError.Add(e.Data); } }
                };

                process.Start();

                var startTime = process.StartTime;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exited = process.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { process.Kill(); } catch { /* best effort */ }
                }
                else
                {
                    // The parameterless overload waits for the async readers to flush.
                    process.WaitForExit();
                }

                lock (standardOutput)
                lock (standardError)
                {
                    return new ChildProcessResult
                    {
                        TimedOut       = !exited,
                        ExitCode       = exited ? process.ExitCode : -1,
                        StartTime      = startTime,
                        StandardOutput = new List<string>(standardOutput),
                        StandardError  = new List<string>(standardError),
                    };
                }
            }
        }
    }
}
