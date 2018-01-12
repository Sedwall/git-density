﻿/// ---------------------------------------------------------------------------------
///
/// Copyright (c) 2018 Sebastian Hönel [sebastian.honel@lnu.se]
///
/// https://github.com/MrShoenel/git-density
///
/// This file is part of the project GitHours. All files in this project,
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
using System.Linq;

namespace GitHours.Hours
{
	/// <summary>
	/// Implements the main functionality from this script:
	/// <see cref="https://github.com/kimmobrunfeldt/git-hours/blob/master/src/index.js"/>
	/// </summary>
	internal class GitHours
	{
		/// <summary>
		/// Used for <see cref="Since"/> and <see cref="Until"/>. If such date/times are
		/// supplied, they must match this format exactly (24 hours format).
		/// </summary>
		public static readonly String DateTimeFormat = "yyyy-MM-dd HH:mm";

		public Repository Repository { get; protected set; }

		public UInt32 MaxCommitDiffInMinutes { get; protected set; } = 2 * 60;

		public UInt32 FirstCommitAdditionInMinutes { get; protected set; } = 2 * 60;

		/// <summary>
		/// Defaults to <see cref="DateTime.MinValue"/> if not supplied to c'tor.
		/// </summary>
		public DateTime Since { get; protected set; } = DateTime.MinValue;

		/// <summary>
		/// Defaults to <see cref="DateTime.MaxValue"/> if not supplied to c'tor.
		/// </summary>
		public DateTime Until { get; protected set; } = DateTime.MaxValue;

		/// <summary>
		/// Created by c'tor and based on <see cref="Since"/>/<see cref="Until"/> dates.
		/// </summary>
		private IList<Commit> commits;

		/// <summary>
		/// Constructor for the <see cref="GitHours"/> class.
		/// </summary>
		/// <param name="repo">The <see cref="Repository"/> to analyze.</param>
		/// <param name="maxCommitDiffInMinutes"></param>
		/// <param name="firstCommitAdditionInMinutes"></param>
		/// <param name="since"></param>
		/// <param name="until"></param>
		public GitHours(Repository repo, UInt32? maxCommitDiffInMinutes = null, UInt32? firstCommitAdditionInMinutes = null, DateTime? since = null, DateTime? until = null)
		{
			this.Repository = repo;
			if (maxCommitDiffInMinutes.HasValue)
			{
				this.MaxCommitDiffInMinutes = maxCommitDiffInMinutes.Value;
			}
			if (firstCommitAdditionInMinutes.HasValue)
			{
				this.FirstCommitAdditionInMinutes = firstCommitAdditionInMinutes.Value;
			}
			if (since.HasValue)
			{
				this.Since = since.Value;
			}
			if (until.HasValue)
			{
				this.Until = until.Value;
			}


			this.commits = this.Repository.Commits.Where(commit =>
			{
				return commit.Committer.When.DateTime >= this.Since && commit.Committer.When.DateTime < this.Until;
			}).ToList();
		}

		/// <summary>
		/// Analyzes the <see cref="Repository"/> and returns the computed hours for each
		/// developer and in total as <see cref="GitHoursAnalysisResult"/>.
		/// </summary>
		/// <returns></returns>
		public GitHoursAnalysisResult Analyze()
		{
			var commitsByEmail = this.commits.GroupBy(commit => commit.Author?.Email ?? "unknown");
			var authorWorks = commitsByEmail.Where(authorCommits => authorCommits.Any()).Select(authorCommits =>
			{
				return new GitHoursAuthorStats
				{
					Email = authorCommits.Key,
					Name = authorCommits.First().Author.Name,
					Hours = this.Estimate(authorCommits.Select(commit => commit.Committer.When.DateTime).ToArray()),
					Commits = (UInt32)authorCommits.Count()
				};
			});

			return new GitHoursAnalysisResult
			{
				TotalHours = Math.Round(authorWorks.Aggregate(0d, (sum, authorWork) => sum + authorWork.Hours), 2),
				TotalCommits = (UInt32)this.commits.Count(),
				AuthorStats = authorWorks.OrderBy(aw => aw.Hours),

				FirstCommitAdditionInMinutes = this.FirstCommitAdditionInMinutes,
				MaxCommitDiffInMinutes = this.MaxCommitDiffInMinutes,
				RepositoryPath = this.Repository.Info.WorkingDirectory,
				Since = this.Since,
				Until = this.Until
			};
		}

		/// <summary>
		/// Returns the estimated hours for an array of dates as <see cref="DateTime"/>s.
		/// </summary>
		/// <param name="dates"></param>
		/// <returns>An amount of hours as <see cref="Double"/>.</returns>
		protected Double Estimate(DateTime[] dates)
		{
			if (dates.Length < 2)
			{
				return 0;
			}

			var sortedDates = dates.OrderBy(d => d).ToArray();
			var allButLast = sortedDates.Reverse().Skip(1).Reverse().ToList();

			var index = 0;
			var totalHours = allButLast.Aggregate(0d, (hours, date) =>
			{
				var nextDate = sortedDates[index + 1];
				index++;
				var diffInMinutes = (nextDate - date).TotalMinutes;

				if (diffInMinutes < this.MaxCommitDiffInMinutes)
				{
					return hours + (double)diffInMinutes / 60d;
				}

				return hours + (double)this.FirstCommitAdditionInMinutes / 60d;
			});

			return Math.Round(totalHours, 2);
		}
	}
}