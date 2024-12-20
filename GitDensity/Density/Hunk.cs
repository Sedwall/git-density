﻿/// ---------------------------------------------------------------------------------
///
/// Copyright (c) 2024 Sebastian Hönel [sebastian.honel@lnu.se]
///
/// https://github.com/MrShoenel/git-density
///
/// This file is part of the project GitDensity. All files in this project,
/// if not noted otherwise, are licensed under the GPLv3-license. You will
/// find a copy of this file in the project's root directory.
///
/// Note that the license changed from MIT to GPLv3. In general, the license
/// from the latest public commit applies.
///
/// ---------------------------------------------------------------------------------
///
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Util.Extensions;
using Util.Logging;

namespace GitDensity.Density
{
	/// <summary>
	/// Represents lines added/removed/changed within a diff. Note that a diff
	/// can consist of multiple <see cref="Hunk"/>s for one file. Use
	/// <see cref="HunksForPatch(PatchEntryChanges, DirectoryInfo, DirectoryInfo)"/>
	/// to obtain all hunks for a <see cref="PatchEntryChanges"/> for one file.
	/// </summary>
	public class Hunk
	{
		private static BaseLogger<Hunk> logger = Program.CreateLogger<Hunk>();

		/// <summary>
		/// Used to split and analyze a git-diff hunk.
		/// </summary>
		protected internal static readonly Regex HunkSplitRegex =
			new Regex(
				@"^@@\s+\-((?<oldStart>[0-9]+),)?(?<oldNum>[0-9]+)\s+\+((?<newStart>[0-9]+),)?(?<newNum>[0-9]+)\s+@@.*$",
				RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Multiline);

		public UInt32 OldLineStart { get; protected internal set; }

		/// <summary>
		/// The number of lines of the entire <see cref="Hunk"/> before modification.
		/// This includes also unaffected (context) lines.
		/// </summary>
		public UInt32 OldNumberOfLines { get; protected internal set; }

		public UInt32 NewLineStart { get; protected internal set; }

		/// <summary>
		/// The number of lines of the entire <see cref="Hunk"/> after modification.
		/// This includes also unaffected (context) lines.
		/// </summary>
		public UInt32 NewNumberOfLines { get; protected internal set; }

		public String SourceFilePath { get; protected internal set; }

		public String TargetFilePath { get; protected internal set; }

		public UInt32 NumberOfLinesAdded { get { return (UInt32)this.lineNumbersAdded.Count; } }

		public UInt32 NumberOfLinesDeleted { get { return (UInt32)this.lineNumbersDeleted.Count; } }

		public IReadOnlyList<UInt32> LineNumbersAdded { get => this.lineNumbersAdded.ToList().AsReadOnly(); }

		public IReadOnlyList<UInt32> LineNumbersDeleted { get => this.lineNumbersDeleted.ToList().AsReadOnly(); }
		
		public String Patch { protected set; get; }

		protected IList<UInt32> lineNumbersAdded;

		protected IList<UInt32> lineNumbersDeleted;

		private Hunk(String patch)
		{
			this.Patch = patch;

			this.lineNumbersAdded = new List<UInt32>();
			this.lineNumbersDeleted = new List<UInt32>();
		}

		/// <summary>
		/// Sets all referenced objects to null and clears all lists.
		/// </summary>
		/// <returns>This <see cref="Hunk"/> for chaining.</returns>
		public Hunk Clear()
		{
			this.SourceFilePath = null;
			this.TargetFilePath = null;
			this.lineNumbersAdded.Clear();
			this.lineNumbersDeleted.Clear();
			this.Patch = null;
			return this;
		}

		/// <summary>
		/// Computes the numbers (their index) of lines that have been added
		/// or removed. Note that indexes start with 1, not 0.
		/// It is essential to only call this method _after_ the properties
		/// <see cref="OldLineStart"/> and <see cref="NewLineStart"/> have been
		/// set; otherwise, the computed numbers will be off by these.
		/// </summary>
		/// <returns>This <see cref="Hunk"/> for chaining.</returns>
		private Hunk ComputeLinesAddedAndDeleted()
		{
			var idxOld = this.OldLineStart;
			var idxNew = this.NewLineStart;

			foreach (var line in Patch.GetLines())
			{
				var firstChar = line.Length == 0 ? 'X' : line[0];
				if (firstChar != '+' && firstChar != '-')
				{
					// No line affected
					idxOld++;
					idxNew++;
					continue;
				}
				
				if (firstChar == '-')
				{
					this.lineNumbersDeleted.Add(idxOld++);
				}
				else if (firstChar == '+')
				{
					this.lineNumbersAdded.Add(idxNew++);
				}
				else
				{
					// No addition or deletion
					idxOld++;
					idxNew++;
				}
			}

			return this;
		}

