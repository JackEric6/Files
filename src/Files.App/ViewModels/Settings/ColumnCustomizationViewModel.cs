// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.Collections.ObjectModel;

namespace Files.App.ViewModels.Settings
{
	/// <summary>
	/// Represents a group or a column in <see cref="Views.Settings.ColumnCustomizationPage"/>.
	/// </summary>
	public sealed partial class ColumnPickerItem : ObservableObject
	{
		public ColumnPickerItem(string id, string displayName, ColumnPickerItem? group = null)
		{
			Id = id;
			DisplayName = displayName;
			Group = group;
		}

		public string Id { get; }

		public string DisplayName { get; }

		/// <summary>
		/// Gets the group of the column, or null when the item is a group.
		/// </summary>
		public ColumnPickerItem? Group { get; }

		public bool IsGroup
			=> Group is null;

		public bool IsColumn
			=> !IsGroup;

		/// <summary>
		/// Gets whether the user can change the item, which isn't the case for the column that is always shown.
		/// </summary>
		public bool IsEnabled
			=> IsGroup ? Columns.Any(column => column.IsEnabled) : !DetailsLayoutColumnOrder.IsFixed(Id);

		public List<ColumnPickerItem> Columns { get; } = [];

		private bool? _IsChecked = false;
		/// <summary>
		/// Gets whether the column is shown, or for a group whether all, some or none of its columns are shown.
		/// </summary>
		public bool? IsChecked
		{
			get => _IsChecked;
			set => SetProperty(ref _IsChecked, value);
		}

		private bool _IsExpanded;
		public bool IsExpanded
		{
			get => _IsExpanded;
			set
			{
				if (SetProperty(ref _IsExpanded, value))
					OnPropertyChanged(nameof(ExpandGlyphAngle));
			}
		}

		public double ExpandGlyphAngle
			=> IsExpanded ? 90 : 0;

		public override string ToString()
			=> DisplayName;
	}

	/// <summary>
	/// Edits which columns of <see cref="DetailsLayoutPage"/> are shown.
	/// </summary>
	public sealed partial class ColumnCustomizationViewModel : ObservableObject
	{
		private readonly IReadOnlyList<string> _defaultColumns;
		private readonly List<string> _shownColumns = [];
		private readonly IReadOnlyList<ColumnPickerItem> _groups;

		/// <summary>
		/// Gets the groups and the columns of the expanded groups, in display order.
		/// </summary>
		public ObservableCollection<ColumnPickerItem> Items { get; } = [];

		private string _SearchQuery = string.Empty;
		public string SearchQuery
		{
			get => _SearchQuery;
			set
			{
				if (SetProperty(ref _SearchQuery, value ?? string.Empty))
					UpdateItems();
			}
		}

		/// <summary>
		/// Occurs when the user saves the shown columns, in display order.
		/// </summary>
		public event EventHandler<IReadOnlyList<string>>? Saved;

		public string Title { get; }

		public string? Description { get; }

		public bool HasDescription => !string.IsNullOrEmpty(Description);

		public ColumnCustomizationViewModel(IEnumerable<string> shownColumns, IEnumerable<string> availableColumns, IEnumerable<string> defaultColumns, Func<string, string> getDisplayName, string title, string? description = null)
		{
			_defaultColumns = defaultColumns.ToArray();
			Title = title;
			Description = description;

			var columnIds = new[] { DetailsLayoutColumnIds.Name }.Concat(availableColumns.Where(id => !id.StartsWith("System.VersionControl.", StringComparison.Ordinal))).Concat(shownColumns).Distinct().ToArray();
			_groups = columnIds
				.GroupBy(GetGroup)
				.OrderBy(group => group.Key)
				.Select(group =>
				{
					var groupItem = new ColumnPickerItem(group.Key.ToString(), GetGroupDisplayName(group.Key));
					groupItem.Columns.AddRange(group
						.Select(id => new ColumnPickerItem(id, getDisplayName(id), groupItem))
						.OrderBy(column => column.IsEnabled)
						.ThenBy(column => column.DisplayName, StringComparer.CurrentCultureIgnoreCase));

					return groupItem;
				}).ToArray();

			SetShownColumns(shownColumns);
			UpdateItems();
		}

		/// <summary>
		/// Shows or hides the specified column, or all columns of the specified group.
		/// </summary>
		public void Toggle(ColumnPickerItem item)
		{
			if (!item.IsEnabled)
				return;

			if (item.IsGroup)
			{
				var isShown = item.IsChecked is not true;
				foreach (var column in item.Columns.Where(column => column.IsEnabled))
					SetColumnShown(column, isShown);
			}
			else
			{
				SetColumnShown(item, item.IsChecked is not true);
			}

			UpdateCheckStates();
		}

		public void RestoreDefaults()
		{
			SetShownColumns(_defaultColumns);
		}

		public void Save()
		{
			Saved?.Invoke(this, _shownColumns.ToArray());
		}

