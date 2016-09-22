// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Migrate
{
    public partial class MigrateCommand
    {
        private readonly string _templateFile;
        private readonly string _outputDirectory;
        private readonly string _projectJson;
        private readonly string _sdkVersion;
        private readonly bool _skipProjectReferences;

        private readonly TemporaryDotnetNewTemplateProject _temporaryDotnetNewProject;

        public MigrateCommand(string templateFile, string outputDirectory, string projectJson, string sdkVersion, bool skipProjectReferences)
        {
            _templateFile = templateFile;
            _outputDirectory = outputDirectory;
            _projectJson = projectJson;
            _sdkVersion = sdkVersion;
            _skipProjectReferences = skipProjectReferences;
            _temporaryDotnetNewProject = new TemporaryDotnetNewTemplateProject();
        }

        public int Execute()
        {
            var project = GetProjectJsonPath(_projectJson) ?? GetProjectJsonPath(Directory.GetCurrentDirectory());
            EnsureNotNull(project, "Unable to find project.json");
            var projectDirectory = Path.GetDirectoryName(project);

            var msBuildTemplate = _templateFile != null ?
                ProjectRootElement.TryOpen(_templateFile) : _temporaryDotnetNewProject.MSBuildProject;

            var outputDirectory = _outputDirectory ?? projectDirectory;
            EnsureNotNull(outputDirectory, "Null output directory");

            var sdkVersion = _sdkVersion ?? new ProjectJsonParser(_temporaryDotnetNewProject.ProjectJson).SdkPackageVersion;
            EnsureNotNull(sdkVersion, "Null Sdk Version");

            var migrationSettings = new MigrationSettings(projectDirectory, outputDirectory, sdkVersion, msBuildTemplate);
            new ProjectMigrator().Migrate(migrationSettings, _skipProjectReferences);

            return 0;
        }

        private void EnsureNotNull(string variable, string message)
        {
            if (variable == null)
            {
                throw new Exception(message);
            }
        }

        private string GetProjectJsonPath(string projectJson)
        {
            if (projectJson == null)
            {
                return null;
            }

            projectJson = ProjectPathHelper.NormalizeProjectFilePath(projectJson);

            if (File.Exists(projectJson))
            {
                return projectJson;
            }

            throw new Exception($"Unable to find project file at {projectJson}");
        }
    }
}
