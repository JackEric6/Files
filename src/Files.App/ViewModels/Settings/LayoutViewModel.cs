// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Dialogs;
using System.Windows.Input;

namespace Files.App.ViewModels.Settings
{
	public sealed partial class LayoutViewModel : ObservableObject
	{
		private IUserSettingsService UserSettingsService { get; } = Ioc.Default.GetRequiredService<IUserSettingsService>();
		private ILayoutTemplatesService LayoutTemplatesService { get; } = Ioc.Default.GetRequiredService<ILayoutTemplatesService>();

		public LayoutViewModel()
		{
			Templates = [.. LayoutTemplatesService.Templates.Where(template => template.Id != LayoutTemplateIds.Default).Select(template => new LayoutTemplateViewModel(LayoutTemplatesService, template.Id, this))];

			EditDefaultTemplateCommand = new AsyncRelayCommand(() => EditTemplateAsync(LayoutTemplateIds.Default));
			AddTemplateCommand = new AsyncRelayCommand(AddTemplateAsync);
		}

		internal void StartListening()
		{
			UserSettingsService.OnSettingChangedEvent -= OnSharedSettingChanged;
			UserSettingsService.OnSettingChangedEvent += OnSharedSettingChanged;
			LayoutTemplatesService.TemplateChanged -= LayoutTemplatesService_TemplateChanged;
			LayoutTemplatesService.TemplateChanged += LayoutTemplatesService_TemplateChanged;
			LayoutTemplatesService.RulesChanged -= LayoutTemplatesService_RulesChanged;
			LayoutTemplatesService.RulesChanged += LayoutTemplatesService_RulesChanged;
			LayoutTemplatesService.PropertyChanged -= LayoutTemplatesService_PropertyChanged;
			LayoutTemplatesService.PropertyChanged += LayoutTemplatesService_PropertyChanged;
			RefreshSettings();
		}

		internal void StopListening()
		{
			UserSettingsService.OnSettingChangedEvent -= OnSharedSettingChanged;
			LayoutTemplatesService.TemplateChanged -= LayoutTemplatesService_TemplateChanged;
			LayoutTemplatesService.RulesChanged -= LayoutTemplatesService_RulesChanged;
			LayoutTemplatesService.PropertyChanged -= LayoutTemplatesService_PropertyChanged;
		}

		private void OnSharedSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.NewValue is JsonElement)
				RefreshSettings(e.SettingName);
		}

		private void RefreshSettings(string? name = null)
		{
			if (name is null or nameof(ILayoutSettingsService.AutoSizeColumnsInDetailsLayout))
				OnPropertyChanged(nameof(AutoSizeColumnsInDetailsLayout));
		}

		// Properties

		public ObservableCollection<LayoutTemplateViewModel> Templates { get; }

		/// <summary>
		/// Occurs when a template should be opened for editing.
		/// </summary>
		public event EventHandler<LayoutTemplateEditorViewModel>? EditTemplateRequested;

		public ICommand EditDefaultTemplateCommand { get; }

		public ICommand AddTemplateCommand { get; }

		// Details view

		public bool AutoSizeColumnsInDetailsLayout
		{
			get => UserSettingsService.LayoutSettingsService.AutoSizeColumnsInDetailsLayout;
			set
			{
				if (value != UserSettingsService.LayoutSettingsService.AutoSizeColumnsInDetailsLayout)
				{
					UserSettingsService.LayoutSettingsService.AutoSizeColumnsInDetailsLayout = value;

					OnPropertyChanged();
				}
			}
		}

		// Methods

		public Task EditTemplateAsync(LayoutTemplateViewModel template)
			=> EditTemplateAsync(template.Id);

		public async Task DeleteTemplateAsync(LayoutTemplateViewModel template)
		{
			if (!template.CanDelete || !await LayoutTemplateDialogs.ConfirmDeleteAsync(template.DisplayName))
				return;

			LayoutTemplatesService.DeleteTemplate(template.Id);
		}

		private async Task AddTemplateAsync()
		{
			if (await LayoutTemplateDialogs.AskNameAsync(Strings.AddTemplate.GetLocalizedResource(), Strings.Add.GetLocalizedResource(), Strings.NewTemplate.GetLocalizedResource()) is not { } name)
				return;

			var source = LayoutTemplatesService.DefaultTemplate.Clone();

			var template = LayoutTemplatesService.AddTemplate(source, name);
			await EditTemplateAsync(template.Id);
		}

		public async Task EditTemplateAsync(string id)
		{
			if (LayoutTemplatesService.GetTemplate(id) is not { } template)
				return;

			var editor = new LayoutTemplateEditorViewModel(LayoutTemplatesService, template);
			await editor.LoadAsync();
			EditTemplateRequested?.Invoke(this, editor);
		}

		private void LayoutTemplatesService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not nameof(ILayoutTemplatesService.Templates))
				return;

			// Keep the rows of templates that still exist, so the list doesn't flicker
			var ids = LayoutTemplatesService.Templates.Where(template => template.Id != LayoutTemplateIds.Default).Select(template => template.Id).ToArray();
			foreach (var removed in Templates.Where(template => !ids.Contains(template.Id)).ToArray())
				Templates.Remove(removed);

			for (var i = 0; i < ids.Length; i++)
			{
				if (i < Templates.Count && Templates[i].Id == ids[i])
					continue;

				if (Templates.FirstOrDefault(template => template.Id == ids[i]) is { } existing)
					Templates.Move(Templates.IndexOf(existing), i);
				else
					Templates.Insert(i, new(LayoutTemplatesService, ids[i], this));
			}

			foreach (var template in Templates)
				template.Refresh();
		}

		private void LayoutTemplatesService_TemplateChanged(object? sender, string id)
		{
			Templates.FirstOrDefault(template => template.Id == id)?.Refresh();
		}

		private void LayoutTemplatesService_RulesChanged(object? sender, EventArgs e)
		{
			foreach (var template in Templates)
				template.Refresh();
		}
	}
}
