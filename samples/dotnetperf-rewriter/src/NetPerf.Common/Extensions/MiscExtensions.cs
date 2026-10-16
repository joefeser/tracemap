
namespace System.Collections.Generic {
	public static class MiscExtensions {
		/// <summary>
		/// Adds the <paramref name="itemsToAdd"/> to the end of the collection.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="items">The collection to add the <paramref name="itemsToAdd"/> to.</param>
		/// <param name="itemsToAdd">The elements to add</param>
		public static void AddRange<T>(this IList<T> items, IEnumerable<T> itemsToAdd) {
			if (itemsToAdd == null)
				throw new ArgumentNullException("itemsToAdd");

			foreach (var item in itemsToAdd) {
				items.Add(item);
			}
		}
	}
}
