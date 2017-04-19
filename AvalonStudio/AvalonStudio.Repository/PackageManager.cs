﻿using AvalonStudio.Platforms;
using AvalonStudio.Repositories;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AvalonStudio.Packages
{
    public class PackageManager
    {
        private ILogger _logger;

        public PackageManager(ILogger logger = null)
        {
            _logger = logger;

            if (logger == null)
            {
                _logger = new ConsoleNuGetLogger();
            }
        }

        private const string DefaultPackageSource = "https://www.myget.org/F/avalonstudio/api/v3/index.json";

        public static NuGetFramework GetFramework()
        {
            return new NuGetFramework("AvalonStudio1.0");
        }

        public static InstalledPackagesCache GetCache()
        {
            return new InstalledPackagesCache(Path.Combine(Platform.ReposDirectory, "cachedPackages.xml"), Path.Combine(Platform.ReposDirectory, "installedPackages.xml"), false);
        }

        public async Task InstallPackage(string packageId, string version)
        {
            PackageIdentity identity = new PackageIdentity(packageId, new NuGet.Versioning.NuGetVersion(version));

            List<Lazy<INuGetResourceProvider>> providers = new List<Lazy<INuGetResourceProvider>>();

            providers.AddRange(Repository.Provider.GetCoreV3());  // Add v3 API support
            providers.AddRange(Repository.Provider.GetCoreV2());  // Add v2 API support

            var settings = (NuGet.Configuration.Settings)NuGet.Configuration.Settings.LoadDefaultSettings(Platform.ReposDirectory, null, new MachineWideSettings(), false, true);

            ISourceRepositoryProvider sourceRepositoryProvider = new SourceRepositoryProvider(settings, providers);  // See part 2

            using (var installedPackageCache = GetCache())
            {
                var project = new AvalonStudioExtensionsFolderProject(new NuGet.PhysicalFileSystem(Platform.ReposDirectory), GetFramework(), installedPackageCache, Platform.ReposDirectory);

                if (!project.PackageExists(identity))
                {
                    var packageManager = new NuGetPackageManager(sourceRepositoryProvider, settings, Platform.ReposDirectory)
                    {
                        PackagesFolderNuGetProject = project,
                    };

                    bool allowPrereleaseVersions = true;
                    bool allowUnlisted = false;

                    ResolutionContext resolutionContext = new ResolutionContext(
                        DependencyBehavior.Lowest, allowPrereleaseVersions, allowUnlisted, VersionConstraints.None);

                    INuGetProjectContext projectContext = new ProjectContext(_logger);
                    var sourceRepositories = new List<SourceRepository>();
                    sourceRepositories.Add(new SourceRepository(new NuGet.Configuration.PackageSource(DefaultPackageSource), providers));

                    await packageManager.InstallPackageAsync(packageManager.PackagesFolderNuGetProject,
                        identity, resolutionContext, projectContext, sourceRepositories,
                        Array.Empty<SourceRepository>(),  // This is a list of secondary source respositories, probably empty
                        CancellationToken.None);
                }
                else
                {
                    _logger.LogInformation("Package is already installed.");
                }
            }
        }

        public async Task UninstallPackage(string packageId, string version)
        {
            PackageIdentity identity = new PackageIdentity(packageId, new NuGet.Versioning.NuGetVersion(version));

            List<Lazy<INuGetResourceProvider>> providers = new List<Lazy<INuGetResourceProvider>>();

            providers.AddRange(Repository.Provider.GetCoreV3());  // Add v3 API support
            providers.AddRange(Repository.Provider.GetCoreV2());  // Add v2 API support

            var settings = (NuGet.Configuration.Settings)NuGet.Configuration.Settings.LoadDefaultSettings(Platform.ReposDirectory, null, new MachineWideSettings(), false, true);

            ISourceRepositoryProvider sourceRepositoryProvider = new SourceRepositoryProvider(settings, providers);  // See part 2

            using (var installedPackageCache = GetCache())
            {
                var project = new AvalonStudioExtensionsFolderProject(new NuGet.PhysicalFileSystem(Platform.ReposDirectory), GetFramework(), installedPackageCache, Platform.ReposDirectory);

                var packageManager = new NuGetPackageManager(sourceRepositoryProvider, settings, new SolutionManager(), new DeleteOnRestartManager())
                {
                    PackagesFolderNuGetProject = project,
                };

                INuGetProjectContext projectContext = new ProjectContext(_logger);

                var uninstallationContext = new UninstallationContext(true, true);

                await packageManager.UninstallPackageAsync(project, packageId, uninstallationContext, projectContext, CancellationToken.None);
            }
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> ListPackages(int max = 20)
        {
            List<Lazy<INuGetResourceProvider>> providers = new List<Lazy<INuGetResourceProvider>>();
            providers.AddRange(Repository.Provider.GetCoreV3());  // Add v3 API support
            providers.AddRange(Repository.Provider.GetCoreV2());  // Add v2 API support
            PackageSource packageSource = new PackageSource(DefaultPackageSource);
            SourceRepository sourceRepository = new SourceRepository(packageSource, providers);

            var packageMetadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>();

            var prov = new V3FeedListResourceProvider();
            var feed = await prov.TryCreate(sourceRepository, CancellationToken.None);
            var lister = (V2FeedListResource)feed.Item2;

            var results = await lister.ListAsync(string.Empty, true, true, false, _logger, CancellationToken.None);

            var enumerator = results.GetEnumeratorAsync();

            var result = new List<IPackageSearchMetadata>();

            while (max > 0)
            {
                await enumerator.MoveNextAsync();

                if (enumerator.Current == null)
                {
                    break;
                }

                result.Add(enumerator.Current);

                max--;
            }

            return result;
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> FindPackages(string packageName)
        {
            List<Lazy<INuGetResourceProvider>> providers = new List<Lazy<INuGetResourceProvider>>();
            providers.AddRange(Repository.Provider.GetCoreV3());  // Add v3 API support
            providers.AddRange(Repository.Provider.GetCoreV2());  // Add v2 API support
            PackageSource packageSource = new PackageSource(DefaultPackageSource);
            SourceRepository sourceRepository = new SourceRepository(packageSource, providers);

            var packageMetadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>();

            var searchResource = await sourceRepository.GetResourceAsync<PackageSearchResource>();

            return await searchResource.SearchAsync(packageName, new SearchFilter(true), 0, 10, _logger, CancellationToken.None);
        }

        public IEnumerable<PackageIdentity> ListInstalledPackages()
        {
            using (var installedPackageCache = GetCache())
            {
                return installedPackageCache.GetInstalledPackagesAndDependencies();
            }
        }

        private async Task<NuGetVersion> GetLatestMatchingVersion(string packageId, NuGetFramework currentFramework, VersionRange versionRange, SourceRepository sourceRepository, ILogger logger)
        {
            try
            {
                DependencyInfoResource dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                IEnumerable<SourcePackageDependencyInfo> dependencyInfo = await dependencyInfoResource.ResolvePackages(
                    packageId, currentFramework, logger, CancellationToken.None);
                return dependencyInfo
                    .Select(x => x.Version)
                    .Where(x => x != null && (versionRange == null || versionRange.Satisfies(x)))
                    .DefaultIfEmpty()
                    .Max();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Could not get latest version for package {packageId} from source {sourceRepository}: {ex.Message}");
                return null;
            }
        }
    }
}