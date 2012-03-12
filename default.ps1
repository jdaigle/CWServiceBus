properties {
	$ProductVersion = "3.0"
	$BuildNumber = "0";
	$PatchVersion = "0"
	$PreRelease = "-build"	
	$PackageNameSuffix = ""
	$TargetFramework = "net-4.0"
	$UploadPackage = $false;
	$PackageIds = ""
	$DownloadDependentPackages = $false
	
}

$baseDir  = resolve-path .
$buildBase = "$baseDir\build\"
$slnFile = "$baseDir\CWServiceBus.sln"
$toolsDir = "tools"
$nunitexec = "tools\nunit\nunit-console.exe"
$script:nunitTargetFramework = "/framework=4.0";

include $toolsDir\psake\buildutils.ps1

task default -depends ReleaseNServiceBus
 
task Clean{
	if(Test-Path $buildBase){
		Delete-Directory $buildBase		
	}
}

task InitEnvironment{

	if($script:isEnvironmentInitialized -ne $true){
		if ($TargetFramework -eq "net-4.0"){
			$netfxInstallroot ="" 
			$netfxInstallroot =	Get-RegistryValue 'HKLM:\SOFTWARE\Microsoft\.NETFramework\' 'InstallRoot' 			
			$netfxCurrent = $netfxInstallroot + "v4.0.30319"			
			$script:msBuild = $netfxCurrent + "\msbuild.exe"			
			echo ".Net 4.0 build requested - $script:msBuild" 
			$script:ilmergeTargetFramework  = "/targetplatform:v4," + $netfxCurrent			
			$script:msBuildTargetFramework ="/p:TargetFrameworkVersion=v4.0 /ToolsVersion:4.0"			
			$script:nunitTargetFramework = "/framework=4.0";			
			$script:isEnvironmentInitialized = $true
		}
	
	}
}

task Init -depends InitEnvironment, Clean {
   	
	echo "Creating build directory at the follwing path $buildBase"
	Delete-Directory $buildBase
	Create-Directory $buildBase
	
	$currentDirectory = Resolve-Path .
	
	echo "Current Directory: $currentDirectory" 
 }
  
task Compile -depends Init -description "A build script CompileMain " { 
    exec { &$script:msBuild $slnFile /p:Configuration=Release /p:OutDir="$buildBase\" }	    
}

task Tests -depends Compile {
    exec {&$nunitexec $buildBase\CWServiceBus.UnitTests.dll $script:nunitTargetFramework} 
}

task Build -depends Compile, Tests {

}