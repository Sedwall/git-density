﻿/// ---------------------------------------------------------------------------------
///
/// Copyright (c) 2018 Sebastian Hönel [sebastian.honel@lnu.se]
///
/// https://github.com/MrShoenel/git-density
///
/// This file is part of the project GitDensity. All files in this project,
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
using FluentNHibernate.Mapping;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Util.Extensions;
using Util.Logging;

namespace Util.Data.Entities
{
	public enum ProjectEntityLanguage
	{
		Java, PHP, C, CSharp
	}

	/// <summary>
	/// Represents an entity from the 'projects' table.
	/// </summary>
	public class ProjectEntity
	{
		public virtual UInt64 AiId { get; set; }
		public virtual UInt64 InternalId { get; set; }
		public virtual String Name { get; set; }
		public virtual ProjectEntityLanguage Language { get; set; }
		public virtual String CloneUrl { get; set; }
		public virtual Boolean WasCorrected { get; set; }

		public virtual RepositoryEntity Repository { get; set; }

		/// <summary>
		/// Cleans up <see cref="ProjectEntity"/>s in the database and attempts to probe
		/// and repair the git clone-URL. Checks and modifies <see cref="ProjectEntity.WasCorrected"/> as the status of an entity. Some entities
		/// have empty clone-URLs and cannot be fixed; those will be deleted, if required.
		/// </summary>
		/// <param name="deleteUselessEntities">If true, will delete such entities,
		/// that do not have a clone-URL. This method was primarily made to fix broken
		/// URLs. Entities without a repairable URL or any URL at all were considered
		/// useless.</param>
		public static void CleanUpDatabase(BaseLogger<ProjectEntity> logger, bool deleteUselessEntities = false, ExecutionPolicy execPolicy = ExecutionPolicy.Parallel)
		{
			using (var tempSess = DataFactory.Instance.OpenSession())
			{
				logger.LogDebug("Successfully probed the configured database.");


				var ps = tempSess.QueryOver<Data.Entities.ProjectEntity>().Where(p => !p.WasCorrected).Future();
				var toSave = new ConcurrentBag<ProjectEntity>();
				var toDelete = new ConcurrentBag<ProjectEntity>();
				var parallelOptions = new ParallelOptions();
				if (execPolicy == ExecutionPolicy.Linear)
				{
					parallelOptions.MaxDegreeOfParallelism = 1;
				}

				Parallel.ForEach(ps, parallelOptions, proj =>
				{
					if (String.IsNullOrEmpty(proj.CloneUrl))
					{
						logger.LogDebug("Entity with ID {0} has an empty clone-URL.", proj.AiId);
						toDelete.Add(proj);
						return;
					}

					String realUrl = null;
					try
					{
						realUrl = proj.CloneUrl.Substring(0, proj.CloneUrl.LastIndexOf('/')) + "/" + proj.Name + ".git";
					}
					catch (Exception e)
					{
						logger.LogError(e, e.Message);
					}

					if (realUrl == proj.CloneUrl)
					{
						proj.WasCorrected = true;
						toSave.Add(proj);
						return;
					}

					using (var wc = new HttpClient())
					{
						var req = new HttpRequestMessage(HttpMethod.Head, realUrl);
						var task = wc.SendAsync(req);
						task.Wait();
						if (task.Result.StatusCode == System.Net.HttpStatusCode.OK)
						{
							logger.LogDebug("OK: {0}", realUrl);
							// We should update the URL:
							proj.CloneUrl = realUrl;
							proj.WasCorrected = true;
							toSave.Add(proj);
						}
						else if (task.Result.StatusCode == System.Net.HttpStatusCode.NotFound)
						{
							logger.LogError("NOT FOUND: {0}", realUrl);
							// Usually happens when the repository disappears.
							toDelete.Add(proj);
						}
						else
						{
							logger.LogError("ERROR: {0}", realUrl);
						}
					}
				});


				if (deleteUselessEntities)
				{
					using (var trans = tempSess.BeginTransaction())
					{
						foreach (var item in toDelete)
						{
							tempSess.Delete(item);
						}
						trans.Commit();
						logger.LogInformation("Cleaned up {0} broken items.", toDelete.Count);
					}
				}


				foreach (var part in toSave.Partition(200))
				{
					using (var trans = tempSess.BeginTransaction())
					{
						foreach (var item in part)
						{
							tempSess.Update(item);
						}
						trans.Commit();
						logger.LogInformation("Repaired {0} items and stored them successfully.", part.Count);
					}
				}
			}
		}
	}

	/// <summary>
	/// Maps the entity-class <see cref="ProjectEntity"/>.
	/// </summary>
	public class ProjectEntityMap : ClassMap<ProjectEntity>
	{
		public ProjectEntityMap()
		{
			this.Table("projects");
			this.Id(x => x.AiId).Column("AI_ID");
			this.Map(x => x.InternalId).Column("INTERNAL_ID").Index("IDX_INTERNAL_ID");
			this.Map(x => x.Name).Column("NAME");
			this.Map(x => x.Language).Column("LANGUAGE")
				.Index("IDX_LANGUAGE")
				.CustomType<StringEnumMapper<ProjectEntityLanguage>>();
			this.Map(x => x.CloneUrl).Column("CLONE_URL");
			this.Map(x => x.WasCorrected).Column("WAS_CORRECTED");

			this.HasOne<RepositoryEntity>(x => x.Repository).Cascade.Lock();
		}
	}

	
}
