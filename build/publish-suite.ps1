param(
    [parameter(Mandatory = $true)]$basePath,
    [parameter(Mandatory = $true)]$platform,
    [parameter(Mandatory = $true)]$environment,
    $configuration = 'Release',
    $samples = $false)

$sourcePath = Join-Path -Path $basePath -ChildPath '/src' -Resolve
$samplePath = Join-Path -Path $basePath -ChildPath '/samples' -Resolve
$publishDirectory = Join-Path -Path $basePath -ChildPath "/publish-$platform"
$moduleDirectory = Join-Path -Path $publishDirectory -ChildPath "/Modules"

$scriptsDirectory = Join-Path -Path $sourcePath -ChildPath "/Sdk.Deployment/Scripts"
$publishScript = Join-Path -Path $scriptsDirectory -ChildPath "/publish-module.ps1" -Resolve
$cleanupScript = Join-Path -Path $scriptsDirectory -ChildPath "/cleanup-module.ps1" -Resolve

# SUITE
dotnet publish "$sourcePath/Core.OS" -c Release -r $platform -o $publishDirectory --self-contained -v q --nologo

# UI HOSTS
Invoke-Expression "& `"$publishScript`" -module 'Blazor.Server' -srcPath $sourcePath -platform $platform -outputPath $moduleDirectory/Blazor.Server"

# MODULES
$modules = [System.Collections.ArrayList]@("ClusterManagement")
if ($environment -eq "ALL") {
    $modules.Add("Api")
}

foreach ($moduleId in $modules) {
    write-host "---------- $moduleId ".PadRight(45, [char]45)

    $moduleOutputPath = Join-Path -Path $moduleDirectory -ChildPath "/$moduleId"

    Invoke-Expression "& `"$publishScript`" -module $moduleId -srcPath $sourcePath -platform $platform -outputPath $moduleOutputPath"
    Invoke-Expression "& `"$cleanupScript`" -artifactPath $moduleOutputPath"
}

if ($samples) {
    # SAMPLES
    foreach ($moduleId in @("Ping", "Burger", "JiTChat")) {
        write-host "---------- $moduleId ".PadRight(45, [char]45)

        $moduleOutputPath = Join-Path -Path $moduleDirectory -ChildPath "/$moduleId"

        Invoke-Expression "& `"$publishScript`" -module $moduleId -srcPath $samplePath -platform $platform -outputPath $moduleOutputPath"
        Invoke-Expression "& `"$cleanupScript`" -artifactPath $moduleOutputPath"
    }
}