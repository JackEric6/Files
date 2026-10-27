// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using Files.App.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using WinRT;

namespace Files.App.Views.Settings
{
	/// <summary>
	/// Lets the user choose the columns of <see cref="DetailsLayoutPage"/> from a grouped list.
	/// </summary>
	public sealed partial class ColumnCustomizationPage : Page
	{
		private WindowEx? _hostWindow;

		public ColumnCustomizationViewModel ViewModel { get; private set; } = null!;

		public FrameworkElement TitleBarElement => WindowTitleBar;

		public ColumnCustomizationPage()
		{
			InitializeComponent();
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			if (e.Parameter is ColumnCustomizationNavigationArguments { Window: var window, ViewModel: var viewModel })
			{
				_hostWindow = window;
				ViewModel = viewModel;
				Bindings.Update();
			}
		}

		[DynamicWindowsRuntimeCast(typeof(CheckBox))]
		private void ColumnCheckBox_Click(object sender, RoutedEventArgs e)
		{
			if (sender is CheckBox { DataContext: ColumnPickerItem item } checkBox)
			{
				ViewModel.Toggle(item);
				checkBox.IsChecked = item.IsChecked;
			}
		}

		private void ColumnsList_ItemClick(object sender, ItemClickEventArgs e)
		{
			if (e.ClickedItem is not ColumnPickerItem item)
				return;

			if (item.IsGroup)
				ViewModel.ToggleExpanded(item);
			else
				ViewModel.Toggle(item);
		}

		[DynamicWindowsRuntimeCast(typeof(FrameworkElement))]
		[DynamicWindowsRuntimeCast(typeof(Control))]
		private void ColumnsList_KeyDown(object sender, KeyRoutedEventArgs e)
		{
			if ((e.OriginalSource as FrameworkElement)?.DataContext is not ColumnPickerItem item)
				return;

			switch (e.Key)
			{
				case VirtualKey.Right when item.IsGroup && !item.IsExpanded:
				case VirtualKey.Left when item.IsGroup && item.IsExpanded:
					ViewModel.ToggleExpanded(item);
					e.Handled = true;
					break;
				case VirtualKey.Left when item.IsColumn:
					(ColumnsList.ContainerFromItem(item.Group) as Control)?.Focus(FocusState.Keyboard);
					e.Handled = true;
					break;
			}
		}

		private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
		{
			ViewModel.RestoreDefaults();
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			ViewModel.Save();
			_hostWindow?.Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			_hostWindow?.Close();
		}
	}
}
