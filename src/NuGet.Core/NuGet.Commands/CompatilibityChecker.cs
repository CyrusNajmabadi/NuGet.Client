﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;

namespace NuGet.Commands
{
    internal class CompatibilityChecker
    {
        private readonly NuGetv3LocalRepository _localRepository;
        private readonly LockFile _lockFile;
        private readonly ILogger _log;

        public CompatibilityChecker(NuGetv3LocalRepository localRepository, LockFile lockFile, ILogger log)
        {
            _localRepository = localRepository;
            _lockFile = lockFile;
            _log = log;
        }

        internal CompatibilityCheckResult Check(RestoreTargetGraph graph)
        {
            // The Compatibility Check is designed to alert the user to cases where packages are not behaving as they would
            // expect, due to compatibility issues.
            //
            // During this check, we scan all packages for a given restore graph and check the following conditions
            // (using an example TxM 'foo' and an example Runtime ID 'bar'):
            //
            // * If any package provides a "ref/foo/Thingy.dll", there MUST be a matching "lib/foo/Thingy.dll" or
            //   "runtimes/bar/lib/foo/Thingy.dll" provided by a package in the graph.
            // * All packages that contain Managed Assemblies must provide assemblies for 'foo'. If a package
            //   contains any of 'ref/' folders, 'lib/' folders, or framework assemblies, it must provide at least
            //   one of those for the 'foo' framework. Otherwise, the package is intending to provide managed assemblies
            //   but it does not support the target platform. If a package contains only 'content/', 'build/', 'tools/' or
            //   other NuGet convention folders, it is exempt from this check. Thus, content-only packages are always considered
            //   compatible, regardless of if they actually provide useful content.
            //
            // It is up to callers to invoke the compatibility check on the graphs they wish to check, but the general behavior in
            // the restore command is to invoke a compatibility check for each of:
            //
            // * The Targets (TxMs) defined in the project.json, with no Runtimes
            // * All combinations of TxMs and Runtimes defined in the project.json
            // * Additional (TxMs, Runtime) pairs defined by the "supports" mechanism in project.json

            var runtimeAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var compileAssemblies = new Dictionary<string, LibraryIdentity>(StringComparer.OrdinalIgnoreCase);
            var issues = new List<CompatibilityIssue>();
            foreach (var node in graph.Flattened)
            {
                _log.LogDebug(string.Format(CultureInfo.CurrentCulture, Strings.Log_CheckingPackageCompatibility, node.Key.Name, node.Key.Version, graph.Name));
                var compatibilityData = GetCompatibilityData(graph, node.Key);
                if (compatibilityData == null)
                {
                    continue;
                }

                if (!IsCompatible(compatibilityData))
                {
                    var issue = CompatibilityIssue.Incompatible(
                        new PackageIdentity(node.Key.Name, node.Key.Version),
                        graph.Framework,
                        graph.RuntimeIdentifier);
                    issues.Add(issue);
                    _log.LogError(issue.Format());
                }

                // Check for matching ref/libs if we're checking a runtime-specific graph
                var targetLibrary = compatibilityData.TargetLibrary;
                if (!string.IsNullOrEmpty(graph.RuntimeIdentifier))
                {
                    // Scan the package for ref assemblies
                    foreach (var compile in targetLibrary.CompileTimeAssemblies.Where(p => Path.GetExtension(p.Path).Equals(".dll", StringComparison.OrdinalIgnoreCase)))
                    {
                        string name = Path.GetFileNameWithoutExtension(compile.Path);

                        // If we haven't already started tracking this compile-time assembly, AND there isn't already a runtime-loadable version
                        if (!compileAssemblies.ContainsKey(name) && !runtimeAssemblies.Contains(name))
                        {
                            // Track this assembly as potentially compile-time-only
                            compileAssemblies.Add(name, node.Key);
                        }
                    }

                    // Match up runtime assemblies
                    foreach (var runtime in targetLibrary.RuntimeAssemblies.Where(p => Path.GetExtension(p.Path).Equals(".dll", StringComparison.OrdinalIgnoreCase)))
                    {
                        string name = Path.GetFileNameWithoutExtension(runtime.Path);

                        // Fix for NuGet/Home#752 - Consider ".ni.dll" (native image/ngen) files matches for ref/ assemblies
                        if (name.EndsWith(".ni"))
                        {
                            name = name.Substring(0, name.Length - 3);
                        }

                        // If there was a compile-time-only assembly under this name...
                        if (compileAssemblies.ContainsKey(name))
                        {
                            // Remove it, we've found a matching runtime ref
                            compileAssemblies.Remove(name);
                        }

                        // Track this assembly as having a runtime assembly
                        runtimeAssemblies.Add(name);
                    }
                }
            }

            // Generate errors for un-matched reference assemblies, if we're checking a runtime-specific graph
            if (!string.IsNullOrEmpty(graph.RuntimeIdentifier))
            {
                foreach (var compile in compileAssemblies)
                {
                    var issue = CompatibilityIssue.ReferenceAssemblyNotImplemented(
                        compile.Key,
                        new PackageIdentity(compile.Value.Name, compile.Value.Version),
                        graph.Framework,
                        graph.RuntimeIdentifier);
                    issues.Add(issue);
                    _log.LogError(issue.Format());
                }
            }

            return new CompatibilityCheckResult(graph, issues);
        }