		/// <summary>
		/// Returns true if this <see cref="Hunk"/> was created as a result of adding a
		/// new, empty file. This can happen by using <see cref="HunksForPatch(PatchEntryChanges, DirectoryInfo, DirectoryInfo)"/>.
		/// </summary>
		public Boolean RepresentsNewEmptyFile =>
			this.OldLineStart == 0u && this.OldNumberOfLines == 0u && this.NewLineStart == 0u && this.NewNumberOfLines == 0u && this.Patch == String.Empty;

		/// <summary>
		/// An override for <see cref="HunksForPatch(PatchEntryChanges, DirectoryInfo, DirectoryInfo)"/>
		/// that uses a dummy directory. Use this method only when you do not have any
		/// reason to access or rely on a valid <see cref="Hunk.SourceFilePath"/> or
		/// <see cref="Hunk.TargetFilePath"/> (useful for purely virtual use-cases when
		/// no access to the underlying file is needed).
		/// </summary>
		/// <param name="pec"></param>
		/// <returns></returns>
		public static IEnumerable<Hunk> HunksForPatch(PatchEntryChanges pec)
		{
			var dummyDir = new DirectoryInfo(Path.GetTempPath());
			return Hunk.HunksForPatch(pec, dummyDir, dummyDir);
		}

		/// <summary>
		/// Returns an <see cref="IEnumerable{Hunk}"/> containing all hunks
		/// for the given <see cref="PatchEntryChanges"/>.
		/// </summary>
		/// <param name="pec"></param>
		/// <param name="pairSourceDirectory"></param>
		/// <param name="pairTargetDirectory"></param>
		/// <returns></returns>
		public static IEnumerable<Hunk> HunksForPatch(PatchEntryChanges pec, DirectoryInfo pairSourceDirectory, DirectoryInfo pairTargetDirectory)
		{
			String fullSourcePath, fullTargetPath;
			Exception hunkPathException;

			// First condition is an empty patch that is usually the result from adding a new, empty, diffable file.
			// Second condition is a pure Move/Rename (then there's no real diff, i.e. no added/del'd lines).
			// Third condition is a deletion of a whole, empty, diffable file.
			//
			// We will only return one empty Hunk for this case. This is important as all methods expect
			// at least one Hunk from this enumeration, even if it's empty (check references).
			if ((pec.Mode == Mode.NonExecutableFile && pec.OldMode == Mode.Nonexistent && pec.LinesAdded == 0)
				|| (pec.Status == ChangeKind.Renamed && pec.LinesAdded == 0 && pec.LinesDeleted == 0)
				|| (pec.Status == ChangeKind.Deleted && pec.Mode == Mode.Nonexistent && pec.LinesDeleted == 0))
			{
				if (!Hunk.TryGetHunkPaths(
					pairSourceDirectory, pairTargetDirectory,
					pec.OldPath, pec.Path,
					out fullSourcePath, out fullTargetPath, out hunkPathException))
				{
					Hunk.WarnAboutBrokenHunkPaths(
						hunkPathException, pairSourceDirectory, pairTargetDirectory, pec.OldPath, pec.Path);
				}

				// This is an empty patch that is usually the result from adding a new, empty file.
				// We will only return one empty Hunk for this case.
				/// <see cref="RepresentsNewEmptyFile"/>
				yield return new Hunk(String.Empty)
				{
					OldLineStart = 0u,
					OldNumberOfLines = 0u,
					NewLineStart = 0u,
					NewNumberOfLines = 0u,
					SourceFilePath = fullSourcePath,
					TargetFilePath = fullTargetPath
				};
				yield break;
			}

			foreach (var hunk in Hunk.SplitPatch(pec.Patch))
			{
				if (!Hunk.TryGetHunkPaths(
					pairSourceDirectory, pairTargetDirectory,
					pec.OldPath, pec.Path,
					out fullSourcePath, out fullTargetPath, out hunkPathException))
				{
					Hunk.WarnAboutBrokenHunkPaths(
						hunkPathException, pairSourceDirectory, pairTargetDirectory, pec.OldPath, pec.Path);
				}

				hunk.SourceFilePath = fullSourcePath;
				hunk.TargetFilePath = fullTargetPath;

				yield return hunk.ComputeLinesAddedAndDeleted(); // Important to call having set the props;
			}
		}

		//private static readonly Regex RegexTrimDirectorySlashes =
		//	new Regex(@"^(?:/|\)([^/\]*)(?:/|\)$", RegexOptions.Compiled | RegexOptions.ECMAScript);

