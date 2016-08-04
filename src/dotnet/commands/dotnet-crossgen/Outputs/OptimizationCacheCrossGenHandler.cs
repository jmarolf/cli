// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.CrossGen.Exceptions;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.DotNet.Tools.CrossGen.Outputs
{
    public class OptimizationCacheCrossGenHandler : CrossGenHandler
    {
        // looks like the hash value "{Algorithm}-{value}" should be handled as an opaque string
        private const string Sha512PropertyName = "sha512";
        private readonly string _archName;
        private readonly bool _overwriteOnConflict;

        public OptimizationCacheCrossGenHandler(
            string crossGenExe,
            string diaSymReaderDll,
            CrossGenTarget crossGenTarget,
            DependencyContext depsFileContext,
            DependencyContext runtimeContext,
            string appDir,
            string outputDir,
            bool generatePDB,
            bool overwriteOnConflict)
            : base(crossGenExe, diaSymReaderDll, crossGenTarget, depsFileContext, runtimeContext, appDir, outputDir, generatePDB)
        {
            _archName = crossGenTarget.RID.Split(new char[]{'-'}).Last();
            _overwriteOnConflict = overwriteOnConflict;
        }

        protected override string GetOutputDirFor(string sourcePathUsed, RuntimeLibrary lib, string assetPath)
        {
            var libRoot = GetLibRoot(lib);
            var targetLocation = Path.Combine(libRoot, assetPath);
            return Path.GetDirectoryName(targetLocation);
        }

        protected override bool ShouldCrossGenLib(RuntimeLibrary lib)
        {
            return lib.Serviceable;
        }

        protected override void OnCrossGenCompletedFor(RuntimeLibrary lib)
        {
            var sha = GetShaValueToWrite(lib);
            if (sha != null)
            {
                var shaLocation = GetShaLocation(lib);
                File.WriteAllText(shaLocation, sha);
            }
        }

        private string GetShaLocation(RuntimeLibrary lib)
        {
            var libRoot = GetLibRoot(lib);
            return Path.Combine(libRoot, $"{lib.Name}.{lib.Version}.nupkg.sha512");
        }

        private string GetShaValueToWrite(RuntimeLibrary lib)
        {
            var libHashString = lib.Hash;
            if (!libHashString.StartsWith($"{Sha512PropertyName}-"))
            {
                throw new CrossGenException($"Unsupported Hash value for package {lib.Name}.{lib.Version}, value: {libHashString}");
            }
            var newShaValue = libHashString.Substring(Sha512PropertyName.Length + 1);

            var targetLibShaFile = GetShaLocation(lib);
            
            if (!File.Exists(targetLibShaFile) || ShouldOverwrite(lib, targetLibShaFile, newShaValue))
            {
                // We don't have to write until we need to
                return newShaValue;
            }

            return null;
        }

        private bool ShouldOverwrite(RuntimeLibrary lib, string targetLibShaFile, string newShaValue)
        {
            var oldShaValue = File.ReadAllText(targetLibShaFile);
            if (oldShaValue == newShaValue)
            {
                return false;
            }
            else if (_overwriteOnConflict)
            {
                Reporter.Output.WriteLine($"[INFO] Hash mismatch found for {lib.Name}.{lib.Version}. Overwriting existing hash file. This might causes cache misses for other applications.");
                return true;
            }
            else
            {
                throw new CrossGenException($"Hash mismatch found for {lib.Name}.{lib.Version}.");
            }
        }

        private string GetLibRoot(RuntimeLibrary lib)
        {
            return Path.Combine(OutputRoot, _archName, lib.Name, lib.Version);
        }
    }
}