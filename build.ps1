param(
    [Parameter(Position=0,Mandatory=0)]
    [string[]]$taskList = @(),
    [Parameter(Position=1, Mandatory=0)]
    [System.Collections.Hashtable]$properties = @{}
  )

if(($taskList -eq $null) -or ($args -eq $null)){
	$taskList = @("Build")
}
elseif($taskList.Count -le 0){
	$taskList = @("Build")
}


Import-Module .\tools\psake\psake.psm1 -ErrorAction SilentlyContinue
Invoke-psake .\default.ps1 -taskList $taskList  -properties $properties
Remove-Module psake -ErrorAction SilentlyContinue