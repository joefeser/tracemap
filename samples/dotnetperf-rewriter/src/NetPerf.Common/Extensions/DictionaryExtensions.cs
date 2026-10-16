using System.Linq;

namespace System.Collections.Generic {
	public static class DictionaryExtensions {

		public static void AddUniqueChildRecords<TKey, T>(this IDictionary<TKey, T> source, IEnumerable<T> itemsToAdd,
			Predicate<T> shouldBeAdded, Func<T, T, bool> equalityComparer, Func<T, TKey> keySelector) where T : class //
		{
			if (itemsToAdd.IsEmpty())
				return;

			var filtered = itemsToAdd.Where(m => m != null).ToArray();

			var anyShouldnyBeAdded = filtered.Any(f => !shouldBeAdded(f));
			if (anyShouldnyBeAdded)
				throw new InvalidOperationException();

			var anyItemsConflict = source.Values.ContainsAny(filtered, equalityComparer)
				|| filtered.ContainsDuplicates(equalityComparer);

			if (anyItemsConflict)
				throw new InvalidOperationException();

			foreach (var item in filtered) {
				source.AtomicAdd(keySelector(item),
					() => item,
					sf => sf);
			}
		}

		public static TReturn AtomicAdd<TKey, TValue, TReturn>(this IDictionary<TKey, TValue> storage, TKey key, Func<TValue> getNewValue, Func<TValue, TReturn> getReturnVal) {
			TValue item;
			if (storage.TryGetValue(key, out item))
				return getReturnVal(item);

			lock (((ICollection)storage).SyncRoot) {
				if (storage.TryGetValue(key, out item))
					return getReturnVal(item);

				item = getNewValue();
				storage.Add(key, item);
				return getReturnVal(item);
			}
		}
	}
}
