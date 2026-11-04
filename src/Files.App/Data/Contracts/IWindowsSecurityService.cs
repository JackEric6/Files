// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

namespace Files.App.Data.Contracts
{
	/// <summary>
	/// Provides service for security APIs on Windows.
	/// </summary>
	public interface IWindowsSecurityService
	{
		/// <summary>
		/// Gets a value that indicates whether the application is elevated.
		/// </summary>
		/// <returns>Returns true if the application is elevated; otherwise, false.</returns>
		bool IsAppElevated();

		/// <summary>
		/// Gets a value that indicates whether drag &amp; drop should be enabled in the application.
		/// </summary>
		/// <remarks>
		/// The application does not disable drag &amp; drop based on its elevation state.
		/// Windows may still restrict drag &amp; drop between processes at different integrity levels.
		/// <br/>
		/// <br/>
		/// For more information, see
		/// <see href="https://github.com/files-community/Files/issues/12390"/>.
		/// </remarks>
		/// <returns>Returns true if drag &amp; drop should be enabled in the application.</returns>
		bool CanDragAndDrop();

		/// <summary>
		/// Gets a value that indicates whether the application needs to be elevated for some operations.
		/// </summary>
		/// <param name="path"></param>
		/// <returns>True if the application needs to be elevated for some operations; otherwise, false.</returns>
		bool IsElevationRequired(string? path);
	}
}
