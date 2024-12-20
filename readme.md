# Git Density [![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.2565238.svg)](https://doi.org/10.5281/zenodo.2565238)

Git Density (`git-density`) is a tool to analyze `git`-repositories with the goal of detecting the source code density.

It was developed during the research phase of the short technical paper and poster "_A changeset-based approach to assess source code density and developer efficacy_" [1] and has since been extended to support thorough analyses and insights.

## Building and running

To build the application, restore all _nuget_ packages and simply rebuild all projects.

Run `GitDensity.exe`, which has an exhaustive command line interface for analyzing repositories. This implementation also includes a reimplementation of `git-hours` [2], runnable using `GitHours.exe` (with a similar command line interface).
There are also separate command line tools for extracting metrics (`GitMetrics.exe`) and smaller utility that unites a few stand-alone commands (`GitTools.exe`, see below).

## Requirement of external tools

This application relies on an external executable to run clone detection. Currently, it uses a local version of Softwerk's clone detection service [3].
To obtain a copy free for academic use of this tool, please contact sebastian.honel@lnu.se (primarily) or welf.lowe@lnu.se.

You are not required to use the clone detection in order to obtain a notion fo source code density. In order to obtain a rough notion of it, you may use `git-tools` which will extract a ratio of net-lines to gross-lines as density.
The clone detection used in `git-density`, however, also computes a string similarity which will yield a most-precise approximation of the source code density.

As for `git-metrics`, the application relies on another tool that supports currently obtaining software metrics from Java applications.
Metrics are obtained by building the application (for each commit).
Please contact me if you intend to use Git Metrics and require the tool. The tool is free for academic use.


# Structure of the applications
Git Density is a solution that currently features these three applications:
* __`git-density`__: A new metric to detect the density of software projects.
  * When running `git-density` on a repository, it will compute the density metric __as well as__ `git-hours` and also attempt to obtain the project's metrics at each commit using `git-metrics`.
  * Since the data produced by `git-density` is exhaustive and not plain, it must use a relational database as backend and does not support (yet) the output to file/stdout. All of its results are stored in the database for each repository.
  * It is possible to remove all previous analysis results for one repository (please refer to the command-line help).
* __`git-hours`__: A C# reimplementation of git-hours with some more features (like timespans between commits or time spent by each developer)
  * It comes also with its own command-line interface and supports `JSON`-formatted output. This useful for just analyzing the time spent on a repository.
  * `git-hours` is also part of the full analysis as run by `git-density`.
* __`git-metrics`__:  A C# wrapper around another tool that can build Java-based projects and extract common software metrics at each commit for the entire project and for files affected by the commit.
  * It comes also with its own command-line interface and supports `JSON`-formatted output (like `git-hours`).
  * It is part of the full analysis of `git-density` as well.
  * Please note that the standalone CLI interface is _not yet fully implemented_, although just minor things are missing (planned is a `JSON`-formatted output).
*	__`git-tools`__: A stand-alone application that uses some of the tools from the other projects to extract information from git repositories and stores them as __`CSV`__-files.
	*	Has its own command-line interface and supports online/offline repos and parallelization.
	*	Supports two methods currently: _Simple_ and _Extended_ (default) extraction.
	*	Does not require tools for clone-detection or metrics, as these are not extracted.
	*	Extracts __58__ features (__13__ features + counts for __20__ keywords (see [5]) in _Simple_-mode): `"SHA1", "RepoPathOrUrl", "AuthorName", "CommitterName", "AuthorTime", "CommitterTime", "Message", "AuthorEmail", "CommitterEmail", "IsInitialCommit", "IsMergeCommit", "NumberOfParentCommits", "ParentCommitSHA1s"` __plus 25 in extended:__ `"MinutesSincePreviousCommit", "AuthorNominalLabel", "CommitterNominalLabel", "NumberOfFilesAdded", "NumberOfFilesAddedNet", "NumberOfLinesAddedByAddedFiles", "NumberOfLinesAddedByAddedFilesNet", "NumberOfFilesDeleted", "NumberOfFilesDeletedNet", "NumberOfLinesDeletedByDeletedFiles", "NumberOfLinesDeletedByDeletedFilesNet", "NumberOfFilesModified", "NumberOfFilesModifiedNet", "NumberOfFilesRenamed", "NumberOfFilesRenamedNet", "NumberOfLinesAddedByModifiedFiles", "NumberOfLinesAddedByModifiedFilesNet", "NumberOfLinesDeletedByModifiedFiles", "NumberOfLinesDeletedByModifiedFilesNet", "NumberOfLinesAddedByRenamedFiles", "NumberOfLinesAddedByRenamedFilesNet", "NumberOfLinesDeletedByRenamedFiles", "NumberOfLinesDeletedByRenamedFilesNet", "Density", "AffectedFilesRatioNet"`
