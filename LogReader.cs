/*
A simple log file monitor class for .NET
Uses a threaded timer to check for changes in the file, if the file length has changed then the unread
section of the file is read and parsed into lines before being passed back to the event handler.
Note, because the class uses the threaded timer callbacks on the event hander WILL be made form a 
different thread, keep this in mind when using the class.
This class is more reliable than the FileSystemWatcher class provided by Microsoft which often does not 
fire an event even after the file size changes
TODO:
* Customer timer period
* Option to ignore exsiting file contents
* Option to provide SynchronizingObject
Sample Usage:
var monitor = new LogFileMonitor("c:\temp\app.log", "\r\n");
monitor.OnLine += (s, e) =>
{
    // WARNING.. this will be a different thread...
    LogManager.Log(e.Line);
};
monitor.Start();
*/


// Credits https://gist.github.com/ant-fx/989dd86a1ace38a9ac58

using System;
using System.IO;
using System.Timers;

namespace AvatarUploader
{
    internal class LogFileMonitorLineEventArgs : EventArgs
    {
        internal string Line { get; set; }
    }

    internal class LogFileMonitor
    {
        internal EventHandler<LogFileMonitorLineEventArgs> OnLine;

        // file path + delimiter internals
        string _path = String.Empty;
        string _delimiter = String.Empty;

        // timer object
        System.Timers.Timer _t = null;

        // buffer for storing data at the end of the file that does not yet have a delimiter
        string _buffer = String.Empty;

        // get the current size
        long _currentSize = 0;

        // are we currently checking the log (stops the timer going in more than once)
        bool _isCheckingLog = false;

        protected bool StartCheckingLog()
        {
            lock (_t)
            {
                if (_isCheckingLog)
                    return true;

                _isCheckingLog = true;
                return false;
            }
        }

        protected void DoneCheckingLog()
        {
            lock (_t)
                _isCheckingLog = false;
        }

        internal LogFileMonitor(string path, string delimiter = "\n")
        {
            _path = path;
            _delimiter = delimiter;
        }

        internal void Start()
        {
            // get the current size
            _currentSize = new FileInfo(_path).Length;

            // start the timer
            _t = new System.Timers.Timer();
            _t.Elapsed += CheckLog;
            _t.AutoReset = true;
            _t.Start();
        }

        internal void CheckLog(object s, ElapsedEventArgs e)
        {
            if (StartCheckingLog())
            {
                try
                {
                    // get the new size
                    var newSize = new FileInfo(_path).Length;

                    // if they are the same then continue.. if the current size is bigger than the new size continue
                    if (_currentSize >= newSize)
                        return;

                    // read the contents of the file
                    using (var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(stream))
                    {
                        // seek to the current file position
                        sr.BaseStream.Seek(_currentSize, SeekOrigin.Begin);

                        // read from current position to the end of the file
                        var newData = _buffer + sr.ReadToEnd();

                        // if we don't end with a delimiter we need to store some data in the buffer for next time
                        if (!newData.EndsWith(_delimiter))
                        {
                            // we don't have any lines to process so save in the buffer for next time
                            if (newData.IndexOf(_delimiter) == -1)
                            {
                                _buffer += newData;
                                newData = String.Empty;
                            }
                            else
                            {
                                // we have at least one line so store the last section (without lines) in the buffer
                                var pos = newData.LastIndexOf(_delimiter) + _delimiter.Length;
                                _buffer = newData.Substring(pos);
                                newData = newData.Substring(0, pos);
                            }
                        }

                        // split the data into lines
                        var lines = newData.Split(new string[] { _delimiter }, StringSplitOptions.RemoveEmptyEntries);

                        // send back to caller, NOTE: this is done from a different thread!
                        foreach (var line in lines)
                        {
                            if (OnLine != null)
                                OnLine(this, new LogFileMonitorLineEventArgs { Line = line });
                        }
                    }

                    // set the new current position
                    _currentSize = newSize;
                }
                catch (Exception)
                {
                }

                // we done..
                DoneCheckingLog();
            }
        }

        internal void Stop()
        {
            if (_t == null)
                return;

            _t.Stop();
        }
    }
}
