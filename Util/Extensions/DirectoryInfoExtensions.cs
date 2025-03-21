﻿/// ---------------------------------------------------------------------------------
///
/// Copyright (c) 2024 Sebastian Hönel [sebastian.honel@lnu.se]
///
/// https://github.com/MrShoenel/git-density
///
/// This file is part of the project Util. All files in this project,
/// if not noted otherwise, are licensed under the GPLv3-license. You will
/// find a copy of this file in the project's root directory.
///
/// Note that the license changed from MIT to GPLv3. In general, the license
/// from the latest public commit applies.
///
/// ---------------------------------------------------------------------------------
///
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util.Extensions
{
	/// <summary>
	/// Extension-methods for <see cref="DirectoryInfo"/>.
	/// </summary>
	public static class DirectoryInfoExtensions
	{
		/// <summary>
		/// Attempts to delete this <see cref="DirectoryInfo"/> and returns a boolean value
		/// that indicates the success of the operation.
		/// </summary>
		/// <param name="directoryInfo"></param>
		/// <param name="recursive"></param>
		/// <returns></returns>
		public static Boolean TryDelete(this DirectoryInfo directoryInfo, Boolean recursive = true)
		{
			try
			{
				directoryInfo.Delete(recursive);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Clears a <see cref="DirectoryInfo"/>. That means that all files and folders
		/// in it will be deleted recursively, without the actual directory being deleted.
		/// </summary>
		/// <param name="directoryInfo">The director to clear/wipe/empty.</param>
		/// <returns>The same given <see cref="DirectoryInfo"/>.</returns>
		public static DirectoryInfo Clear(this DirectoryInfo directoryInfo)
		{
			foreach (var file in directoryInfo.GetFiles())
			{
				file.Delete();
			}
			foreach (var dir in directoryInfo.GetDirectories())
			{
				dir.Delete(true);
			}

			return directoryInfo;
		}

		/// <summary>
		/// Attempts to clear this <see cref="DirectoryInfo"/> and returns a boolean that
		/// indicates the success of the operation.
		/// </summary>
		/// <param name="directoryInfo"></param>
		/// <returns></returns>
		public static Boolean TryClear(this DirectoryInfo directoryInfo)
		{
			foreach (var file in directoryInfo.GetFiles())
			{
				try
				{
					file.Delete();
				} catch
				{
					return false;
				}
			}
			foreach (var dir in directoryInfo.GetDirectories())
			{
				if (!dir.TryDelete(true))
				{
					return false;
				}
			}

			return true;
		}
	}
}
