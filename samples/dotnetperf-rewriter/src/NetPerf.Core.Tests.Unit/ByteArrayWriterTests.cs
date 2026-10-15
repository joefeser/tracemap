using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Core.Tests.Unit {

	[TestClass]
	public class ByteArrayWriterTests {

		[TestMethod]
		public void ByteArrayWriterTests_Test_Bool_False() {
			var item = new ByteArrayWriter(4);
			item.Write(false);
			Assert.AreEqual<byte>(0, item.Buffer()[0]);
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_Bool_True() {
			var item = new ByteArrayWriter(4);
			item.Write(true);
			Assert.AreEqual<byte>(1, item.Buffer()[0]);
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_Four_Bytes() {
			var item = new ByteArrayWriter(4);
			var bytes = new byte[] { 12, 24, 36, 48 };
			item.Write(bytes);
			Assert.AreEqual<byte>(12, item.Buffer()[0]);
			Assert.AreEqual<byte>(24, item.Buffer()[1]);
			Assert.AreEqual<byte>(36, item.Buffer()[2]);
			Assert.AreEqual<byte>(48, item.Buffer()[3]);
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_Four_Bytes_Not_Zero_Based_Start() {
			var item = new ByteArrayWriter(12);
			var bytes = new byte[] { 0, 0, 0, 0 };
			item.Write(bytes);

			var val = int.MaxValue;
			var intBytes = BitConverter.GetBytes(val);
			//place the int at the 4th position
			var bigBytes = new byte[12];
			System.Buffer.BlockCopy(intBytes, 0, bigBytes, 4, 4);

			//now write that buffer to the writer
			item.Write(bigBytes, 4, 4);

			Assert.AreEqual<byte>(intBytes[0], item.Buffer()[4]);
			Assert.AreEqual<byte>(intBytes[1], item.Buffer()[5]);
			Assert.AreEqual<byte>(intBytes[2], item.Buffer()[6]);
			Assert.AreEqual<byte>(intBytes[3], item.Buffer()[7]);

			Assert.AreEqual<int>(val, BitConverter.ToInt32(item.Buffer(), 4));

		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_Short() {
			var item = new ByteArrayWriter(2);
			item.Write((short)75);
			var bytes = BitConverter.GetBytes((short)75);
			Assert.AreEqual<byte>(bytes[0], item.Buffer()[0]);
			Assert.AreEqual<byte>(bytes[1], item.Buffer()[1]);
			Assert.AreEqual<short>(75, BitConverter.ToInt16(item.Buffer(), 0));
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_Int() {
			var item = new ByteArrayWriter(4);
			item.Write(int.MaxValue);
			var bytes = BitConverter.GetBytes(int.MaxValue);
			Assert.AreEqual<byte>(bytes[0], item.Buffer()[0]);
			Assert.AreEqual<byte>(bytes[1], item.Buffer()[1]);
			Assert.AreEqual<byte>(bytes[2], item.Buffer()[2]);
			Assert.AreEqual<byte>(bytes[3], item.Buffer()[3]);
			Assert.AreEqual<int>(int.MaxValue, BitConverter.ToInt32(item.Buffer(), 0));
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_Long() {
			var item = new ByteArrayWriter(8);
			item.Write(long.MaxValue);
			var bytes = BitConverter.GetBytes(long.MaxValue);
			Assert.AreEqual<byte>(bytes[0], item.Buffer()[0]);
			Assert.AreEqual<byte>(bytes[1], item.Buffer()[1]);
			Assert.AreEqual<byte>(bytes[2], item.Buffer()[2]);
			Assert.AreEqual<byte>(bytes[3], item.Buffer()[3]);
			Assert.AreEqual<byte>(bytes[4], item.Buffer()[4]);
			Assert.AreEqual<byte>(bytes[5], item.Buffer()[5]);
			Assert.AreEqual<byte>(bytes[6], item.Buffer()[6]);
			Assert.AreEqual<byte>(bytes[7], item.Buffer()[7]);
			Assert.AreEqual<long>(long.MaxValue, BitConverter.ToInt64(item.Buffer(), 0));
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_Byte() {
			var item = new ByteArrayWriter(1);
			var val = (byte)75;
			item.Write(val);
			var bytes = BitConverter.GetBytes(val);
			Assert.AreEqual<byte>(bytes[0], item.Buffer()[0]);
			Assert.AreEqual<byte>(val, item.Buffer()[0]);
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_SByte() {
			var item = new ByteArrayWriter(1);
			var val = (sbyte)75;
			item.Write(val);
			var bytes = BitConverter.GetBytes(val);
			Assert.AreEqual<sbyte>((sbyte)bytes[0], (sbyte)item.Buffer()[0]);
			Assert.AreEqual<sbyte>(val, (sbyte)item.Buffer()[0]);
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_UShort() {
			var item = new ByteArrayWriter(2);
			var val = (ushort)7556;
			item.Write(val);
			var bytes = BitConverter.GetBytes(val);
			Assert.AreEqual<byte>(bytes[0], item.Buffer()[0]);
			Assert.AreEqual<byte>(bytes[1], item.Buffer()[1]);

			Assert.AreEqual<ushort>(val, BitConverter.ToUInt16(item.Buffer(), 0));
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_UInt() {
			var item = new ByteArrayWriter(4);
			var val = UInt32.MaxValue;
			item.Write(val);
			var bytes = BitConverter.GetBytes(val);
			Assert.AreEqual<byte>(bytes[0], item.Buffer()[0]);
			Assert.AreEqual<byte>(bytes[1], item.Buffer()[1]);
			Assert.AreEqual<byte>(bytes[2], item.Buffer()[2]);
			Assert.AreEqual<byte>(bytes[3], item.Buffer()[3]);

			Assert.AreEqual<uint>(val, BitConverter.ToUInt32(item.Buffer(), 0));
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_ULong() {
			var item = new ByteArrayWriter(8);
			var val = ulong.MaxValue;
			item.Write(val);
			var bytes = BitConverter.GetBytes(val);
			Assert.AreEqual<byte>(bytes[0], item.Buffer()[0]);
			Assert.AreEqual<byte>(bytes[1], item.Buffer()[1]);
			Assert.AreEqual<byte>(bytes[2], item.Buffer()[2]);
			Assert.AreEqual<byte>(bytes[3], item.Buffer()[3]);
			Assert.AreEqual<byte>(bytes[4], item.Buffer()[4]);
			Assert.AreEqual<byte>(bytes[5], item.Buffer()[5]);
			Assert.AreEqual<byte>(bytes[6], item.Buffer()[6]);
			Assert.AreEqual<byte>(bytes[7], item.Buffer()[7]);
			Assert.AreEqual<ulong>(val, BitConverter.ToUInt64(item.Buffer(), 0));
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_String_Regular() {
			var item = new ByteArrayWriter(4);
			var val = "Test";
			item.Write(val);
			var bytes = UTF8Encoding.UTF8.GetBytes(val);
			Assert.AreEqual<int>(4, bytes.Length);
			Assert.AreEqual<string>(val, UTF8Encoding.UTF8.GetString(item.Buffer()));
		}

		[TestMethod]
		public void ByteArrayWriterTests_Test_String_Unicode() {
			//sample arabic string that should be 10 bytes ؟بحٍ۳
			var item = new ByteArrayWriter(10);
			var val = "؟بحٍ۳";
			item.Write(val);
			var bytes = UTF8Encoding.UTF8.GetBytes(val);
			Assert.AreEqual<int>(10, bytes.Length);
			Assert.AreEqual<string>(val, UTF8Encoding.UTF8.GetString(item.Buffer()));
		}

	}
}
