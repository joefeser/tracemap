using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Core.Interfaces;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace NetPerf.Core {

    /// <summary>
    /// Main class that will handled the named pipe calls to the netperf Receiver
    /// </summary>
    public class NamedPipesIMessageProcessorSenderV1 : IMessageProcessor, IDisposable {

        private static object _lockObject = new object();
        private ByteArrayWriter _writer = new ByteArrayWriter(8 * 1024 * 1024); //we can send upto an 8 meg message.
        private NamedPipeClientStream _pipe = null;

        /// <summary>
        /// The size of the message to send over the wire, no matter how large the buffer is.
        /// </summary>
        /// <remarks>This is ignored for now</remarks>
        public int MessageSize {
            get;
            private set;
        }

        /// <summary>
        /// If the pipe is initialized, we can send messages.
        /// </summary>
        public bool PipInitialized {
            get;
            set;
        }

        /// <summary>
        /// Writers that are queued waiting for the server to connect.
        /// </summary>
        public Queue<ByteArrayWriter> QueuedWriters {
            get;
            set;
        }

        /// <summary>
        /// If the pipe is not connected, then we need to queue the writer and create a new one.
        /// </summary>
        public bool PipeConnected {
            get;
            set;
        }

        /// <summary>
        /// The name of the pipe.
        /// </summary>
        public string PipeName {
            get;
            private set;
        }

        /// <summary>
        /// If the server failed to connect then we must fail fast
        /// </summary>
        public bool ServerFailedToConnect {
            get;
            set;
        }

        public NamedPipesIMessageProcessorSenderV1()
            : this(CoreConstants.PIPE_NAME, int.MaxValue) {
        }

        public NamedPipesIMessageProcessorSenderV1(string pipeName, int messageSize) {
            if (string.IsNullOrEmpty(pipeName) || pipeName.Trim().Length == 0) {
                throw new ArgumentNullException(pipeName);
            }
            this.PipeName = pipeName;
            this.MessageSize = messageSize;

            SetupPipeThread();
        }

        //Message 0x01
        //message is being sent as len(short)(message) processId ticksPerSecond timestamp len(short)(processName) processName len(short)(machineName) machineName 
        public void OnNewProcess(int processId, long ticksPerSecond, long timestamp, string processName, string machineName) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.OnNewProcess(_writer, processId, ticksPerSecond, timestamp, processName, machineName);
                CheckPipeStatus(false, true);
            }
        }

        //Message 0x02
        //message is being sent as len(short)(message) processId timestamp threadId threadName - Threadname is len(message) - 16
        public void OnNewThread(int processId, long timestamp, int threadId, string threadName) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.OnNewThread(_writer, processId, timestamp, threadId, threadName);
                CheckPipeStatus(false, true);
            }
        }

        //Message 0x03
        //message is being sent as len(short)(message) processId timestamp assemblyIdentity - assemblyIdentity is len(message) - 12
        public void OnNewAssembly(int processId, long timestamp, string assemblyIdentity) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.OnNewAssembly(_writer, processId, timestamp, assemblyIdentity);
                CheckPipeStatus(false, true);
            }
        }

        //The new clients will know what to do with this, for now it is just for the new SqLite Client
        //Message 0x04
        //message is being sent as len(int)(message) (len)assemblyName assemblyName len(ilMetadata) ilMetadata
        public void RegisterMetadata(string assemblyName, string ilMetadata) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.RegisterMetadata(_writer, assemblyName, ilMetadata);
                CheckPipeStatus(false, true);
            }
        }

        //we want to leave room for some registration messages

        //Message 0x40
        //message is being sent as processId moduleId threadId timestamp methodId
        public void OnMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.OnMethodEntered(_writer, processId, moduleId, threadId, timestamp, methodId);
                CheckPipeStatus(false, false);
            }
        }

        //Message 0x41
        //message is being sent as len(int)(message) processId moduleId threadId timestamp methodId len(classTypes) classTypes len(methodTypes) methodTypes
        public void OnGenericMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.OnGenericMethodEntered(_writer, processId, moduleId, threadId, timestamp, methodId, classTypes, methodTypes);
                CheckPipeStatus(false, false);
            }
        }

        //Message 0x42
        //message is being sent as processId moduleId threadId timestamp methodId
        public void OnMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.OnMethodExited(_writer, processId, moduleId, threadId, timestamp, methodId);
                CheckPipeStatus(true, false);
            }
        }

        //Message 0x43
        //message is being sent as len(int)(message) processId moduleId threadId timestamp methodId len(classTypes) classTypes len(methodTypes) methodTypes
        public void OnGenericMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.OnGenericMethodExited(_writer, processId, moduleId, threadId, timestamp, methodId, classTypes, methodTypes);
                CheckPipeStatus(true, false);
            }
        }

        //Message 0x44
        //message is being sent as processId moduleId threadId timestamp pointId
        public void OnPointHit(int processId, int moduleId, int threadId, long timestamp, int pointId) {
            if (ServerFailedToConnect) {
                return;
            }
            lock (_lockObject) {
                NamedPipeMessageSerializer.OnPointHit(_writer, processId, moduleId, threadId, timestamp, pointId);
                CheckPipeStatus(false, false);
            }
        }

        //Message 0xFF
        //message is being sent as processId timestamp
        public void Stop(int processId, long timestamp) {
            if (ServerFailedToConnect) {
                return;
            }
            Debug.WriteLine(string.Format( "NamedPipesIMessageProcessorSenderV1.Stop - Start", Thread.CurrentThread.ManagedThreadId));
            lock (_lockObject) {
                NamedPipeMessageSerializer.Stop(_writer, processId, timestamp);
                CheckPipeStatus(true, true);
            }
            Debug.WriteLine(string.Format("NamedPipesIMessageProcessorSenderV1.Stop - End", Thread.CurrentThread.ManagedThreadId));
            Dispose();
        }

        private void CheckPipeStatus(bool exitEvent, bool force) {
            if (ServerFailedToConnect) {
                return;
            }
            if ((!PipeConnected && _writer.Position > 1024 * 1024) || (PipeConnected && (force || _writer.Position > 1024))) {

                //see if the server is connected and if it is not, then queue the writer and create a new one.
                if (!PipeConnected) {
                    if (QueuedWriters == null) {
                        QueuedWriters = new Queue<ByteArrayWriter>();
                    }
                    _writer.Truncate();
                    QueuedWriters.Enqueue(_writer);
                    _writer = new ByteArrayWriter(8 * 1024 * 1024); //we can send upto an 8 meg message.
                    return;
                }

                //we need to clear the queue before we process the current item.
                if (QueuedWriters != null) {
                    try {
                        while (QueuedWriters.Count > 0) {
                            var currentItem = QueuedWriters.Dequeue();
                            if (currentItem != null) {
                                var tempBuffer = currentItem.Buffer();
                                _pipe.BeginWrite(tempBuffer, 0, currentItem.Position, null, null);
                            }
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine("Error while processing queue" + ex.ToString());
                    }
                    QueuedWriters = null;
                }

                var buffer = _writer.Buffer();
                _pipe.BeginWrite(buffer, 0, _writer.Position, null, null);
                _writer.Reset(); //reset
            }
        }

        private void SetupPipeThread() {
            ThreadPool.QueueUserWorkItem(new WaitCallback(SetupPipe));
        }

        private void SetupPipe(object notUsed) {
 
            Debug.WriteLine("Connecting to the server...");
            try {
                _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
                
                while (true) {
                    if (!NamedPipeDoesNotExist()) {
                        _pipe.Connect(10000); //10 seconds
                        PipeConnected = true;
                        break;
                    }
                    Thread.Sleep(1000);
                }
           
                Debug.WriteLine("Connected successfully");
            }
            catch (Exception ex) {
                ServerFailedToConnect = true;
                QueuedWriters = null; //we are not going to be able to send the items, clear the memory.
                _writer.Truncate();
                Debug.WriteLine("Failed to connect to pipe:" + ex.ToString());
                CleanupPipe();
            }
        }

        public void Dispose() {
            Dispose(true);
        }

        public void Dispose(bool disposing) {
            Debug.WriteLine("NamedPipesIMessageProcessorSenderV1.Dispose - Start:" + disposing);

            CleanupPipe();

            if (disposing) {
                GC.SuppressFinalize(this);
            }
            Debug.WriteLine("NamedPipesIMessageProcessorSenderV1.Dispose - End:" + disposing);
        }

        /// <summary>
        /// Called in dispose and if the server connection fails.
        /// </summary>
        private void CleanupPipe() {

            if (_pipe == null) {
                return;
            }

            try {
                _pipe.Flush();
            }
            catch (Exception) {

            }

            try {
                var timer = Stopwatch.StartNew();
                _pipe.WaitForPipeDrain();
                timer.Stop();
                Debug.WriteLine(string.Format("Server caught up in {0}", timer.Elapsed));
            }
            catch (Exception) {

            }

            _pipe.Dispose();
            _pipe = null;
        }

        ~NamedPipesIMessageProcessorSenderV1() {
            Dispose(false);
        }


        //http://social.msdn.microsoft.com/Forums/en/netfxnetcom/thread/7bbf5a0b-3c22-4836-b271-999e514c321b

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool WaitNamedPipe(string name, int timeout);


        /// <summary>
        /// Provides an indication if the named pipe exists. 
        /// This has to prove the pipe does not exist. If there is any doubt, this
        /// returns that it does exist and it is up to the caller to attempt to connect
        /// to that server. The means that there is a wide variety of errors that can occur that
        /// will be ignored - for example, a pipe name that contains invalid characters will result
        /// in a return value of false.
        /// 
        /// </summary>
        /// <param name="pipeName">The pipe to connect to</param>
        /// <returns>false if it can be proven it does not exist, otherwise true</returns>
        /// <remarks>
        /// Attempts to check if the pipe server exists without incurring the cost
        /// of calling NamedPipeClientStream.Connect. This is because Connect either 
        /// times out and throws an exception or goes into a tight spin loop burning
        /// up cpu cycles if the server does not exist.
        /// 
        /// Common Error codes from WinError.h
        /// ERROR_FILE_NOT_FOUND 2L
        /// ERROR_BROKEN_PIPE =  109 (0x6d)
        /// ERROR_BAD_PATHNAME  161L The specified path is invalid.
        /// ERROR_BAD_PIPE =  230  (0xe6) The pipe state is invalid.
        /// ERROR_PIPE_BUSY =  231 (0xe7) All pipe instances are busy.
        /// ERROR_NO_DATA =   232   (0xe8) the pipe is being closed
        /// ERROR_PIPE_NOT_CONNECTED 233L No process is on the other end of the pipe.
        /// ERROR_PIPE_CONNECTED        535L There is a process on other end of the pipe.
        /// ERROR_PIPE_LISTENING        536L Waiting for a process to open the other end of the pipe.
        /// 
        /// </remarks>
        private bool NamedPipeDoesNotExist() {
            try {
                int timeout = 0;
                string normalizedPath = System.IO.Path.GetFullPath(
                 string.Format(@"\\.\pipe\{0}", PipeName));
                bool exists = WaitNamedPipe(normalizedPath, timeout);
                if (!exists) {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 0) // pipe does not exist
                        return true;
                    else if (error == 2) // win32 error code for file not found
                        return true;
                    // all other errors indicate other issues
                }
                return false;
            }
            catch (Exception ex) {
                //Exception.Publish("Failure in WaitNamedPipe()", ex);
                Debug.WriteLine("NamedPipeDoesNotExist Failed:" + ex.ToString());
                return true; // assume it exists
            }
        }
    }
}
