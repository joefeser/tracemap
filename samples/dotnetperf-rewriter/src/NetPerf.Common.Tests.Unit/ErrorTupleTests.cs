using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Common.Tests {
	[TestClass]
	public class ErrorTupleTests {

		[TestMethod]
		public void DefaultConstructor_RepresentsSuccessfulReturn() {
			var c = new ErrorTuple();

			Assert.IsNotNull(c.ErrorMessage);
			Assert.IsTrue(c.WasSuccess);
			Assert.IsFalse(c.HasError);
		}

		[TestMethod]
		public void ErrorConstructor_NullFormatArgs_ErrorMessageIsSetToMessageFormat() {
			var message = "error message blah";
			var c = new ErrorTuple(message);

			Assert.AreEqual(message, c.ErrorMessage);
		}

		[TestMethod]
		public void ErrorConstructor_EmptyFormatArgs_ErrorMessageIsSetToMessageFormat() {
			var message = "error message blah";
			var c = new ErrorTuple(message, new object[0]);

			Assert.AreEqual(message, c.ErrorMessage);
		}

		[TestMethod, ExpectedException(typeof(FormatException))]
		public void ErrorConstructor_NotEnoughFormatArgsProvided_ExceptionThrown() {
			var message = "error message blah {2}";
			var c = new ErrorTuple(message, 2);

			Assert.AreEqual(message, c.ErrorMessage);
		}

		[TestMethod]
		public void ErrorConstructor_InterpolatesFormatArgsIntoMessageFormat() {
			var message = "{1} error {2} message {0}";
			var c = new ErrorTuple(message, 1, 2, 3);

			Assert.AreEqual("2 error 3 message 1", c.ErrorMessage);
		}

		[TestMethod]
		public void ErrorConstructor_RepresentsFailure() {
			var c = new ErrorTuple("error message blah");

			Assert.IsNotNull(c.ErrorMessage);
			Assert.IsFalse(c.WasSuccess);
			Assert.IsTrue(c.HasError);
		}

		[TestMethod]
		public void ImplicitBooleanConversion_ObjectRepresentsFailure_ConversionReturnsFalse() {
			var c = new ErrorTuple("error message");

			Assert.IsFalse(c);
		}

		[TestMethod]
		public void ImplicitBooleanConversion_ObjectRepresentsSuccess_ConversionReturnsTrue() {
			var c = new ErrorTuple();

			Assert.IsTrue(c);
		}

		[TestMethod]
		public void Create_RepresentsSuccessfulReturn() {
			var c = ErrorTuple.Create("hello there");

			Assert.IsNotNull(c.ErrorMessage);
			Assert.IsTrue(c.WasSuccess);
			Assert.IsFalse(c.HasError);
		}

		[TestMethod]
		public void Create_ParameterIsAssignedToDataProperty() {
			var c = ErrorTuple.Create(45);

			Assert.AreEqual(45, c.Data);
		}

	}

	[TestClass]
	public class ErrorTupleOfTTests {

		[TestMethod]
		public void DefaultConstructor_RepresentsSuccessfulReturn() {
			var c = new ErrorTuple<int>(34);

			Assert.IsNotNull(c.ErrorMessage);
			Assert.IsTrue(c.WasSuccess);
			Assert.IsFalse(c.HasError);
		}

		[TestMethod]
		public void DefaultConstructor_ParameterIsAssignedToDataProperty() {
			var c = new ErrorTuple<double>(0.0);

			Assert.AreEqual(0.0, c.Data);
		}

		[TestMethod]
		public void ErrorConstructor_NullFormatArgs_ErrorMessageIsSetToMessageFormat() {
			var message = "error message blah";
			var c = new ErrorTuple<int>(message);

			Assert.AreEqual(message, c.ErrorMessage);
		}

		[TestMethod]
		public void ErrorConstructor_EmptyFormatArgs_ErrorMessageIsSetToMessageFormat() {
			var message = "error message blah";
			var c = new ErrorTuple<int>(message, new object[0]);

			Assert.AreEqual(message, c.ErrorMessage);
		}

		[TestMethod, ExpectedException(typeof(FormatException))]
		public void ErrorConstructor_NotEnoughFormatArgsProvided_ExceptionThrown() {
			var message = "error message blah {2}";
			var c = new ErrorTuple<int>(message, 2);

			Assert.AreEqual(message, c.ErrorMessage);
		}

		[TestMethod]
		public void ErrorConstructor_InterpolatesFormatArgsIntoMessageFormat() {
			var message = "{1} error {2} message {0}";
			var c = new ErrorTuple<int>(message, 1, 2, 3);

			Assert.AreEqual("2 error 3 message 1", c.ErrorMessage);
		}

		[TestMethod]
		public void ErrorConstructor_RepresentsFailure() {
			var c = new ErrorTuple<int>("error message blah");

			Assert.IsNotNull(c.ErrorMessage);
			Assert.IsFalse(c.WasSuccess);
			Assert.IsTrue(c.HasError);
		}

		[TestMethod]
		public void ImplicitBooleanConversion_ObjectRepresentsFailure_ConversionReturnsFalse() {
			var c = new ErrorTuple<int>("error message");

			Assert.IsFalse(c);
		}

		[TestMethod]
		public void ImplicitBooleanConversion_ObjectRepresentsSuccess_ConversionReturnsTrue() {
			var c = new ErrorTuple<int>(34);

			Assert.IsTrue(c);
		}

	}
}
