using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Pipes;
using System.Diagnostics;
using NetPerf.Core.Interfaces;
using System.Threading;

namespace NetPerf.Core {

    /// <summary>
    /// Class that deals with the instance of the pipe.
    /// </summary>
    public class NamedPipesIMessageProcessorReceiverInstanceV1 : IDisposable {

        private readonly int MILLION = 1024 * 1024;
        private readonly int _internalBufferSize;
        private readonly int _pipeBufferSize;
        //private readonly byte[] _messageBuffer = new byte[8 * 1024 * 1024]; //for now an 8 meg message seems like enough :)
        private int _messageBufferPosition = 0;
        private int _messageFoundMessagePosition = 0; //we do not move every time, so this is the last position we are at looking for the next message.

        private readonly byte[] _messageBuffer = new byte[8 * 1024 * 1024]; //for now an 8 meg message seems like enough :)
        private NamedPipeServerStream _pipe = null;
        private readonly IMessageProcessor _processor = null;

        public Action ConnectCallBack {
            get;
            set;
        }

        public Action<NamedPipesIMessageProcessorReceiverInstanceV1> DisconnectCallBack {
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

        public NamedPipesIMessageProcessorReceiverInstanceV1(string pipeName, int internalBufferSize, int pipeBufferSize, IMessageProcessor processor,
            Action connectCallBack, Action<NamedPipesIMessageProcessorReceiverInstanceV1> disconnectCallBack) {

            if (processor == null) {
                throw new ArgumentNullException("processor");
            }
            if (connectCallBack == null) {
                throw new ArgumentNullException("connectCallBack");
            }
            if (disconnectCallBack == null) {
                throw new ArgumentNullException("disconnectCallBack");
            }
            this.PipeName = pipeName;
            this._internalBufferSize = internalBufferSize;
            this._pipeBufferSize = pipeBufferSize;
            this._processor = processor;
            this.ConnectCallBack = connectCallBack;
            this.DisconnectCallBack = disconnectCallBack;
        }

        public void Start() {
            if (_pipe != null) {
                throw new InvalidOperationException("Pipe is already initialized and was called twice.");
            }
            var thread = new Thread(new ThreadStart(SetupPipe));
            thread.Name = "Pipe Receiver";
            thread.IsBackground = true;
            thread.Start();
        }

        private void SetupPipe() {
            //this needs to be in a try catch because we may abort without ever receiving a connection and this will throw an unhandled exception.
            try {
                Debug.WriteLine(string.Format("Server recieving {0:#,#} byte chunks, pipe buffer is {1:#,#} bytes on thread {2}.", _internalBufferSize, _pipeBufferSize, Thread.CurrentThread.ManagedThreadId));

                var timer = new Stopwatch();
                var byteBuffer = new byte[_internalBufferSize];
                var bytesRead = 0;

                _pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 4,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough, _pipeBufferSize, _pipeBufferSize);

                Debug.WriteLine(string.Format("waiting for a connection on thread {0}", Thread.CurrentThread.ManagedThreadId));
                _pipe.WaitForConnection();
                Debug.WriteLine(string.Format("connection recieved on thread {0}", Thread.CurrentThread.ManagedThreadId));
                ConnectCallBack();

                timer.Start();
                do {
                    if (_pipe != null) {
                        bytesRead = _pipe.Read(byteBuffer, 0, byteBuffer.Length);
                        Buffer.BlockCopy(byteBuffer, 0, _messageBuffer, _messageBufferPosition, bytesRead);
                        _messageBufferPosition += bytesRead;
                        ProcessForMessages();
                    }
                } while (bytesRead > 0 && _pipe != null);

                ProcessForMessages();
                timer.Stop();
                DisconnectCallBack(this);
            }
            catch (System.IO.IOException io) {
                //do nothing
                if (_pipe != null) {
                    Debug.WriteLine(string.Format("Most likely a second pipe was killed waiting for a connection on thread {0}. Reason: {1}", Thread.CurrentThread.ManagedThreadId, io.ToString()));
                }
            }
            catch (Exception ex) {
                Debug.WriteLine(string.Format("Failed waiting for a connection on thread {0}. Reason: {1}", Thread.CurrentThread.ManagedThreadId, ex.ToString()));
            }
        }

        private void ProcessForMessages() {
            //only move back to zero once we get to one meg.
            _messageFoundMessagePosition = NamedPipeMessageSerializer.ProcessArrayForMessages(_processor, _messageFoundMessagePosition,
                _messageBuffer, _messageBufferPosition);

            if (_messageFoundMessagePosition >= MILLION) {
                if (_messageFoundMessagePosition == _messageBufferPosition) {
                    _messageBufferPosition = 0;
                    _messageFoundMessagePosition = 0;
                }
                else {
                    //move to the beginning
                    Buffer.BlockCopy(_messageBuffer, _messageFoundMessagePosition, _messageBuffer, 0, _messageBufferPosition - _messageFoundMessagePosition);
                    _messageBufferPosition = _messageBufferPosition - _messageFoundMessagePosition;
                    _messageFoundMessagePosition = 0;
                }
            }
        }

        public void Dispose() {
            Dispose(true);
        }

        public void Dispose(bool disposing) {
            if (_pipe == null) {
                return;
            }

            if (_pipe.IsConnected) {
                try {
                    _pipe.Disconnect();
                }
                catch (Exception) {
                }
                try {
                    ProcessForMessages();
                }
                catch (Exception) {
                }
            }

            try {
                _pipe.Dispose();
            }
            catch (Exception) {
            }

            _pipe = null;
            if (disposing) {
                GC.SuppressFinalize(this);
            }
        }

        ~NamedPipesIMessageProcessorReceiverInstanceV1() {
            Dispose(false);
        }
    }
}
