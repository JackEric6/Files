// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT;

namespace Files.App.Dialogs
{
	/// <summary>
	/// Asks the user about layout templates.
	/// </summary>
	public static class LayoutTemplateDialogs
	{
		private static ElementTheme AppTheme
		{
			[DynamicWindowsRuntimeCast(typeof(FrameworkElement))]
			get => ((FrameworkElement)MainWindow.Instance.Content).RequestedTheme;
		}

		/// <summary>
		/// Asks for a name and returns it, or null when the user cancels.
		/// </summary>
		[DynamicWindowsRuntimeCast(typeof(Style))]
		public static async Task<string?> AskNameAsync(string title, string primaryButtonText, string name)
		{
			var textBox = new TextBox()
			{
				Text = name,
				PlaceholderText = Strings.TemplateName.GetLocalizedResource(),
			};
			AutomationProperties.SetName(textBox, Strings.TemplateName.GetLocalizedResource());

			var dialog = new ContentDialog()
			{
				Title = title,
				Content = textBox,
				PrimaryButtonText = primaryButtonText,
				CloseButtonText = Strings.Cancel.GetLocalizedResource(),
				DefaultButton = ContentDialogButton.Primary,
				PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
				IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(name),
				Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
				RequestedTheme = AppTheme,
			};

			textBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(textBox.Text);
			dialog.Opened += (_, _) =>
			{
				textBox.Focus(FocusState.Programmatic);
				textBox.SelectAll();
			};

			return await dialog.TryShowAsync() is ContentDialogResult.Primary ? textBox.Text.Trim() : null;
		}

		/// <summary>
		/// Asks for file extensions and returns them, or null when the user cancels.
		/// </summary>
		/// <param name="findConflict">Explains why one of the extensions can't be used, or returns null.</param>
		[DynamicWindowsRuntimeCast(typeof(Brush))]
		[DynamicWindowsRuntimeCast(typeof(Style))]
		public static async Task<string?> EditFileTypesAsync(string extensions, Func<IEnumerable<string>, string?> findConflict)
		{
			var extensionsBox = new TextBox()
			{
				Header = Strings.FileExtensions.GetLocalizedResource(),
				PlaceholderText = ".jpg, .png",
				Text = extensions,
			};

			var errorText = new TextBlock()
			{
				Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
				TextWrapping = TextWrapping.WrapWholeWords,
				Visibility = Visibility.Collapsed,
			};
			AutomationProperties.SetLiveSetting(errorText, AutomationLiveSetting.Assertive);

			var dialog = new ContentDialog()
			{
				Title = Strings.FileTypes.GetLocalizedResource(),
				Content = new StackPanel()
				{
					Spacing = 8,
					Children = { extensionsBox, errorText },
				},
				PrimaryButtonText = Strings.Save.GetLocalizedResource(),
				CloseButtonText = Strings.Cancel.GetLocalizedResource(),
				DefaultButton = ContentDialogButton.Primary,
				PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
				Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
				RequestedTheme = AppTheme,
			};

			string[] newExtensions = [];

			void Validate()
			{
				newExtensions = new LayoutRule() { Extensions = extensionsBox.Text }.GetExtensions().ToArray();
				var error = newExtensions.FirstOrDefault(extension => !LayoutRule.IsValidExtension(extension)) is { } invalidExtension
					? string.Format(Strings.InvalidFileExtension.GetLocalizedResource(), invalidExtension)
					: findConflict(newExtensions);
				errorText.Text = error ?? string.Empty;
				errorText.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
				if (error is not null)
					FrameworkElementAutomationPeer.CreatePeerForElement(errorText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
				dialog.IsPrimaryButtonEnabled = newExtensions.Length > 0 && error is null;
			}

			Validate();
			extensionsBox.TextChanged += (_, _) => Validate();
			dialog.Opened += (_, _) => extensionsBox.Focus(FocusState.Programmatic);

			return await dialog.TryShowAsync() is ContentDialogResult.Primary ? string.Join(", ", newExtensions) : null;
		}

		/// <summary>
		/// Asks the user to confirm deleting a template.
		/// </summary>
		[DynamicWindowsRuntimeCast(typeof(Style))]
		public static async Task<bool> ConfirmDeleteAsync(string name)
		{
			var dialog = new ContentDialog()
			{
				Title = Strings.DeleteTemplateConfirmationTitle.GetLocalizedResource(),
				Content = string.Format(Strings.DeleteTemplateConfirmation.GetLocalizedResource(), name, Ioc.Default.GetRequiredService<ILayoutTemplatesService>().DefaultTemplate.DisplayName),
				PrimaryButtonText = Strings.Delete.GetLocalizedResource(),
				CloseButtonText = Strings.Cancel.GetLocalizedResource(),
				DefaultButton = ContentDialogButton.Close,
				Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
				RequestedTheme = AppTheme,
			};

			return await dialog.TryShowAsync() is ContentDialogResult.Primary;
		}
	}
}
