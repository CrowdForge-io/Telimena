﻿param ([string]$TargetPath, [string]$SolutionDir)

Write-Host "Copy to zip starting..."
$fileName = [System.IO.Path]::GetFileName($TargetPath)

$Assembly = [Reflection.Assembly]::Loadfile($TargetPath)
$AssemblyName = $Assembly.GetName()
$Assemblyversion =  $AssemblyName.version


Write-Host "App Path: "  $TargetPath "   FileName: $fileName, Version: $Assemblyversion" 
Write-Host


$testAppsFolder = "$SolutionDir\Telimena.WebApp.UITests\02. IntegrationTests\Apps"
$zipPath1 = "$testAppsFolder\PackageTriggerUpdaterTestApp v.1.0.0.0.zip"
$zipPath2 = "$testAppsFolder\PackageTriggerUpdaterTestApp v.2.0.0.0.zip"

if ($Assemblyversion.ToString().StartsWith("1.")){
	$zipPath = $zipPath1
} else {
	$zipPath = $zipPath2
}



	try { 
	[Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem') | Out-Null
	$zip = [System.IO.Compression.ZipFile]::Open($zipPath, "Update")

	$Entry = $zip.GetEntry($fileName)
	if ($Entry)
	{
		Write-Host "File exists in  $zipPath, it will be deleted and overwritten"
		$Entry.Delete()  
	} 
	 
	[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $TargetPath, $fileName,"Optimal") | Out-Null
	$zip.Dispose()
		Write-Host "Successfully added $fileName to $zipPath"
	} catch {
		Write-Warning "Failed to add $fileName to $zipPath. Details : $_"
	} 

	Write-Host ""