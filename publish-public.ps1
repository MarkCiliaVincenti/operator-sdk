#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         publish-public.ps1
# CONTRIBUTOR:  NeonFORGE Team
# COPYRIGHT:    Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# Publishes RELEASE builds of the NeonForge Nuget packages to the
# local file system and public Nuget.org repositories.
#
# USAGE: pwsh -f neonsdk-nuget-public.ps1 [OPTIONS]
#
# OPTIONS:
#
#       -dirty      - Use GitHub sources for SourceLink even if local repo is dirty
#       -restore    - Just restore the CSPROJ files after cancelling publish
#
# REMARKS:
#
# NOTE: The script writes the package publication version to:
#
#           $/build/nuget/version.txt
#

param 
(
    [switch]$dirty   = $false,    # use GitHub sources for SourceLink even if local repo is dirty
    [switch]$restore = $false     # Just restore the CSPROJ files after cancelling publish
)

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Abort if Visual Studio is running because that can lead to 
# build configuration conflicts because this script builds the
# RELEASE configuration and we normally have VS in DEBUG mode.

#Ensure-VisualStudioNotRunning

# Verify that the user has the required environment variables.  These will
# be available only for maintainers and are intialized by the NEONCLOUD
# [buildenv.cmd] script.

if (!(Test-Path env:NC_ROOT))
{
    "*** ERROR: This script is intended for maintainers only:"
    "           [NC_ROOT] environment variable is not defined."
    ""
    "           Maintainers should re-run the NEONCLOUD [buildenv.cmd] script."

    return 1
}

# Retrieve any necessary credentials.

$nugetApiKey = Get-SecretPassword "NUGET_PUBLIC_KEY"

#------------------------------------------------------------------------------
# Sets the package version in the specified project file.

function SetVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$project,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$version
    )

    "$project"
	neon-build pack-version "$env:NK_ROOT\Lib\Neon.Kube\KubeVersions.cs" NeonKube "$env:NO_ROOT\src\$project\$project.csproj"
    ThrowOnExitCode
}

#------------------------------------------------------------------------------
# Builds and publishes the project packages.

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$project,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$version
    )

    $projectPath = [io.path]::combine($env:NO_ROOT, "src", "$project", "$project" + ".csproj")

    # Disabling symbol packages now that we're embedding PDB files.
    #
    # dotnet pack $projectPath -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -o "$env:NO_BUILD\nuget"

    dotnet pack $projectPath -c Release -o "$env:NO_BUILD\nuget" -p:SolutionName=$env:SolutionName -p:PackageVersion=$version
    ThrowOnExitCode

	nuget push -Source nuget.org -ApiKey $nugetApiKey "$env:NO_BUILD\nuget\$project.$version.nupkg" -SkipDuplicate -Timeout 600
    ThrowOnExitCode
}

try
{
    if ([System.String]::IsNullOrEmpty($env:SolutionName))
    {
        $env:SolutionName = "neonKUBE"
    }

    $msbuild         = $env:MSBUILDPATH
    $neonBuild       = "$env:NF_ROOT\ToolBin\neon-build\neon-build.exe"
    $config          = "Release"
    $nfRoot          = "$env:NF_ROOT"
    $nkRoot          = "$env:NK_ROOT"
    $nkSolution      = "$nkRoot\neonKUBE.sln"
    $nkBuild         = "$env:NK_BUILD"
    $nkLib           = "$nkRoot\Lib"
    $nfLib           = "$nfRoot\Lib"
    $nkTools         = "$nkRoot\Tools"
    $neonSdkVersion  = $(& "neon-build" read-version "$nfLib/Neon.Common/Build.cs" NeonSdkVersion)
    $neonkubeVersion = $(& "neon-build" read-version "$nkLib/Neon.Kube/KubeVersions.cs" NeonKube)

    #------------------------------------------------------------------------------
    # Save the publish version to [$/build/nuget/version.text] so release tools can
    # determine the current release.

    [System.IO.Directory]::CreateDirectory("$nkRoot\build\nuget") | Out-Null
    [System.IO.File]::WriteAllText("$nkRoot\build\nuget\version.txt", $neonkubeVersion)

    #--------------------------------------------------------------------------
    # SourceLink configuration:
	#
	# We're going to fail this when the current git branch is dirty 
	# and [-dirty] wasn't passed.

    $gitDirty = IsGitDirty

    if ($gitDirty -and -not $dirty)
    {
        throw "Cannot publish nugets because the git branch is dirty.  Use the [-dirty] option to override."
    }

    $env:NEON_PUBLIC_SOURCELINK = "true"

    #--------------------------------------------------------------------------
    # We need to do a release solution build to ensure that any tools or other
    # dependencies are built before we build and publish the individual packages.

    if (-not $restore)
    {
        #------------------------------------------------------------------------------
        # Build and publish the projects.

        Publish Neon.Kubernetes                $neonkubeVersion
        Publish Neon.Operator                  $neonkubeVersion
        Publish Neon.Operator.Analyzers        $neonkubeVersion
        Publish Neon.Operator.Core             $neonkubeVersion
        Publish Neon.Operator.Templates        $neonkubeVersion
        Publish Neon.Operator.Xunit            $neonkubeVersion
    }

    #------------------------------------------------------------------------------
    # Remove all of the generated nuget files so these don't accumulate.

    Remove-Item "$env:NO_BUILD\nuget\*.nupkg"

    ""
    "** Package publication completed"
    ""
}
catch
{
    Write-Exception $_
    exit 1
}
