using System.Collections.Generic;
using System.Text;

namespace System.Linq {
	public static class LinqExtensions {
		public static IEnumerable<T> Concat<T>(this T head, IEnumerable<T> tail) {
			if (head == null)
				return tail;

			return new[] { head }.Concat(tail);
		}

		public static bool Contains<T>(this IEnumerable<T> items, Func<T, bool> predicate) {
			return items.Any(predicate);
		}

		public static bool ContainsAny<T>(this IEnumerable<T> items, IEnumerable<T> itemsToCheck, Func<T, T, bool> equalityComparer) {
			return items.Any(i => itemsToCheck.Any(ii => equalityComparer(i, ii)));
		}

		public static bool ContainsDuplicates<T>(this IEnumerable<T> items, Func<T, T, bool> equalityComparer) {
			return items.Any(i => items.Any(ii => !object.ReferenceEquals(i, ii) && equalityComparer(i, ii)));
		}

		public static IEnumerable<T> Distinct<T>(this IEnumerable<T> items, Func<T, T, bool> comparer) {
			return items.Distinct(new GenericEqualityComparer<T>(comparer));
		}

		public static bool DoesNotContain<T>(this IEnumerable<T> items, Func<T, bool> predicate) {
			return !items.Any(predicate);
		}

		public static bool IsEmpty<T>(this IEnumerable<T> items) {
			return items == null || !items.Any();
		}

		public static bool IsNotEmpty<T>(this IEnumerable<T> items) {
			return !IsEmpty(items);
		}

		public static string DelimitWith(this IEnumerable<string> source, string delimiter) {
			var sb = new StringBuilder(100);

			foreach (var item in source) {
				sb.Append(item).Append(delimiter);
			}

			// get rid of the last one
			sb.Length -= delimiter.Length;

			return sb.ToString();
		}

		public static IEnumerable<TResult> Zip<TFirst, TSecond, TResult>(
			this IEnumerable<TFirst> first,
			IEnumerable<TSecond> second,
			Func<TFirst, TSecond, TResult> combinator) //
		{
			var firstByindex = first.Select((item, index) => new {
				Item = item,
				Index = index
			});

			var secondByIndex = second.Select((item, index) => new {
				Item = item,
				Index = index
			});

			return firstByindex.Join(secondByIndex,
				fItem => fItem.Index,
				sItem => sItem.Index,
				(fItem, sItem) => combinator(fItem.Item, sItem.Item));
		}
	}

	public class GenericEqualityComparer<TItem> : IEqualityComparer<TItem> {

		private readonly Func<TItem, TItem, bool> _compare;
		private readonly Func<TItem, int> _getHashCode;

		public GenericEqualityComparer(Func<TItem, TItem, bool> compareEqual, Func<TItem, int> createHashCode = null) {
			if (compareEqual == null)
				throw new ArgumentNullException("compareEqual");

			_getHashCode = createHashCode;
			_compare = compareEqual;
		}

		public bool Equals(TItem x, TItem y) {
			return _compare(x, y);
		}

		public int GetHashCode(TItem obj) {
			if (_getHashCode == null)
				return obj.GetHashCode();

			return _getHashCode(obj);
		}
	}
}
