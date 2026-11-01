// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using Files.App.Utils.Shell;
using Files.Shared.Helpers;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.IO;

namespace Files.App.Helpers
{
	/// <summary>
	/// Represents an option of the sort or group menus.
	/// </summary>
	/// <param name="BuiltInCommand">The built-in sort or group command, when the option is one.</param>
	/// <param name="PropertyId">The Shell property or Git column the option uses, when it isn't built in.</param>
	/// <param name="Items">The options of a submenu, such as the date units of a date.</param>
	internal sealed record LayoutSortGroupOption(
		string Label,
		IRichCommand? BuiltInCommand = null,
		string? PropertyId = null,
		bool IsChecked = false,
		IReadOnlyList<LayoutSortGroupOption>? Items = null);

	/// <summary>
	/// Provides the sort and group options of a location, as File Explorer offers them for the type of the folder.
	/// </summary>
	internal static class LayoutSortGroupOptions
	{
		private const string FolderTypesKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FolderTypes";

		private static readonly Guid GenericFolderType = new("5c4f28b5-f869-4e84-8e60-f11db97c5cc7");
		private static readonly Guid PicturesFolderType = new("b3690e58-e961-423b-b687-386ebfd83239");
		private static readonly Guid VideosFolderType = new("5fa96407-7e77-483c-ac93-691d05850de8");
		private static readonly Guid MusicFolderType = new("94d6ddcc-4a68-4175-a374-bd584a510b78");

		private static readonly string[] FallbackExplorerColumns = [DetailsLayoutColumnIds.Name, DetailsLayoutColumnIds.DateModified, DetailsLayoutColumnIds.ItemType, DetailsLayoutColumnIds.Size, DetailsLayoutColumnIds.DateCreated];

		/// <summary>
		/// Gets the Git columns that items can be sorted by.
		/// </summary>
		public static IReadOnlyList<string> GitSortColumnIds { get; } = DetailsLayoutColumnDefinitions.BuiltIn.Where(definition => definition.IsGitColumn).Select(definition => definition.Id).ToArray();

		/// <summary>
		/// Gets the Git columns that items can be grouped by.
		/// </summary>
		public static IReadOnlyList<string> GitGroupColumnIds { get; } = new[] { DetailsLayoutColumnIds.GitStatus, DetailsLayoutColumnIds.GitCommitAuthor, DetailsLayoutColumnIds.GitLastCommitDate };

		private static readonly int ContentThreshold = new LayoutRule().Threshold;

		private static readonly ConcurrentDictionary<Guid, IReadOnlyList<string>> _folderTypeColumns = new();

		private static ICommandManager Commands { get; } = Ioc.Default.GetRequiredService<ICommandManager>();

		/// <summary>
		/// Gets the options of the sort menus for the specified location.
		/// </summary>
		public static IReadOnlyList<LayoutSortGroupOption> GetSortOptions(LayoutPreferencesManager folderSettings, ShellViewModel? shellViewModel)
		{
			var options = new List<LayoutSortGroupOption>();
			var commands = new HashSet<IRichCommand>();
			var sortPropertyId = folderSettings.DirectorySortOption is SortOption.Property ? folderSettings.DirectorySortPropertyId : null;

			foreach (var (id, _) in GetExplorerColumns(shellViewModel))
			{
				if (GetBuiltInSortCommand(id) is { } command)
					AddBuiltInCommand(command);
				else if (ColumnValueHelper.IsShellPropertyColumn(id) && options.All(option => option.PropertyId != id))
					options.Add(CreatePropertyOption(id, id == sortPropertyId));
			}

			if (shellViewModel?.IsValidGitDirectory is true)
			{
				foreach (var id in GitSortColumnIds.Where(id => options.All(option => option.PropertyId != id)))
					options.Add(CreatePropertyOption(id, id == sortPropertyId));
			}

			foreach (var command in new[] { Commands.SortByTag, Commands.SortBySyncStatus, Commands.SortByPath, Commands.SortByOriginalFolder, Commands.SortByDateDeleted })
				AddBuiltInCommand(command);

			if (sortPropertyId is not null && options.All(option => option.PropertyId != sortPropertyId))
				options.Add(CreatePropertyOption(sortPropertyId, true));

			return options;

			void AddBuiltInCommand(IRichCommand command)
			{
				if (command.IsExecutable && commands.Add(command))
					options.Add(new(command.Label, command, IsChecked: command.IsOn));
			}
		}

