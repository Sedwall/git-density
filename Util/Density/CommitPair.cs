﻿/// ---------------------------------------------------------------------------------
///
/// Copyright (c) 2018 Sebastian Hönel [sebastian.honel@lnu.se]
///
/// https://github.com/MrShoenel/git-density
///
/// This file is part of the project Util. All files in this project,
/// if not noted otherwise, are licensed under the MIT-license.
///
/// ---------------------------------------------------------------------------------
///
/// Permission is hereby granted, free of charge, to any person obtaining a
/// copy of this software and associated documentation files (the "Software"),
/// to deal in the Software without restriction, including without limitation
/// the rights to use, copy, modify, merge, publish, distribute, sublicense,
/// and/or sell copies of the Software, and to permit persons to whom the
/// Software is furnished to do so, subject to the following conditions:
///
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
///
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
/// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
///
/// ---------------------------------------------------------------------------------
///
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Util.Extensions;

namespace Util.Density
{
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>If disposed and the <see cref="Patch"/> has been
	/// accessed, will dispose the <see cref="Patch"/>.</remarks>
	[DebuggerDisplay("CommitPair of {Parent}  and  {Child}")]
	public class CommitPair : IDisposable, IEquatable<CommitPair>, ISupportsExecutionPolicy
	{
		private Tuple<Commit, Commit> pair;

		public Repository Repository { get; protected set; }

		/// <summary>
		/// The child-commit represents the next commit on the same branch
		/// that springs off its parent (i.e. it is younger/newer).
		/// </summary>
		public Commit Child { get { return this.pair.Item1; } }

		/// <summary>
		/// The parent-commit is the ancestor on the same branch that
		/// directly precedes the <see cref="Child"/>.
		/// </summary>
		public Commit Parent { get { return this.pair.Item2; } }

		/// <summary>
		/// Returns a string that uniquely identifies this pair. The string
		/// is guaranteed not to be longer than 32 characters and contains
		/// sub-strings of the parent- and child-commit's SHA1-IDs.
		/// </summary>
		public String Id
		{
			get => $"{this.Parent?.Sha.Substring(0, 15) ?? "(initial)"}_{Child.Sha.Substring(0, 15)}";
		}

		private Lazy<Patch> lazyPatch;

		/// <summary>
		/// The <see cref="Patch"/> that represents the difference between
		/// parent- and child-commit. Computed using <see cref="Repository.Diff"/>.
		/// </summary>
		public Patch Patch => this.lazyPatch.Value;

		private Lazy<TreeChanges> lazyTree;

		/// <summary>
		/// The <see cref="TreeChanges"/> that represent the difference
		/// between the two file-system trees of both commits, computed
		/// using <see cref="Repository.Diff"/>.
		/// </summary>
		public TreeChanges TreeChanges => this.lazyTree.Value;

		/// <summary>
		/// Set the <see cref="ExecutionPolicy"/> for parallel operations. Currently
		/// supported within <see cref="WriteOutTree(IEnumerable{TreeEntryChanges}, DirectoryInfo, bool, string, string)"/>.
		/// </summary>
		public ExecutionPolicy ExecutionPolicy { get; set; } = ExecutionPolicy.Parallel;

		/// <summary>
		/// C'tor; initializes the pair and its patch.
		/// </summary>
		/// <param name="repo"></param>
		/// <param name="child"></param>
		/// <param name="parent"></param>
		internal CommitPair(Repository repo, Commit child, Commit parent = null)
		{
			this.Repository = repo;
			this.pair = Tuple.Create(child, parent);

			this.lazyPatch = new Lazy<Patch>(() =>
			{
				return this.Repository.Diff.Compare<Patch>(this.Parent?.Tree, this.Child.Tree);
			});

			this.lazyTree = new Lazy<TreeChanges>(() =>
			{
				return this.Repository.Diff.Compare<TreeChanges>(this.Parent?.Tree, this.Child.Tree);
			});
		}

