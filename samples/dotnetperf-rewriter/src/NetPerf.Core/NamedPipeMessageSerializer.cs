using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Core.Interfaces;

namespace NetPerf.Core {

    /// <summary>
    /// Class that is designed to serialize the messages that will be sent over the wire.
    /// </summary>
    public static class NamedPipeMessageSerializer {

        private static Encoding _encoder = UTF8Encoding.UTF8;

        public static int ProcessArrayForMessages(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {

            var startPosition = messagePosition;
           
            do {
                startPosition = messagePosition;
                messagePosition = ProcessForMessage(processor, messagePosition, buffer, bufferLength);
            } while (messagePosition > startPosition);
            return messagePosition;
        }

        private static int ProcessForMessage(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {
            //No message is under 14 bytes
            if (messagePosition + 14 <= bufferLength) {
                //get the message number
                var message = buffer[messagePosition];

                switch (message) {
                    case 0x01:
                        return OnNewProcess(processor, messagePosition, buffer, bufferLength);
                    case 0x02:
                        return OnNewThread(processor, messagePosition, buffer, bufferLength);
                    case 0x03:
                        return OnNewAssembly(processor, messagePosition, buffer, bufferLength);
                    case 0x04:
                        return RegisterMetadata(processor, messagePosition, buffer, bufferLength);
                    case 0x40:
                        return OnMethodEntered(processor, messagePosition, buffer, bufferLength);
                    case 0x41:
                        return OnGenericMethodEntered(processor, messagePosition, buffer, bufferLength);
                    case 0x42:
                        return OnMethodExited(processor, messagePosition, buffer, bufferLength);
                    case 0x43:
                        return OnGenericMethodExited(processor, messagePosition, buffer, bufferLength);
                    case 0x44:
                        return OnPointHit(processor, messagePosition, buffer, bufferLength);
                    case 0xFF:
                        return Stop(processor, messagePosition, buffer, bufferLength);
                    default:
                        //TODO what do we do with messages that we do not understand.
                        throw new ApplicationException("Unknown message id.");
                    //break;
                }
            }
            return messagePosition;
        }

        //Message 0x01
        //message is being sent as len(short)(message) processId ticksPerSecond timestamp len(short)(processName) processName len(short)(machineName) machineName 
        public static void OnNewProcess(ByteArrayWriter writer, int processId, long ticksPerSecond, long timestamp, string processName, string machineName) {

            //determine the length of the message
            var processBytes = writer.GetBytes(processName);
            var machineNameBytes = writer.GetBytes(machineName);

            var messageLength = 4 + 8 + 8 + 2 + processBytes.Length + 2 + machineNameBytes.Length;
            writer.Write((byte)0x01); //message number
            writer.Write((byte)0); //version (0 is v1)
            writer.Write((short)messageLength); //the message length minus the header
            writer.Write(processId);
            writer.Write(ticksPerSecond);
            writer.Write(timestamp);

            writer.Write((short)processBytes.Length);
            writer.Write(processBytes);

            writer.Write((short)machineNameBytes.Length);
            writer.Write(machineNameBytes);
        }

        //Message 0x01
        //message is being sent as len(short)(message) processId ticksPerSecond timestamp len(short)(processName) processName len(short)(machineName) machineName 
        public static int OnNewProcess(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {

            var headerPlusMessageSize = 4;
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {
                var messageLength = BitConverter.ToInt16(buffer, messagePosition + 2); //skip message and version and read length
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length
                    var processId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var ticksPerSecond = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;
                    var timestamp = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;
                    var processNameLength = BitConverter.ToInt16(buffer, messagePosition);
                    messagePosition += 2;
                    var processName = _encoder.GetString(buffer, messagePosition, processNameLength);
                    messagePosition += processNameLength;
                    var machineNameLength = BitConverter.ToInt16(buffer, messagePosition);
                    messagePosition += 2;
                    var machineName = _encoder.GetString(buffer, messagePosition, machineNameLength);
                    messagePosition += machineNameLength;
                    processor.OnNewProcess(processId, ticksPerSecond, timestamp, processName, machineName);
                }
            }

            return messagePosition;
        }

        //Message 0x02
        //message is being sent as len(short)(message) processId timestamp threadId threadName - Threadname is len(message) - 16
        public static void OnNewThread(ByteArrayWriter writer, int processId, long timestamp, int threadId, string threadName) {

            //determine the length of the message
            var threadNameBytes = writer.GetBytes(threadName);

            var messageLength = 4 + 8 + 4 + threadNameBytes.Length;
            writer.Write((byte)0x02); //message number
            writer.Write((byte)0); //version (0 is v1)
            writer.Write((short)messageLength); //the message length minus the header

            writer.Write(processId);
            writer.Write(timestamp);
            writer.Write(threadId);
            writer.Write(threadNameBytes);
        }

        //Message 0x02
        //message is being sent as len(short)(message) processId timestamp threadId threadName - Threadname is len(message) - 16
        public static int OnNewThread(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {

            var headerPlusMessageSize = 4;
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {

                var messageLength = BitConverter.ToInt16(buffer, messagePosition + 2); //skip message and version and read length
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length
                    var processId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var timestamp = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;
                    var threadId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;

                    var threadName = _encoder.GetString(buffer, messagePosition, messageLength - 16);
                    messagePosition += messageLength - 16;

                    processor.OnNewThread(processId, timestamp, threadId, threadName);
                }
            }

            return messagePosition;
        }

        //Message 0x03
        //message is being sent as len(short)(message) processId timestamp assemblyIdentity - assemblyIdentity is len(message) - 12
        public static void OnNewAssembly(ByteArrayWriter writer, int processId, long timestamp, string assemblyIdentity) {

            //determine the length of the message
            var assemblyIdentityBytes = writer.GetBytes(assemblyIdentity);

            var messageLength = 4 + 8 + assemblyIdentityBytes.Length;
            writer.Write((byte)0x03); //message number
            writer.Write((byte)0); //version (0 is v1)
            writer.Write((short)messageLength); //the message length minus the header

            writer.Write(processId);
            writer.Write(timestamp);
            writer.Write(assemblyIdentityBytes);
        }

        //Message 0x03
        //message is being sent as len(short)(message) processId timestamp assemblyIdentity - assemblyIdentity is len(message) - 12
        public static int OnNewAssembly(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {

            var headerPlusMessageSize = 4;
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {

                var messageLength = BitConverter.ToInt16(buffer, messagePosition + 2); //skip message and version and read length
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length
                    var processId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var timestamp = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;

                    var assemblyIdentity = _encoder.GetString(buffer, messagePosition, messageLength - 12);
                    messagePosition += messageLength - 12;

                    processor.OnNewAssembly(processId, timestamp, assemblyIdentity);
                }
            }

            return messagePosition;
        }

        //The new clients will know what to do with this, for now it is just for the new SqLite Client
        //Message 0x04
        //message is being sent as len(int)(message) (len short)assemblyName assemblyName len(ilMetadata) ilMetadata
        public static void RegisterMetadata(ByteArrayWriter writer, string assemblyName, string ilMetadata) {

            //determine the length of the message
            var assemblyNameBytes = writer.GetBytes(assemblyName);
            var ilMetadataBytes = writer.GetBytes(ilMetadata);

            var messageLength = 2 + assemblyNameBytes.Length + 2 + ilMetadataBytes.Length;
            writer.Write((byte)0x04); //message number
            writer.Write((byte)0); //version (0 is v1)
            writer.Write((int)messageLength); //the message length minus the header

            writer.Write((short)assemblyNameBytes.Length);
            writer.Write(assemblyNameBytes);

            writer.Write((int)ilMetadataBytes.Length);
            writer.Write(ilMetadataBytes);

        }

        //Message 0x04
        //message is being sent as len(int)(message) (len)assemblyName assemblyName len(ilMetadata) ilMetadata
        public static int RegisterMetadata(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {

            var headerPlusMessageSize = 6; //int
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {

                var messageLength = BitConverter.ToInt32(buffer, messagePosition + 2); //skip message and version and read length
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length

                    var assemblyNameLength = BitConverter.ToInt16(buffer, messagePosition);
                    messagePosition += 2;

                    var assemblyName = _encoder.GetString(buffer, messagePosition, assemblyNameLength);
                    messagePosition += assemblyNameLength;

                    var ilMetadataLength = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;

                    var ilMetadata = _encoder.GetString(buffer, messagePosition, ilMetadataLength);
                    messagePosition += ilMetadataLength;

                    processor.RegisterMetadata(assemblyName, ilMetadata);
                }
            }
            return messagePosition;
        }

        //we want to leave room for some registration messages

        //Message 0x40
        //message is being sent as processId moduleId threadId timestamp methodId
        public static void OnMethodEntered(ByteArrayWriter writer, int processId, int moduleId, int threadId, long timestamp, int methodId) {

            writer.Write((byte)0x40); //message number
            writer.Write((byte)0); //version (0 is v1)

            writer.Write(processId);
            writer.Write(moduleId);
            writer.Write(threadId);
            writer.Write(timestamp);
            writer.Write(methodId);
        }

        //Message 0x40
        //message is being sent as processId moduleId threadId timestamp methodId
        public static int OnMethodEntered(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {

            var headerPlusMessageSize = 2; //no length
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {

                var messageLength = 24;
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length

                    var processId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var moduleId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var threadId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var timestamp = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;
                    var methodId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    processor.OnMethodEntered(processId, moduleId, threadId, timestamp, methodId);
                }
            }

            return messagePosition;
        }

        //Message 0x41
        //message is being sent as len(int)(message) processId moduleId threadId timestamp methodId len(classTypes) classTypes len(methodTypes) methodTypes
        public static void OnGenericMethodEntered(ByteArrayWriter writer, int processId, int moduleId, int threadId, long timestamp,
                                                    int methodId, string[] classTypes, string[] methodTypes) {

            CreateOnGenericMethod(writer, 0x41, processId, moduleId, threadId, timestamp, methodId, classTypes, methodTypes);
        }

        //Message 0x41
        //message is being sent as len(int)(message) processId moduleId threadId timestamp methodId len(classTypes) classTypes len(methodTypes) methodTypes
        public static int OnGenericMethodEntered(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {
            //see if we have enough of a buffer to read the length
            return ProcessOnGenericMethod(processor, 0x41, messagePosition, buffer, bufferLength);
        }

        //Message 0x42
        //message is being sent as processId moduleId threadId timestamp methodId
        public static void OnMethodExited(ByteArrayWriter writer, int processId, int moduleId, int threadId, long timestamp, int methodId) {

            writer.Write((byte)0x42); //message number
            writer.Write((byte)0); //version (0 is v1)

            writer.Write(processId);
            writer.Write(moduleId);
            writer.Write(threadId);
            writer.Write(timestamp);
            writer.Write(methodId);
        }

        //Message 0x42
        //message is being sent as processId moduleId threadId timestamp methodId
        public static int OnMethodExited(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {
            var headerPlusMessageSize = 2; //no length
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {

                var messageLength = 24;
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length

                    var processId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var moduleId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var threadId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var timestamp = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;
                    var methodId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    processor.OnMethodExited(processId, moduleId, threadId, timestamp, methodId);
                }
            }
            return messagePosition;
        }

        //Message 0x43
        //message is being sent as len(int)(message) processId moduleId threadId timestamp methodId len(classTypes) classTypes len(methodTypes) methodTypes
        public static void OnGenericMethodExited(ByteArrayWriter writer, int processId, int moduleId, int threadId, long timestamp,
                                                    int methodId, string[] classTypes, string[] methodTypes) {
            CreateOnGenericMethod(writer, 0x43, processId, moduleId, threadId, timestamp, methodId, classTypes, methodTypes);
        }

        //Message 0x43
        //message is being sent as len(int)(message) processId moduleId threadId timestamp methodId len(classTypes) classTypes len(methodTypes) methodTypes
        public static int OnGenericMethodExited(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {
            //see if we have enough of a buffer to read the length
            return ProcessOnGenericMethod(processor, 0x43, messagePosition, buffer, bufferLength);
        }

        //Message 0x44
        //message is being sent as processId moduleId threadId timestamp pointId
        public static void OnPointHit(ByteArrayWriter writer, int processId, int moduleId, int threadId, long timestamp, int pointId) {

            writer.Write((byte)0x44); //message number
            writer.Write((byte)0); //version (0 is v1)

            writer.Write(processId);
            writer.Write(moduleId);
            writer.Write(threadId);
            writer.Write(timestamp);
            writer.Write(pointId);

        }

        //Message 0x44
        //message is being sent as processId moduleId threadId timestamp pointId
        public static int OnPointHit(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {
            var headerPlusMessageSize = 2; //no length
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {

                var messageLength = 24;
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length

                    var processId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var moduleId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var threadId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var timestamp = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;
                    var pointId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    processor.OnPointHit(processId, moduleId, threadId, timestamp, pointId);
                }
            }
            return messagePosition;
        }

        //Message 0xFF
        //message is being sent as processId timestamp
        public static void Stop(ByteArrayWriter writer, int processId, long timestamp) {

            writer.Write((byte)0xFF); //message number
            writer.Write((byte)0); //version (0 is v1)

            writer.Write(processId);
            writer.Write(timestamp);
        }

        //Message 0xFF
        //message is being sent as processId timestamp
        public static int Stop(IMessageProcessor processor, int messagePosition, byte[] buffer, int bufferLength) {
            var headerPlusMessageSize = 2; //no length
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {

                var messageLength = 12;
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length

                    var processId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var timestamp = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;
                    processor.Stop(processId, timestamp);
                }
            }
            return messagePosition;
        }

        //Message 0x41 or 0x43
        //message is being sent as len(int)(message) processId moduleId threadId timestamp methodId len(classTypes) classTypes len(methodTypes) methodTypes
        private static void CreateOnGenericMethod(ByteArrayWriter writer, byte messageId, int processId, int moduleId, int threadId, long timestamp,
                                                    int methodId, string[] classTypes, string[] methodTypes) {

            //determine the length of the message
            var classTypeBytes = writer.GetBytes(classTypes != null ? string.Join(",", classTypes) : string.Empty);
            var methodTypeBytes = writer.GetBytes(methodTypes != null ? string.Join(",", methodTypes) : string.Empty);

            var messageLength = 4 + 4 + 4 + 8 + 4 + 2 + classTypeBytes.Length + 2 + methodTypeBytes.Length;
            writer.Write(messageId); //message number
            writer.Write((byte)0); //version (0 is v1)
            writer.Write((short)messageLength); //the message length minus the header

            writer.Write(processId);
            writer.Write(moduleId);
            writer.Write(threadId);
            writer.Write(timestamp);
            writer.Write(methodId);

            writer.Write((short)classTypeBytes.Length);
            writer.Write(classTypeBytes);

            writer.Write((short)methodTypeBytes.Length);
            writer.Write(methodTypeBytes);

        }

        //Message 0x41 or 0x43
        //message is being sent as len(int)(message) processId moduleId threadId timestamp methodId len(classTypes) classTypes len(methodTypes) methodTypes
        private static int ProcessOnGenericMethod(IMessageProcessor processor, byte messageId, int messagePosition, byte[] buffer, int bufferLength) {

            var headerPlusMessageSize = 4; //short
            //see if we have enough of a buffer to read the length

            if (messagePosition + headerPlusMessageSize < bufferLength) {

                var messageLength = BitConverter.ToInt16(buffer, messagePosition + 2); //skip message and version and read length
                //now see if the messagelengh + headerPlusMessageSize exists in the buffer.
                if (messagePosition + messageLength + headerPlusMessageSize <= bufferLength) {
                    messagePosition += headerPlusMessageSize; //message plus length

                    var processId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var moduleId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var threadId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;
                    var timestamp = BitConverter.ToInt64(buffer, messagePosition);
                    messagePosition += 8;
                    var methodId = BitConverter.ToInt32(buffer, messagePosition);
                    messagePosition += 4;

                    var classTypesLength = BitConverter.ToInt16(buffer, messagePosition);
                    messagePosition += 2;

                    var classTypes = classTypesLength > 0 ? _encoder.GetString(buffer, messagePosition, classTypesLength) : string.Empty;
                    messagePosition += classTypesLength;

                    var methodTypesLength = BitConverter.ToInt16(buffer, messagePosition);
                    messagePosition += 2;

                    var methodTypes = methodTypesLength > 0 ? _encoder.GetString(buffer, messagePosition, methodTypesLength) : string.Empty;
                    messagePosition += methodTypesLength;

                    if (messageId == 0x41) {
                        processor.OnGenericMethodEntered(processId, moduleId, threadId, timestamp, methodId, classTypes.Split(','), methodTypes.Split(','));
                    }
                    else {
                        processor.OnGenericMethodExited(processId, moduleId, threadId, timestamp, methodId, classTypes.Split(','), methodTypes.Split(','));
                    }

                }
            }

            return messagePosition;
        }

    }
}