		/// <summary>
		/// Returns a string that does neither end nor start with a slash or backslash.
		/// </summary>
		/// <param name="orgPath"></param>
		/// <returns></returns>
		private static String trimPathSep(String orgPath) => orgPath.Trim('/', '\\');

		/// <summary>
		/// Attempts to obtain paths to a <see cref="Hunk"/>'s source/target files by combining
		/// directories and filenames. However, sometimes these contain invalid chars. In that
		/// case, this method will resort to manually concatenating these and return false; true,
		/// otherwise.
		/// A warning is logged using the <see cref="Hunk"/>'s <see cref="logger"/>.
		/// </summary>
		/// <param name="pairSourceDirectory"></param>
		/// <param name="pairTargetDirectory"></param>
		/// <param name="hunkSourcePath"></param>
		/// <param name="hunkTargetPath"></param>
		/// <param name="fullSourcePath"></param>
		/// <param name="fullTargetPath"></param>
		/// <param name="ex"></param>
		/// <returns></returns>
		protected static Boolean TryGetHunkPaths(
			DirectoryInfo pairSourceDirectory, DirectoryInfo pairTargetDirectory,
			String hunkSourcePath, String hunkTargetPath,
			out String fullSourcePath, out String fullTargetPath,
			out Exception ex)
		{
			try
			{
				fullSourcePath = new DirectoryInfo(
					Path.Combine(pairSourceDirectory.FullName, hunkSourcePath)).FullName;
				fullTargetPath = new DirectoryInfo(
					Path.Combine(pairTargetDirectory.FullName, hunkTargetPath)).FullName;

				ex = null;
				return true;
			}
			catch (Exception tempEx)
			{
				fullSourcePath = trimPathSep(pairSourceDirectory.FullName)
					+ Path.DirectorySeparatorChar
					+ trimPathSep(hunkSourcePath);
				fullTargetPath = trimPathSep(pairTargetDirectory.FullName)
					+ Path.DirectorySeparatorChar
					+ trimPathSep(hunkTargetPath);

				ex = tempEx;
				return false;
			}
		}

		/// <summary>
		/// Issues a warning using the <see cref="logger"/>, when, for some reason,
		/// the method <see cref="TryGetHunkPaths(DirectoryInfo, DirectoryInfo, string, string, out string, out string, out Exception)"/> fails to obtain the <see cref="Hunk"/>'s paths.
		/// </summary>
		/// <param name="ex"></param>
		/// <param name="pairSourceDirectory"></param>
		/// <param name="pairTargetDirectory"></param>
		/// <param name="hunkSourcePath"></param>
		/// <param name="hunkTargetPath"></param>
		protected static void WarnAboutBrokenHunkPaths(
			Exception ex,
			DirectoryInfo pairSourceDirectory, DirectoryInfo pairTargetDirectory,
			String hunkSourcePath, String hunkTargetPath)
		{
			logger.LogWarning(ex, $"Obtaining the paths for a Hunk failed, returning manually concatenated paths. {nameof(pairSourceDirectory)}: {pairSourceDirectory.FullName}, {nameof(pairTargetDirectory)}: {pairTargetDirectory.FullName}, {nameof(hunkSourcePath)}: {hunkSourcePath}, {nameof(hunkTargetPath)}: {hunkTargetPath}");
		}

		/// <summary>
		/// Uses <see cref="HunkSplitRegex"/> to properly split the diff-output into line-numbers
		/// </summary>
		/// <param name="patchFromDiff"></param>
		/// <returns></returns>
		protected internal static IEnumerable<Hunk> SplitPatch(String patchFromDiff)
		{
			var matches = HunkSplitRegex.Matches(patchFromDiff).Cast<Match>().ToList();
			
			for (int i = 0; i < matches.Count; i++)
			{
				var contentIdx = matches[i].Index + matches[i].Length;
				var content = i + 1 == matches.Count ? patchFromDiff.Substring(contentIdx) :
					patchFromDiff.Substring(contentIdx, matches[i + 1].Index - contentIdx);

				var g = matches[i].Groups;
				yield return new Hunk(content.TrimStart('\n'))
				{
					OldLineStart = g["oldStart"].Success ? UInt32.Parse(g["oldStart"].Value) : 0u,
					OldNumberOfLines = g["oldNum"].Success ? UInt32.Parse(g["oldNum"].Value) : 0u,
					NewLineStart = g["newStart"].Success ? UInt32.Parse(g["newStart"].Value) : 0u,
					NewNumberOfLines = g["newNum"].Success ? UInt32.Parse(g["newNum"].Value) : 0u
				};
			}
		}
	}
}