		/// <summary>
		/// Gets the options of the group menus for the specified location.
		/// </summary>
		public static IReadOnlyList<LayoutSortGroupOption> GetGroupOptions(LayoutPreferencesManager folderSettings, ShellViewModel? shellViewModel)
		{
			var options = new List<LayoutSortGroupOption>();
			var commands = new HashSet<IRichCommand>();
			var groupPropertyId = folderSettings.DirectoryGroupOption is GroupOption.Property ? folderSettings.DirectoryGroupPropertyId : null;

			AddBuiltInCommand(Commands.GroupByNone);
			foreach (var (id, canGroup) in GetExplorerColumns(shellViewModel))
			{
				if (!canGroup)
					continue;

				if (GetBuiltInGroupCommand(id) is { } command)
					AddBuiltInCommand(command);
				else if (GetBuiltInGroupByDateCommands(id) is { } dateCommands)
					AddDateCommands(dateCommands);
				else if (ColumnValueHelper.IsShellPropertyColumn(id) && options.All(option => option.PropertyId != id))
					options.Add(CreatePropertyOption(id, id == groupPropertyId));
			}

			if (shellViewModel?.IsValidGitDirectory is true)
			{
				foreach (var id in GitGroupColumnIds.Where(id => options.All(option => option.PropertyId != id)))
					options.Add(CreatePropertyOption(id, id == groupPropertyId));
			}

			foreach (var command in new[] { Commands.GroupByTag, Commands.GroupBySyncStatus, Commands.GroupByFolderPath, Commands.GroupByOriginalFolder })
				AddBuiltInCommand(command);

			if (GetBuiltInGroupByDateCommands(DetailsLayoutColumnIds.DateDeleted) is { } dateDeletedCommands)
				AddDateCommands(dateDeletedCommands);

			if (groupPropertyId is not null && options.All(option => option.PropertyId != groupPropertyId))
				options.Add(CreatePropertyOption(groupPropertyId, true));

			return options;

			void AddBuiltInCommand(IRichCommand command)
			{
				if (command.IsExecutable && commands.Add(command))
					options.Add(new(command.Label, command, IsChecked: command.IsOn));
			}

			void AddDateCommands((IRichCommand Date, IRichCommand[] Units) dateCommands)
			{
				if (dateCommands.Date.IsExecutable && commands.Add(dateCommands.Date))
					options.Add(new(dateCommands.Date.Label, Items: dateCommands.Units.Select(unit => new LayoutSortGroupOption(unit.Label, unit, IsChecked: unit.IsOn)).ToArray()));
			}
		}

		/// <summary>
		/// Sorts the items of the folder by the specified property.
		/// </summary>
		public static void SortBy(LayoutPreferencesManager folderSettings, string id)
		{
			folderSettings.SetSortProperty(id);
			LayoutHelpers.UpdateOpenTabsPreferences();
		}

		/// <summary>
		/// Groups the items of the folder by the specified property.
		/// </summary>
		public static void GroupBy(LayoutPreferencesManager folderSettings, string id)
		{
			folderSettings.SetGroupProperty(id);
			LayoutHelpers.UpdateOpenTabsPreferences();
		}

		/// <summary>
		/// Gets the columns File Explorer offers for the location, such as the music columns of a music folder.
		/// </summary>
		public static IEnumerable<string> GetExplorerColumnIds(ShellViewModel? shellViewModel)
			=> GetExplorerColumns(shellViewModel).Select(column => column.Id);

		/// <summary>
		/// Gets the columns File Explorer offers for any type of folder.
		/// </summary>
		public static IEnumerable<string> GetExplorerFolderTypeColumnIds()
		{
			return new[] { GenericFolderType, PicturesFolderType, VideosFolderType, MusicFolderType }
				.SelectMany(folderType => _folderTypeColumns.GetOrAdd(folderType, ReadExplorerFolderTypeColumns))
				.Distinct();
		}

		/// <summary>
		/// Gets whether the specified column is sorted by a built-in option, such as System.Keywords by tags.
		/// </summary>
		public static bool HasBuiltInSortOption(string id)
			=> GetBuiltInSortCommand(id) is not null;

		private static IEnumerable<(string Id, bool CanGroup)> GetExplorerColumns(ShellViewModel? shellViewModel)
		{
			if (shellViewModel?.WorkingDirectory is { Length: > 0 } path &&
				!ShellFolderColumns.IsFileSystemPath(path) &&
				ShellFolderColumns.GetAsync(path) is { IsCompletedSuccessfully: true, Result.Count: > 0 } task)
				return task.Result.Where(column => column.IsPrimary).Select(column => (column.Id, column.CanGroup));

			var folderType = shellViewModel is null ? GenericFolderType : GetFolderType(FolderContentHelpers.GetContent(shellViewModel.FilesAndFolders));
			return _folderTypeColumns.GetOrAdd(folderType, ReadExplorerFolderTypeColumns).Select(id => (id, true));
		}

