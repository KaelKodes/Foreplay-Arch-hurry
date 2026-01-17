$filePath = "c:\Users\KyleB\Documents\foreplay--arch-hurry\Scenes\Entities\Player.tscn"
$tempPath = "$filePath.tmp"
$targetIds = @(
    "Animation_pid1y", # standing idle 01
    "Animation_yheof", # standing run forward
    "Animation_rbvjg", # standing walk forward
    "Animation_hgdn8", # standing run back
    "Animation_ejhnn", # standing run left
    "Animation_cd6b6", # standing run right
    "Animation_abqr1", # standing walk back
    "Animation_bydt1", # standing walk left
    "Animation_okpjm"  # standing walk right
)

$reader = [System.IO.File]::OpenText($filePath)
$writer = [System.IO.File]::CreateText($tempPath)

$currentId = $null
$loopAdded = $false

while ($line = $reader.ReadLine()) {
    $writer.WriteLine($line)
    
    # Check if we just started a target animation block
    foreach ($id in $targetIds) {
        if ($line -match "\[sub_resource type=`"Animation`" id=`"$id`"\]") {
            $currentId = $id
            $loopAdded = $false
            break
        }
    }
    
    # If we are in a target block, inject loop_mode after resource_name
    if ($currentId -and -not $loopAdded -and $line -match "resource_name = ") {
        $writer.WriteLine("loop_mode = 1")
        $loopAdded = $true
        $currentId = $null # Reset until next header
    }
    
    # Safety reset: if we hit another resource header without adding loop, clear currentId
    if ($line -match "^\[sub_resource" -and -not ($line -match $currentId)) {
        $currentId = $null
    }
}

$reader.Close()
$writer.Close()

Move-Item -Path $tempPath -Destination $filePath -Force
Write-Host "Animation looping enabled for target movement animations."