		/// <summary>
		/// Retrieves a <see cref="CommitPair"/> by ID, where the ID has been
		/// obtained from <see cref="CommitPair.Id"/> (and follows its format).
		/// </summary>
		/// <param name="commitPairId"></param>
		/// <param name="repository"></param>
		/// <returns></returns>
		public static CommitPair FromId(String commitPairId, Repository repository)
		{
			var parentId = commitPairId.Split('_')[0];
			var childId = commitPairId.Split('_')[1];

			var parentCommit = repository.Commits.Where(c => c.Sha.StartsWith(parentId)).Single();
			var childCommit = repository.Commits.Where(c => c.Sha.StartsWith(childId)).Single();

			return new CommitPair(repository, childCommit, parentCommit);
		}

		#region CommitPair specific methods
		/// <summary>
		/// Writes out all changes of a <see cref="Tree"/>. A change is obtained as a result
		/// from comparing two <see cref="Commit"/>s (or a <see cref="CommitPair"/>). Writes
		/// out all files with their relative  path into the target directory that existed in
		/// the old/previous commit and the new/current commit.
		/// </summary>
		/// <param name="changes">List of changes (differences) between two files.</param>
		/// <param name="targetDirectory">Relative root-directory to write files to.</param>
		/// <param name="wipeTargetDirectoryBefore">True, if the target directory should be
		/// cleared first.</param>
		/// <param name="parentDirectoryName">Name of directory in target-directory to write
		/// files of old/previous commit to.</param>
		/// <param name="childDirectoryName">Name of directory in target-directory to write
		/// files of new/current commit to.</param>
		public void WriteOutTree(IEnumerable<TreeEntryChanges> changes, DirectoryInfo targetDirectory, Boolean wipeTargetDirectoryBefore = true, String parentDirectoryName = "old", String childDirectoryName = "new")
		{
			if (!targetDirectory.Exists)
			{
				targetDirectory.Create();
			}

			if (wipeTargetDirectoryBefore && targetDirectory.Exists)
			{
				targetDirectory.Clear();
			}

			var diOld = new DirectoryInfo(Path.Combine(targetDirectory.FullName, parentDirectoryName));
			if (!diOld.Exists) { diOld.Create(); }
			var diNew = new DirectoryInfo(Path.Combine(targetDirectory.FullName, childDirectoryName));
			if (!diNew.Exists) { diNew.Create(); }

			if (!(this.Parent is Commit && this.Child is Commit))
			{
				// We can only write out changes where an old and a new version exists.
				// That means that adds and deletes cannot be written out.
				return;
			}

			var parallelOptions = new ParallelOptions();
			if (this.ExecutionPolicy == ExecutionPolicy.Linear)
			{
				parallelOptions.MaxDegreeOfParallelism = 1;
			}
			
			Parallel.ForEach(changes.Select(change => new
			{
				OldTreeEntry = this.Parent[change.OldPath],
				NewTreeEntry = this.Child[change.Path]
			}).Where(anon =>
				anon.OldTreeEntry is TreeEntry && anon.NewTreeEntry is TreeEntry
			),
				parallelOptions,
				anon =>
			{
				if (anon.OldTreeEntry.TargetType == TreeEntryTargetType.Blob
					&& anon.NewTreeEntry.TargetType == TreeEntryTargetType.Blob)
				{
					Task.WaitAll(
						Task.Run(() => anon.OldTreeEntry.WriteOutTreeEntry(diOld)),
						Task.Run(() => anon.NewTreeEntry.WriteOutTreeEntry(diNew))
					);
				}
			});
		}
		#endregion

		#region Equals
		public bool Equals(CommitPair other)
		{
			return this.pair.Equals(other);
		}
		#endregion

		#region Dispose
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (this.lazyPatch.IsValueCreated)
				{
					this.lazyPatch.Value.Clear();
					this.lazyPatch.Value.Dispose();
				}
				if (this.lazyTree.IsValueCreated)
				{
					this.lazyTree.Value.Dispose();
				}

				this.lazyPatch = null;
				this.lazyTree = null;
			}
		}
		#endregion
	}
}
