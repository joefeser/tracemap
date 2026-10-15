using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using NetPerf.Core.Interfaces;

namespace NetPerf.Core {

    /// <summary>
    /// The Named Pipes Receiver used to collect the messages over the wire.
    /// </summary>
    /// <remarks>We implement the interface because we call the methods when a message is received of that type.</remarks>
    public class NamedPipesIMessageProcessorReceiverV1 : IDisposable {

        private readonly List<NamedPipesIMessageProcessorReceiverInstanceV1> _receivers = new List<NamedPipesIMessageProcessorReceiverInstanceV1>();
        private object _lockObject = new object();

        //we use this to process the messages.
        private readonly IMessageProcessor _processor = null;

        public int InternalBufferSize {
            get;
            set;
        }

        public int PipeBufferSize {
            get;
            set;
        }

        public IMessageProcessor Processor {
            get {
                return _processor;
            }
        }

        /// <summary>
        /// The name of the pipe.
        /// </summary>
        public string PipeName {
            get;
            private set;
        }

        public NamedPipesIMessageProcessorReceiverV1(IMessageProcessor processor, string pipeName, int internalBufferSize, int pipeBufferSize) {
            if (processor == null) {
                throw new ArgumentNullException("processor");
            }
            if (string.IsNullOrEmpty(pipeName) || pipeName.Trim().Length == 0) {
                throw new ArgumentNullException("pipeName");
            }
            if (internalBufferSize <= 0) {
                throw new ArgumentOutOfRangeException("internalBufferSize");
            }
            if (pipeBufferSize <= 0) {
                throw new ArgumentOutOfRangeException("pipeBufferSize");
            }
            _processor = processor;
            this.PipeName = pipeName;
            this.InternalBufferSize = internalBufferSize;
            this.PipeBufferSize = pipeBufferSize;
        }

        public void Start() {
            CreateReceiverInstance();
        }

        public void Stop() {
            //ProcessForMessages();
            Console.WriteLine("Server Stop Thread:{0} - Start", Thread.CurrentThread.ManagedThreadId);
            Debug.WriteLine(string.Format( "Server Stop Thread:{0} - Start", Thread.CurrentThread.ManagedThreadId));
            Dispose(true);
            Console.WriteLine("Server Stop Thread:{0} - End", Thread.CurrentThread.ManagedThreadId);
            Debug.WriteLine(string.Format("Server Stop Thread:{0} - End", Thread.CurrentThread.ManagedThreadId));
        }

        public void Dispose() {
            Dispose(true);
        }

        public void Dispose(bool disposing) {
            if (_receivers == null || _receivers.Count == 0) {
                return;
            }

            Console.WriteLine("Server Dispose Thread:{0} Disposing: {1} - Start", Thread.CurrentThread.ManagedThreadId, disposing);

            //copy the list
            foreach (var item in _receivers.ToArray()) {
                try {
                    item.Dispose(disposing);
                    lock (_lockObject) {
                        _receivers.Remove(item);
                    }
                }
                catch (Exception) {

                }
            }

            Console.WriteLine("Server Dispose Thread:{0} Disposing: {1} - End", Thread.CurrentThread.ManagedThreadId, disposing);

            if (disposing) {
                GC.SuppressFinalize(this);
            }
        }

        ~NamedPipesIMessageProcessorReceiverV1() {
            Dispose(false);
        }

        private void CreateReceiverInstance() {
            if (_receivers.Count >= 4) {
                return;
            }
            lock (_lockObject) {
                var instance = new NamedPipesIMessageProcessorReceiverInstanceV1(PipeName, InternalBufferSize, PipeBufferSize, Processor, ConnectedCallBack, DisconnectedCallBack);
                _receivers.Add(instance);
                instance.Start();
            }
        }

        /// <summary>
        /// When the pipe connects, this callback is called so we can spin up a new instance.
        /// </summary>
        private void ConnectedCallBack() {
            Console.WriteLine("Client is connected. Thread:{0}", Thread.CurrentThread.ManagedThreadId);
            CreateReceiverInstance();
        }

        /// <summary>
        /// When the pipe connects, this callback is called so we can spin up a new instance.
        /// </summary>
        private void DisconnectedCallBack(NamedPipesIMessageProcessorReceiverInstanceV1 item) {
            try {
                Console.WriteLine("Client is disconnected. Thread:{0}", Thread.CurrentThread.ManagedThreadId);
                Debug.WriteLine(string.Format("Client is disconnected. Thread:{0}", Thread.CurrentThread.ManagedThreadId));
                lock (_lockObject) {
                    _receivers.Remove(item);
                }
                item.Dispose();
            }
            catch (Exception ex) {
                Debug.WriteLine(string.Format("DisconnectedCallBack failed on thread {0}: {1}", Thread.CurrentThread.ManagedThreadId, ex.ToString()));
            }
        }
    }
}
