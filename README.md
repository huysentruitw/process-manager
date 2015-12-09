# ProcessManager

## Overview

ProcessManager is a managed (C#) library that can be used to queue and start processes like you would with `Process.Start()` but better. Maximum number of simultaneous processes can be configured, processes can be queued and events will notify your code when a process is started, finished or outputs data to standard output or standard error.

This library is great for running lengthy, CPU intensive, tasks like image or video conversions.

## Usage

```C#
var manager = new Manager(4); // Max 4 processes will be started simultaneously
manager.Start();

manager.ProcessErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
manager.ProcessOutputDataReceived += (sender, e) => Console.WriteLine(e.Data);

foreach (var videoFileName in Directory.EnumerateFiles("videos"))
{
	var info = new ProcessInfo(
		"ffprobe.exe",
		string.Format("-v quiet -print_format json -show_format -show_streams \"{0}\"", videoFileName));

	manager.Queue(info);
}
```