		private void SetColumnShown(ColumnPickerItem column, bool isShown)
		{
			if (!isShown)
				_shownColumns.Remove(column.Id);
			else if (!_shownColumns.Contains(column.Id))
				_shownColumns.Add(column.Id);
		}

		private void SetShownColumns(IEnumerable<string> columns)
		{
			_shownColumns.Clear();
			_shownColumns.AddRange(columns.Where(id => !DetailsLayoutColumnOrder.IsFixed(id)).Distinct());
			UpdateCheckStates();
		}

		private void UpdateCheckStates()
		{
			foreach (var group in _groups)
			{
				foreach (var column in group.Columns)
					column.IsChecked = !column.IsEnabled || _shownColumns.Contains(column.Id);

				var checkedCount = group.Columns.Count(column => column.IsChecked is true);
				group.IsChecked = checkedCount == group.Columns.Count ? true : checkedCount == 0 ? false : null;
			}
		}

		private void UpdateItems()
		{
			var query = SearchQuery.Trim();
			var items = new List<ColumnPickerItem>();
			foreach (var group in _groups)
			{
				var columns = group.Columns.Where(column => query.Length == 0 || column.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
				if (columns.Length == 0)
					continue;

				if (query.Length > 0)
					group.IsExpanded = true;

				items.Add(group);
				if (group.IsExpanded)
					items.AddRange(columns);
			}

			var itemSet = items.ToHashSet();
			for (int i = Items.Count - 1; i >= 0; i--)
			{
				if (!itemSet.Contains(Items[i]))
					Items.RemoveAt(i);
			}

			for (int i = 0; i < items.Count; i++)
			{
				if (i >= Items.Count || Items[i] != items[i])
					Items.Insert(i, items[i]);
			}
		}

		/// <summary>
		/// Expands or collapses the specified group.
		/// </summary>
		public void ToggleExpanded(ColumnPickerItem group)
		{
			group.IsExpanded = !group.IsExpanded;
			UpdateItems();
		}

		private enum ColumnGroup
		{
			General,
			Dates,
			Location,
			Description,
			Origin,
			Sharing,
			Documents,
			Pictures,
			Camera,
			Music,
			Videos,
			Media,
			RecordedTV,
			Email,
			Calendar,
			Tasks,
			Contacts,
			PhoneAndEmail,
			Addresses,
			Business,
			Devices,
			Git,
			Other,
		}

		private static readonly Dictionary<string, ColumnGroup> SystemColumnGroups = new()
		{
			["DateAccessed"] = ColumnGroup.Dates,
			["DateAcquired"] = ColumnGroup.Dates,
			["DateArchived"] = ColumnGroup.Dates,
			["DateCompleted"] = ColumnGroup.Dates,
			["DueDate"] = ColumnGroup.Dates,
			["StartDate"] = ColumnGroup.Dates,
			["EndDate"] = ColumnGroup.Dates,
			["ItemDate"] = ColumnGroup.Dates,
			["ItemFolderNameDisplay"] = ColumnGroup.Location,
			["ItemFolderPathDisplay"] = ColumnGroup.Location,
			["ItemFolderPathDisplayNarrow"] = ColumnGroup.Location,
			["ItemFolderPathDisplayNarrowNamespaceRelative"] = ColumnGroup.Location,
			["ItemSearchLocation"] = ColumnGroup.Location,
			["ComputerName"] = ColumnGroup.Location,
			["Title"] = ColumnGroup.Description,
			["Subject"] = ColumnGroup.Description,
			["Rating"] = ColumnGroup.Description,
			["Keywords"] = ColumnGroup.Description,
			["Category"] = ColumnGroup.Description,
			["Comment"] = ColumnGroup.Description,
			["Language"] = ColumnGroup.Description,
			["ParentalRating"] = ColumnGroup.Description,
			["ParentalRatingReason"] = ColumnGroup.Description,
			["TitleSortOverride"] = ColumnGroup.Description,
			["ContentStatus"] = ColumnGroup.Description,
			["Author"] = ColumnGroup.Origin,
			["ItemAuthors"] = ColumnGroup.Origin,
			["Company"] = ColumnGroup.Origin,
			["Copyright"] = ColumnGroup.Origin,
			["Trademarks"] = ColumnGroup.Origin,
			["Project"] = ColumnGroup.Origin,
			["ApplicationName"] = ColumnGroup.Origin,
			["FileDescription"] = ColumnGroup.Origin,
			["FileVersion"] = ColumnGroup.Origin,
			["SourceItem"] = ColumnGroup.Origin,
			["OfflineAvailability"] = ColumnGroup.Sharing,
			["OfflineStatus"] = ColumnGroup.Sharing,
			["IsShared"] = ColumnGroup.Sharing,
			["SharedWith"] = ColumnGroup.Sharing,
			["SharingStatus"] = ColumnGroup.Sharing,
			["HomeGroupSharingStatus"] = ColumnGroup.Sharing,
			["StorageProviderSharingStatus"] = ColumnGroup.Sharing,
			["StorageProviderState"] = ColumnGroup.Sharing,
			["IsAttachment"] = ColumnGroup.Email,
			["IsRead"] = ColumnGroup.Email,
			["ItemParticipants"] = ColumnGroup.Email,
			["FlagColor"] = ColumnGroup.Tasks,
			["FlagStatus"] = ColumnGroup.Tasks,
			["IsFlagged"] = ColumnGroup.Tasks,
			["IsFlaggedComplete"] = ColumnGroup.Tasks,
			["IsIncomplete"] = ColumnGroup.Tasks,
			["Priority"] = ColumnGroup.Tasks,
			["Importance"] = ColumnGroup.Tasks,
			["Sensitivity"] = ColumnGroup.Tasks,
			["Capacity"] = ColumnGroup.Devices,
			["FreeSpace"] = ColumnGroup.Devices,
			["PercentFull"] = ColumnGroup.Devices,
			["IsDeleted"] = ColumnGroup.Other,
			["FileCount"] = ColumnGroup.Other,
			["MileageInformation"] = ColumnGroup.Other,
		};

		private static readonly HashSet<string> PictureColumns = ["DateTaken", "Event", "PeopleNames", "Orientation"];

		private static readonly HashSet<string> BusinessContactColumns = ["AssistantName", "AssistantTelephone", "CompanyMainTelephone", "Department", "JobTitle", "OfficeLocation", "Profession"];

		private static ColumnGroup GetGroup(string id)
		{
			if (DetailsLayoutColumnDefinitions.Get(id) is { } definition)
				return definition.IsGitColumn ? ColumnGroup.Git : ColumnGroup.General;

			var parts = id.Split('.');
			if (parts is not ["System", ..])
				return ColumnGroup.Other;

			if (parts.Length == 2)
				return SystemColumnGroups.GetValueOrDefault(parts[1], ColumnGroup.General);

			return parts[1] switch
			{
				"Document" or "Visio" or "Presentation" => ColumnGroup.Documents,
				"Image" => ColumnGroup.Pictures,
				"GPS" => ColumnGroup.Location,
				"Photo" => PictureColumns.Contains(parts[2]) ? ColumnGroup.Pictures : ColumnGroup.Camera,
				"Music" or "Audio" => ColumnGroup.Music,
				"Video" => ColumnGroup.Videos,
				"Media" or "DRM" => ColumnGroup.Media,
				"RecordedTV" => ColumnGroup.RecordedTV,
				"Message" => ColumnGroup.Email,
				"Communication" => parts[2] is "TaskStatus" ? ColumnGroup.Tasks : ColumnGroup.Email,
				"Calendar" or "Journal" => ColumnGroup.Calendar,
				"Task" => ColumnGroup.Tasks,
				"Contact" => GetContactGroup(parts[2]),
				"Software" => ColumnGroup.Origin,
				"Sharing" or "Sync" or "Home" => ColumnGroup.Sharing,
				"Devices" or "Volume" or "Computer" => ColumnGroup.Devices,
				"VersionControl" => ColumnGroup.Git,
				_ => ColumnGroup.Other,
			};
		}

		private static ColumnGroup GetContactGroup(string name)
		{
			if (name.StartsWith("Business", StringComparison.Ordinal) || BusinessContactColumns.Contains(name))
				return ColumnGroup.Business;

			if (name.Contains("Email", StringComparison.Ordinal) || name.Contains("Telephone", StringComparison.Ordinal) ||
				name.EndsWith("Number", StringComparison.Ordinal) || name is "IMAddress" or "WebPage")
				return ColumnGroup.PhoneAndEmail;

			return name.Contains("Address", StringComparison.Ordinal) ? ColumnGroup.Addresses : ColumnGroup.Contacts;
		}

		private static string GetGroupDisplayName(ColumnGroup group)
		{
			return (group switch
			{
				ColumnGroup.General => Strings.General,
				ColumnGroup.Dates => Strings.Dates,
				ColumnGroup.Location => Strings.LocationColumns,
				ColumnGroup.Description => Strings.Description,
				ColumnGroup.Origin => Strings.Origin,
				ColumnGroup.Sharing => Strings.Sharing,
				ColumnGroup.Documents => Strings.Documents,
				ColumnGroup.Pictures => Strings.Pictures,
				ColumnGroup.Camera => Strings.Camera,
				ColumnGroup.Music => Strings.Music,
				ColumnGroup.Videos => Strings.Videos,
				ColumnGroup.Media => Strings.Media,
				ColumnGroup.RecordedTV => Strings.RecordedTV,
				ColumnGroup.Email => Strings.Email,
				ColumnGroup.Calendar => Strings.Calendar,
				ColumnGroup.Tasks => Strings.Tasks,
				ColumnGroup.Contacts => Strings.Contacts,
				ColumnGroup.PhoneAndEmail => Strings.PhoneAndEmail,
				ColumnGroup.Addresses => Strings.Addresses,
				ColumnGroup.Business => Strings.Business,
				ColumnGroup.Devices => Strings.DevicesAndDrives,
				ColumnGroup.Git => Strings.Git,
				_ => Strings.OtherColumns,
			}).GetLocalizedResource();
		}
	}
}
