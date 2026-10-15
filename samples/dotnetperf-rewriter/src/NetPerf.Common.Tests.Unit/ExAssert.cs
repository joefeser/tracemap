using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Common.UnitTests {
	/// <summary>
	/// Extends the default Assert class used by MSTest unit tests.
	/// </summary>
	public static class ExAssert {

		public static void AreEqual<T>(T expected, T actual, IComparer<T> itemComparer = null, string message = null, params object[] messageArgs) {
			itemComparer = itemComparer ?? Comparer<T>.Default;

			if (itemComparer.Compare(expected, actual) != 0) {
				message = string.Concat("AreEqual<", typeof(T).Name,
					"> failed. Expected <", expected.SafeToString(),
					"> but was <", actual.SafeToString(), ">",
					message ?? string.Empty);

				Assert.Fail(message, messageArgs);
			}
		}

		private static string SafeToString<T>(this T item) {
			return item == null ? "NULL" : item.ToString();
		}

		public static void AreEqual<TElement>(ICollection<TElement> expected, ICollection<TElement> actual,
			IComparer<TElement> itemComparer = null, string message = null, params object[] messageArgs) //
		{
			if ((expected == null && actual != null) || (expected != null && actual == null))
				Assert.Fail(message, messageArgs);

			Assert.AreEqual(expected.Count, actual.Count);

			var exIter = expected.GetEnumerator();
			var actIter = actual.GetEnumerator();

			while (exIter.MoveNext() && actIter.MoveNext()) {
				ExAssert.AreEqual<TElement>((TElement)exIter.Current, (TElement)actIter.Current, itemComparer, message, messageArgs);
			}
		}

		public static void IsEmpty<T>(IEnumerable<T> items) {
			if (!items.IsEmpty()) {
				throw new AssertFailedException(string.Format(
					"IsEmpty failed (the collection was not empty)."));
			}
		}

		public static void GreaterThan<T>(T expectedLess, T expectedGreater) where T : IComparable<T> {
			if (expectedLess.CompareTo(expectedGreater) >= 0)
				throw new AssertFailedException(string.Format(
					"GreaterThan failed. Expected that <{0}> was strictly greater than <{1}>",
					expectedLess,
					expectedGreater));
		}

		public static void DoesNotContain(string value, string substring) {
			if (value.Contains(substring))
				throw new AssertFailedException("DoesNotContain failed.");
		}

		public static void ContainsCount(string value, string substring, int expectedOccurances) {
			int lastIndex = -1;
			int actualOccurrances = 0;
			while ((lastIndex = value.IndexOf(substring, lastIndex + 1)) > -1) {
				actualOccurrances += 1;
			}

			if (actualOccurrances != expectedOccurances)
				throw new AssertFailedException(string.Format(
					"ContainsCount failed. Expected {0} occurrances but found {1}", expectedOccurances, actualOccurrances));
		}
	}
}