* **New in v2024.11**: __`git-tools`__ can now export also source code! This may not be as trivial as it sounds.
  * Export source code into CSV or JSON.
  * Export on varying level of granularity: Commits, Files, Hunks, Blocks, or Lines.
  * Depending on the level of granularity, export up to **`30`** additional details: `"SHA1","SHA1_Parent","Message","AuthorName","AuthorEmail","AuthorTime","CommitterName","CommitterEmail","CommitterTime","IsInitialCommit","IsMergeCommit","NumberOfParentCommits","DaysSinceParentCommit","TreeChangeIntent","FileIdx","FileName","HunkIdx","HunkLineNumbersAdded","HunkLineNumbersDeleted","HunkOldLineStart","HunkOldNumberOfLines","HunkNewLineStart","HunkNewNumberOfLines","BlockNature","BlockIdx","BlockLineNumbersDeleted","BlockLineNumbersAdded","BlockLineNumbersUntouched","LineType","LineNumber"`
  * These features can be used to completely restore the patches and modified files.
  * While commits, files, hunks, and lines are self-explanatory, blocks are groups of consectuive and contiguous lines. A block has a nature of added/deleted/replaced or context (untouched lines). The nature is determined by the lines, since it can have zero or more deleted lines, followed by zero or more added lines. A new block starts when switching from/to context.

All applications can be run standalone, but may also be included as references, as they all feature a public API.


## About Databases

You may also use other types of databases, as Git Density supports these: `MsSQL2000`, `MsSQL2005`, `MsSQL2008`, `MsSQL2012`, `MySQL`, `Oracle10`, `Oracle9`, `PgSQL81`, `PgSQL82`, `SQLite`, `SQLiteTemp` (temporary database that is discarded after the analysis, mainly for testing).

___


# Citing
Please use the following BibTeX to cite __`GitDensity`__:

<pre>
@article{honel2020gitdensity,
  title={Git Density (2024.11): Analyze git repositories to extract the Source Code Density and other Commit Properties},
  DOI={10.5281/zenodo.2565238},
  url={https://doi.org/10.5281/zenodo.2565238},
  publisher={Zenodo},
  author={Sebastian Hönel},
  year={2024},
  month={Nov},
  abstractNote={Git Density (<code>git-density</code>) is a tool to analyze <code>git</code>-repositories with the goal of detecting the source code density. It was developed during the research phase of the short technical paper and poster &quot;<em>A changeset-based approach to assess source code density and developer efficacy</em>&quot; and has since been extended to support extended analyses.},
}
</pre>

___

# References

[1] Hönel, S., Ericsson, M., Löwe, W. and Wingkvist, A., 2018, May. A changeset-based approach to assess source code density and developer efficacy. In _Proceedings of the 40th International Conference on Software Engineering: Companion Proceedings_ (pp. 220-221). ACM, https://www.icse2018.org/event/icse-2018-posters-poster-a-changeset-based-approach-to-assess-source-code-density-and-developer-efficacy

[2] Git hours. "Estimate time spent on a Git repository." https://github.com/kimmobrunfeldt/git-hours

[3] QTools Clone Detection. http://qtools.se/

[4] Hönel, S., Ericsson, M., Löwe, W. and Wingkvist, A., 2019. Importance and Aptitude of Source code Density for Commit Classification into Maintenance Activities. In The 19th IEEE International Conference on Software Quality, Reliability, and Security.

[5] Levin, S. and Yehudai, A., 2017, November. Boosting automatic commit classification into maintenance activities by utilizing source code changes. In Proceedings of the 13th International Conference on Predictive Models and Data Analytics in Software Engineering (pp. 97-106).
