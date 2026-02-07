$config = @(
    @{
        fbx     = "c:\Users\KyleB\Documents\foreplay--arch-hurry\Assets\Monsters\Vampire\Vampire_Ground.fbx"
        matPath = "res://Assets/Monsters/Vampire/Materials/"
    },
    @{
        fbx     = "c:\Users\KyleB\Documents\foreplay--arch-hurry\Assets\Monsters\Vampire\Vampire_Flying.fbx"
        matPath = "res://Assets/Monsters/Vampire/Materials/"
    },
    @{
        fbx     = "c:\Users\KyleB\Documents\foreplay--arch-hurry\Assets\Monsters\Crawler\Crawler_Mesh.fbx"
        matPath = "res://Assets/Monsters/Crawler/Materials/"
    }
)

foreach ($item in $config) {
    $fbxPath = $item.fbx
    $importPath = $fbxPath + ".import"
    $sourceRes = "res://" + $fbxPath.Replace("c:\Users\KyleB\Documents\foreplay--arch-hurry\", "").Replace("\", "/")
    
    $content = @"
[remap]

importer="scene"
importer_version=1
type="PackedScene"

[deps]

source_file="$sourceRes"

[params]

nodes/root_type=""
nodes/root_name=""
nodes/root_script=null
nodes/apply_root_scale=true
nodes/root_scale=1.0
nodes/import_as_skeleton_bones=false
nodes/use_name_suffixes=true
nodes/use_node_type_suffixes=true
meshes/ensure_tangents=true
meshes/generate_lods=true
meshes/create_shadow_meshes=true
meshes/light_baking=1
meshes/lightmap_texel_size=0.2
meshes/force_disable_compression=false
skins/use_named_skins=true
animation/import=true
animation/fps=30
animation/trimming=true
animation/remove_immutable_tracks=true
animation/import_rest_as_RESET=false
import_script/path=""
materials/extract=1
materials/extract_format=0
materials/extract_path="$($item.matPath)"
_subresources={}
fbx/importer=0
fbx/allow_geometry_helper_nodes=false
fbx/embedded_image_handling=1
fbx/naming_version=2
"@
    Set-Content -Path $importPath -Value $content
    Write-Host "Generated $importPath"
}