		private static Guid GetFolderType(LayoutFolderContent content)
		{
			if (content.Total == 0)
				return GenericFolderType;

			int images = 0, videos = 0, audio = 0;
			foreach (var (extension, count) in content.ExtensionCounts)
			{
				if (FileExtensionHelpers.IsImageFile(extension))
					images += count;
				else if (FileExtensionHelpers.IsVideoFile(extension))
					videos += count;
				else if (FileExtensionHelpers.IsAudioFile(extension))
					audio += count;
			}

			var threshold = content.Total * ContentThreshold / 100.0;
			if (images + videos >= threshold)
				return images >= videos ? PicturesFolderType : VideosFolderType;

			return audio >= threshold ? MusicFolderType : GenericFolderType;
		}

		private static IReadOnlyList<string> ReadExplorerFolderTypeColumns(Guid folderType)
		{
			try
			{
				using var key = Registry.LocalMachine.OpenSubKey($@"{FolderTypesKey}\{folderType:B}\TopViews\{Guid.Empty:B}");
				if (key?.GetValue("ColumnList") is string columnList && columnList.Split(':', 2) is [_, var entries])
				{
					// Entries look like "0System.Size" or "0(34)System.ItemNameDisplay"
					var ids = entries.Split(';', StringSplitOptions.RemoveEmptyEntries)
						.Select(entry => entry.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
						.Select(entry => entry.StartsWith('(') && entry.IndexOf(')') is var end and > 0 ? entry[(end + 1)..] : entry)
						.Where(id => id.Length > 0)
						.ToArray();

					if (ids.Length > 0)
						return ids;
				}
			}
			catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
			{
			}

			return FallbackExplorerColumns;
		}

		private static LayoutSortGroupOption CreatePropertyOption(string id, bool isChecked)
		{
			var catalog = ShellPropertyCatalog.GetAsync() is { IsCompletedSuccessfully: true } task ? task.Result : null;
			return new(DetailsLayoutColumnDefaults.GetDisplayName(id, catalog), PropertyId: id, IsChecked: isChecked);
		}

		private static IRichCommand? GetBuiltInSortCommand(string id)
		{
			return id switch
			{
				DetailsLayoutColumnIds.Name => Commands.SortByName,
				DetailsLayoutColumnIds.DateModified => Commands.SortByDateModified,
				DetailsLayoutColumnIds.DateCreated => Commands.SortByDateCreated,
				DetailsLayoutColumnIds.Size => Commands.SortBySize,
				DetailsLayoutColumnIds.ItemType => Commands.SortByType,
				DetailsLayoutColumnIds.Status => Commands.SortBySyncStatus,
				DetailsLayoutColumnIds.Path or "System.ItemFolderPathDisplay" => Commands.SortByPath,
				DetailsLayoutColumnIds.OriginalPath => Commands.SortByOriginalFolder,
				DetailsLayoutColumnIds.DateDeleted => Commands.SortByDateDeleted,
				"System.Keywords" => Commands.SortByTag,
				_ => null,
			};
		}

		private static IRichCommand? GetBuiltInGroupCommand(string id)
		{
			return id switch
			{
				DetailsLayoutColumnIds.Name => Commands.GroupByName,
				DetailsLayoutColumnIds.Size => Commands.GroupBySize,
				DetailsLayoutColumnIds.ItemType => Commands.GroupByType,
				DetailsLayoutColumnIds.Status => Commands.GroupBySyncStatus,
				DetailsLayoutColumnIds.Path or "System.ItemFolderPathDisplay" => Commands.GroupByFolderPath,
				DetailsLayoutColumnIds.OriginalPath => Commands.GroupByOriginalFolder,
				"System.Keywords" => Commands.GroupByTag,
				_ => null,
			};
		}

		private static (IRichCommand Date, IRichCommand[] Units)? GetBuiltInGroupByDateCommands(string id)
		{
			return id switch
			{
				DetailsLayoutColumnIds.DateModified => (Commands.GroupByDateModified, [Commands.GroupByDateModifiedYear, Commands.GroupByDateModifiedMonth, Commands.GroupByDateModifiedDay]),
				DetailsLayoutColumnIds.DateCreated => (Commands.GroupByDateCreated, [Commands.GroupByDateCreatedYear, Commands.GroupByDateCreatedMonth, Commands.GroupByDateCreatedDay]),
				DetailsLayoutColumnIds.DateDeleted => (Commands.GroupByDateDeleted, [Commands.GroupByDateDeletedYear, Commands.GroupByDateDeletedMonth, Commands.GroupByDateDeletedDay]),
				_ => null,
			};
		}
	}
}
