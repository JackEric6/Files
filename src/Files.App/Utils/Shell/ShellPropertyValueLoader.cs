// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;

namespace Files.App.Utils.Shell
{
	/// <summary>
	/// Loads the Windows property values of items, such as for the property columns of <see cref="DetailsLayoutPage"/>.
	/// </summary>
	public static class ShellPropertyValueLoader
	{
		private static readonly ConcurrentDictionary<ListedItem, Task> _loads = new();

		/// <summary>
		/// Loads the specified properties of the item that it doesn't have yet.
		/// </summary>
		public static async Task LoadAsync(ListedItem item, IReadOnlyList<string> propertyIds)
		{
			if (propertyIds.Count == 0 || item.ItemPath is not { Length: > 0 } path)
				return;

			// A second pass reads what a load started by another caller didn't include
			for (var pass = 0; pass < 2; pass++)
			{
				var missing = propertyIds.Where(id => item.ShellPropertyValues?.ContainsKey(id) is not true).ToArray();
				if (missing.Length == 0)
					return;

				var load = _loads.GetOrAdd(item, _ => ReadAsync(item, path, missing));
				try
				{
					await load;
				}
				finally
				{
					_loads.TryRemove(new(item, load));
				}
			}
		}

		private static async Task ReadAsync(ListedItem item, string path, string[] propertyIds)
		{
			var values = await ShellPropertyReader.ReadAsync(path, propertyIds);
			if (item.ShellPropertyValues is { } current)
			{
				foreach (var (id, value) in current)
					values.TryAdd(id, value);
			}

			item.ShellPropertyValues = values;
		}
	}
}
