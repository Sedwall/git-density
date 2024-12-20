﻿/// ---------------------------------------------------------------------------------
///
/// Copyright (c) 2024 Sebastian Hönel [sebastian.honel@lnu.se]
///
/// https://github.com/MrShoenel/git-density
///
/// This file is part of the project UtilTests. All files in this project,
/// if not noted otherwise, are licensed under the GPLv3-license. You will
/// find a copy of this file in the project's root directory.
///
/// Note that the license changed from MIT to GPLv3. In general, the license
/// from the latest public commit applies.
///
/// ---------------------------------------------------------------------------------
///
using Iesi.Collections.Generic;
using LibGit2Sharp;
using LINQtoCSV;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Util.Extensions;
using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;


namespace GitTools.SourceExport
{
    /// <summary>
    /// Represents an entire commit to be exported.
    /// </summary>
    [JsonObject]
    public class ExportableCommit : ExportableEntity, IEnumerable<ExportableFile>
    {
        [JsonIgnore]
        protected LinkedHashSet<ExportableFile> expoFiles;

        public ExportableCommit(ExportCommitPair exportCommit) : base(exportCommit)
        {
            this.expoFiles = new LinkedHashSet<ExportableFile>();
        }

        /// <summary>
        /// The hash of the child commit.
        /// </summary>
        [CsvColumn(FieldIndex = 1)]
        [JsonProperty(Order = 1)]
        public String SHA1 { get => this.ExportCommitPair.Child.ShaShort(); }

        /// <summary>
        /// The hash of the parent commit.
        /// </summary>
        [CsvColumn(FieldIndex = 2)]
        [JsonProperty(Order = 2)]
        public String SHA1_Parent { get => this.ExportCommitPair.Parent?.ShaShort() ?? "(initial)"; }

        [CsvColumn(FieldIndex = 3)]
        [JsonProperty(Order = 3)]
        public String Message { get => this.ExportCommitPair.Child.Message.Trim(); }

        [CsvColumn(FieldIndex = 4)]
        [JsonProperty(Order = 4)]
        public String AuthorName { get => this.ExportCommitPair.Child.Author.Name; }

        [CsvColumn(FieldIndex = 5)]
        [JsonProperty(Order = 5)]
        public String AuthorEmail { get => this.ExportCommitPair.Child.Author.Email; }

        [CsvColumn(FieldIndex = 6, OutputFormat = "yyyy-MM-dd HH:MM:ss")]
        [JsonProperty(Order = 6)]
        public DateTime AuthorTime { get => this.ExportCommitPair.Child.Author.When.UtcDateTime; }

        [CsvColumn(FieldIndex = 7)]
        [JsonProperty(Order = 7)]
        public String CommitterName { get => this.ExportCommitPair.Child.Committer.Name; }

        [CsvColumn(FieldIndex = 8)]
        [JsonProperty(Order = 8)]
        public String CommitterEmail { get => this.ExportCommitPair.Child.Committer.Email; }

        [CsvColumn(FieldIndex = 9, OutputFormat = "yyyy-MM-dd HH:MM:ss")]
        [JsonProperty(Order = 9)]
        public DateTime CommitterTime { get => this.ExportCommitPair.Child.Committer.When.UtcDateTime; }

        [CsvColumn(FieldIndex = 10)]
        [JsonProperty(Order = 10)]
        public Boolean IsInitialCommit { get => this.ExportCommitPair.Child.Parents.Count() == 0; }

        [CsvColumn(FieldIndex = 11)]
        [JsonProperty(Order = 11)]
        public Boolean IsMergeCommit { get => this.ExportCommitPair.Child.Parents.Count() > 1; }

        [CsvColumn(FieldIndex = 12)]
        [JsonProperty(Order = 12)]
        public UInt32 NumberOfParentCommits { get => (uint)this.ExportCommitPair.Child.Parents.Count(); }

        /// <summary>
        /// A map between child and parent commit time as the <see cref="TimeSpan"/> between
        /// both. The key is the parent's SHA1 and the value is in fractional minutes.
        /// </summary>
        [CsvColumn(FieldIndex = 13)]
        [JsonProperty(Order = 13)]
        public Double? DaysSinceParentCommit { get => this.ExportCommitPair.Parent is Commit ? (this.ExportCommitPair.Child.Committer.When.UtcDateTime - this.ExportCommitPair.Parent.Committer.When.UtcDateTime).TotalDays : (Double?)null; }



        /// <summary>
        /// Add an <see cref="ExportableFile"/> to this commit.
        /// </summary>
        /// <param name="expoFile"></param>
        /// <returns></returns>
        public ExportableCommit AddFile(ExportableFile expoFile)
        {
            this.expoFiles.Add(expoFile);
            return this;
        }

        /// <summary>
        /// Returns a concatenation of all <see cref="ExportableFile"/>s. Each file itself is
        /// a concatenation of its hunks. Here, we prefix each file by its <see cref="ExportableFile.TreeChangeIntent"/>
        /// and its <see cref="ExportableFile.FileName"/> before dumping its content to allow
        /// the files to be separable later on.
        /// </summary>
        public override string ContentInteral => String.Join("\n\n", this.Select(ef =>
        {
            return $"({ef.TreeChangeIntent}) {ef.FileName}:\n-------------\n{ef.ContentInteral}";
        }));

        public IEnumerator<ExportableFile> GetEnumerator()
        {
            return this.expoFiles.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