        private bool IsCompatible(CompatibilityData compatibilityData)
        {
            // A package is compatible if it has...
            return
                compatibilityData.TargetLibrary.RuntimeAssemblies.Count > 0 ||                          // Runtime Assemblies, or
                compatibilityData.TargetLibrary.CompileTimeAssemblies.Count > 0 ||                      // Compile-time Assemblies, or
                compatibilityData.TargetLibrary.FrameworkAssemblies.Count > 0 ||                        // Framework Assemblies, or
                compatibilityData.TargetLibrary.ContentFiles.Count > 0 ||                               // Shared content
                compatibilityData.TargetLibrary.ResourceAssemblies.Count > 0 ||                         // Resources (satellite package)
                !compatibilityData.Files.Any(p => 
                    p.StartsWith("ref/", StringComparison.OrdinalIgnoreCase) 
                    || p.StartsWith("lib/", StringComparison.OrdinalIgnoreCase));                       // No assemblies at all (for any TxM)
        }

        private CompatibilityData GetCompatibilityData(RestoreTargetGraph graph, LibraryIdentity libraryId)
        {
            LockFileTargetLibrary targetLibrary = null;
            var target = _lockFile.Targets.FirstOrDefault(t => Equals(t.TargetFramework, graph.Framework) && string.Equals(t.RuntimeIdentifier, graph.RuntimeIdentifier, StringComparison.Ordinal));
            if (target != null)
            {
                targetLibrary = target.Libraries.FirstOrDefault(t => t.Name.Equals(libraryId.Name) && t.Version.Equals(libraryId.Version));
            }

            IEnumerable<string> files = null;
            var lockFileLibrary = _lockFile.Libraries.FirstOrDefault(l => l.Name.Equals(libraryId.Name) && l.Version.Equals(libraryId.Version));
            if (lockFileLibrary != null)
            {
                files = lockFileLibrary.Files;
            }

            if (files != null && targetLibrary != null)
            {
                // Everything we need is in the lock file!
                return new CompatibilityData(lockFileLibrary.Files, targetLibrary);
            }
            else
            {
                // We need to generate some of the data. We'll need the local packge info to do that
                var package = _localRepository.FindPackagesById(libraryId.Name)
                    .FirstOrDefault(p => p.Version.Equals(libraryId.Version));
                if (package == null)
                {
                    return null;
                }

                // Collect the file list if necessary
                if (files == null)
                {
                    using (var packageReader = new PackageArchiveReader(package.ZipPath))
                    {
                        if (Path.DirectorySeparatorChar != '/')
                        {
                            files = packageReader
                                    .GetFiles()
                                    .Select(p => p.Replace(Path.DirectorySeparatorChar, '/'))
                                    .ToList();
                        }
                        else
                        {
                            files = packageReader.GetFiles().ToList();
                        }
                    }
                }

                // Generate the target library if necessary
                if (targetLibrary == null)
                {
                    targetLibrary = LockFileUtils.CreateLockFileTargetLibrary(
                        library: null,
                        package: package,
                        targetGraph: graph,
                        defaultPackagePathResolver: new VersionFolderPathResolver(_localRepository.RepositoryRoot),
                        correctedPackageName: libraryId.Name,
                        dependencyType: LibraryIncludeFlags.All);
                }

                return new CompatibilityData(files, targetLibrary);
            }
        }

        private class CompatibilityData
        {
            public IEnumerable<string> Files { get; }
            public LockFileTargetLibrary TargetLibrary { get; }

            public CompatibilityData(IEnumerable<string> files, LockFileTargetLibrary targetLibrary)
            {
                Files = files;
                TargetLibrary = targetLibrary;
            }
        }
    }
}
