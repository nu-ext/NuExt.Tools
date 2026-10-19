$csproj = "$PSScriptRoot\..\src\NuExt.Tools.ProjectMerger\NuExt.Tools.ProjectMerger.csproj"
$Configuration = "Release"
$outDir = $PSScriptRoot

dotnet clean $csproj -c $Configuration
dotnet pack $csproj -c $Configuration -o $outDir