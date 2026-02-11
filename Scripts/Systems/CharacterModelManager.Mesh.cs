using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class CharacterModelManager
{
	private void SetupCustomModel(CharacterRegistry.CharacterModel model)
	{
		CleanupCustomModel();

		// 1. Hide Erika
		if (_meleeModel != null) _meleeModel.Visible = false;
		if (_archeryModel != null) _archeryModel.Visible = false;
		if (_animTree != null) _animTree.Active = false;

		// 2. Instantiate Custom Model
		var path = model.MeleeScenePath; // Use Melee for default
		if (ResourceLoader.Exists(path))
		{
			var scn = GD.Load<PackedScene>(path);
			if (scn != null)
			{
				_currentCustomModel = scn.Instantiate<Node3D>();
				_player.AddChild(_currentCustomModel);

				// CRITICAL FIX: Sanitize model to remove Physics/AI if the user loaded a full Entity scene
				SanitizeCustomModel(_currentCustomModel);

				_currentCustomModel.Transform = _meleeModel?.Transform ?? Transform3D.Identity;

				// Apply custom rig offsets
				_currentCustomModel.Position += model.PositionOffset;
				_currentCustomModel.RotationDegrees += model.RotationOffset;
				_currentCustomModel.Scale *= model.ModelScale;



				// Find AnimPlayer
				_customAnimPlayer = FindPopulatedAnimationPlayerRecursive(_currentCustomModel);
				if (_customAnimPlayer != null)
				{
					var animations = _customAnimPlayer.GetAnimationList();
					GD.Print($"[CharacterModelManager] Custom AnimPlayer: {_customAnimPlayer.GetPath()} with {animations.Length} anims.");

					// 1. Load Standard Animations (Retargeted) based on Config
					LoadRetargetedStandardAnimations(_customAnimPlayer, model);

					// 2. Map common internal names to standard ones (Alias) for any "Internal" sources
					AliasEmbeddedAnimations(_customAnimPlayer, model);

					// Re-fetch names after aliasing/loading
					animations = _customAnimPlayer.GetAnimationList();
					GD.Print($"[CharacterModelManager] Final animation count: {animations.Length}");

					_lastPlayedAnim = "";
				}
				else
				{
					GD.PrintErr($"[CharacterModelManager] NO ANIMATION PLAYER found in {path}");
				}

				// --- Remove debug dump, keep just a summary ---
				GD.Print($"[CharacterModelManager] Model {model.Id} loaded with custom skeleton");

				// Apply Mesh Configuration (Hiding/Scaling)
				ApplyMeshConfig(_currentCustomModel, model);

				// Handle Weapon Override (Theft)
				if (!string.IsNullOrEmpty(model.WeaponOverridePath))
				{
					HandleWeaponTheft(_currentCustomModel, model);
				}
			}
		}
	}

	private void HandleWeaponTheft(Node3D characterInstance, CharacterRegistry.CharacterModel model)
	{
		string weaponFbxPath = model.WeaponOverridePath;
		if (!ResourceLoader.Exists(weaponFbxPath)) return;

		var scn = GD.Load<PackedScene>(weaponFbxPath);
		if (scn == null) return;

		var weaponModel = scn.Instantiate<Node3D>();

		// 1. Hide default weapons on character
		HideBuiltinWeapons(characterInstance);

		// 2. Find the weapon mesh in the source
		MeshInstance3D meshToSteal = FindWeaponMeshRecursive(weaponModel);
		if (meshToSteal != null)
		{
			// 3. Find the hand bone on character
			var skeleton = FindSkeletonRecursive(characterInstance);
			if (skeleton != null)
			{
				// Create BoneAttachment
				var attachment = new BoneAttachment3D();
				attachment.Name = "StolenWeaponAttachment";
				skeleton.AddChild(attachment);

				// Try common hand bone names
				string boneName = "RightHand";
				if (skeleton.FindBone("mixamorig_RightHand") != -1) boneName = "mixamorig_RightHand";
				else if (skeleton.FindBone("hand.R") != -1) boneName = "hand.R";

				attachment.BoneName = boneName;

				// Move mesh to attachment
				meshToSteal.GetParent()?.RemoveChild(meshToSteal);
				meshToSteal.Owner = null; // Unset owner to prevent scene inconsistency
				attachment.AddChild(meshToSteal);
				meshToSteal.Owner = attachment;
				meshToSteal.AddToGroup("stolen_weapons");

				// Apply custom offsets
				meshToSteal.Position = model.WeaponPositionOffset;
				meshToSteal.RotationDegrees = model.WeaponRotationOffset;

				GD.Print($"[CharacterModelManager] Successfully stole weapon {meshToSteal.Name} from {weaponFbxPath} and attached to {boneName}");
			}
		}

		weaponModel.QueueFree();
	}

	private MeshInstance3D FindWeaponMeshRecursive(Node node)
	{
		if (node is MeshInstance3D mi)
		{
			string name = mi.Name.ToString().ToLower();
			if (name.Contains("sword") || name.Contains("blade") || name.Contains("weapon") || name.Contains("greatsword"))
				return mi;
		}

		foreach (Node child in node.GetChildren())
		{
			var found = FindWeaponMeshRecursive(child);
			if (found != null) return found;
		}
		return null;
	}

	private void ApplyMeshConfig(Node3D modelInstance, CharacterRegistry.CharacterModel modelData)
	{
		if (modelData.Meshes == null || modelData.Meshes.Count == 0)
		{
			// Custom-skeleton models: show ALL meshes by default (body + built-in weapons)
			// Their FBX includes everything they need
			GD.Print($"[CharacterModelManager] No mesh config — showing all meshes (per-hero model)");
			return;
		}

		foreach (var kvp in modelData.Meshes)
		{
			string meshName = kvp.Key;
			var cfg = kvp.Value;

			var meshNode = modelInstance.FindChild(meshName, true, false) as Node3D;
			if (meshNode != null)
			{
				// Apply Scale (Only if not default to avoid overriding natural model scales)
				Vector3 targetScale = new Vector3(cfg.Scale[0], cfg.Scale[1], cfg.Scale[2]);
				if (targetScale != Vector3.One)
				{
					meshNode.Scale = targetScale;
				}

				// Apply Visibility based on Category
				// Items/Body/Hidden are static. Weapons are dynamic.
				if (cfg.Category == CharacterConfig.MeshConfig.Categories.Hidden)
				{
					meshNode.Visible = false;
				}
				else if (cfg.Category == CharacterConfig.MeshConfig.Categories.Body ||
						 cfg.Category == CharacterConfig.MeshConfig.Categories.Item)
				{
					meshNode.Visible = cfg.IsVisible;
				}
				else
				{
					// Weapons: Initially hide until Mode update
					meshNode.Visible = false;
				}
			}
		}
	}

	private void CleanupCustomModel()
	{
		if (_currentCustomModel != null)
		{
			_currentCustomModel.QueueFree();
			_currentCustomModel = null;
			_customAnimPlayer = null;
		}

		// Restore Erika base state
		if (_meleeModel != null) _meleeModel.Visible = true;
		if (_archeryModel != null) _archeryModel.Visible = false;
		if (_animTree != null) _animTree.Active = true;
		_lastPlayedAnim = "";
	}

	/// <summary>
	/// Swaps the mesh on a model node by loading the new FBX and copying mesh instances.
	/// </summary>
	private void SwapModelMesh(Node3D targetModel, string fbxPath)
	{
		if (targetModel == null) return;

		if (!ResourceLoader.Exists(fbxPath))
		{
			GD.PrintErr($"[CharacterModelManager] Model FBX not found: {fbxPath}");
			return;
		}

		var fbxScene = GD.Load<PackedScene>(fbxPath);
		if (fbxScene == null)
		{
			GD.PrintErr($"[CharacterModelManager] Could not load FBX: {fbxPath}");
			return;
		}

		var newModelInstance = fbxScene.Instantiate<Node3D>();
		var targetSkeleton = targetModel.GetNodeOrNull<Skeleton3D>("Skeleton3D");
		var newSkeleton = newModelInstance.GetNodeOrNull<Skeleton3D>("Skeleton3D");

		if (targetSkeleton == null || newSkeleton == null)
		{
			GD.PrintErr("[CharacterModelManager] Could not find skeletons for mesh swap");
			newModelInstance.QueueFree();
			return;
		}

		// Remove old mesh instances from target skeleton (but keep BoneAttachments)
		foreach (var child in targetSkeleton.GetChildren())
		{
			if (child is MeshInstance3D oldMesh)
			{
				oldMesh.QueueFree();
			}
		}

		// Copy mesh instances from new skeleton to target skeleton
		foreach (var child in newSkeleton.GetChildren())
		{
			if (child is MeshInstance3D newMesh)
			{
				newMesh.Owner = null;
				newSkeleton.RemoveChild(newMesh);
				targetSkeleton.AddChild(newMesh);
				newMesh.Skeleton = new NodePath(".."); // Explicitly bind to parent skeleton
				FixMeshCulling(newMesh);
			}
		}

		newModelInstance.QueueFree();
		GD.Print($"[CharacterModelManager] Swapped mesh from: {fbxPath}");
	}

	/// <summary>
	/// Disables backface culling on all materials of a mesh to prevent "see-through" issues.
	/// </summary>
	private void FixMeshCulling(MeshInstance3D mesh)
	{
		if (mesh == null) return;

		int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
		if (surfaceCount == 0 && mesh.Mesh != null)
		{
			surfaceCount = mesh.Mesh.GetSurfaceCount();
		}

		for (int i = 0; i < surfaceCount; i++)
		{
			var mat = mesh.GetSurfaceOverrideMaterial(i) as BaseMaterial3D;
			if (mat == null && mesh.Mesh != null)
			{
				mat = mesh.Mesh.SurfaceGetMaterial(i) as BaseMaterial3D;
			}

			if (mat != null)
			{
				var uniqueMat = (BaseMaterial3D)mat.Duplicate();
				uniqueMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
				mesh.SetSurfaceOverrideMaterial(i, uniqueMat);
			}
		}
	}

	private void HideBuiltinWeapons(Node3D model)
	{
		HideNodesByKeywordsRecursive(model, new string[] {
			"sword", "weapon", "blade", "bow", "arrow",
			"knight_sword", "sword_low", "weapon_r", "hand_r_weapon"
		});
	}

	private void HideNodesByKeywordsRecursive(Node node, string[] keywords)
	{
		string lowerName = node.Name.ToString().ToLower();
		foreach (var k in keywords)
		{
			if (lowerName.Contains(k))
			{
				if (node is Node3D n3d)
				{
					n3d.Visible = false;
					GD.Print($"[CharacterModelManager] Hiding built-in part: {node.Name}");
				}
				break;
			}
		}
		foreach (Node child in node.GetChildren())
		{
			HideNodesByKeywordsRecursive(child, keywords);
		}
	}

	private Skeleton3D FindSkeletonRecursive(Node node)
	{
		if (node is Skeleton3D skel) return skel;
		foreach (Node child in node.GetChildren())
		{
			var found = FindSkeletonRecursive(child);
			if (found != null) return found;
		}
		return null;
	}

	public void LogHierarchyScales(Node node, string indent)
	{
		if (node is Node3D n3d)
		{
			GD.Print($"{indent}- {node.Name}: Scale={n3d.Scale}, GlobalScale={n3d.GlobalTransform.Basis.Scale}");
		}
		foreach (Node child in node.GetChildren())
		{
			LogHierarchyScales(child, indent + "  ");
		}
	}

	private void PrintNodeRecursive(Node node, string indent)
	{
		string extra = "";
		if (node is MeshInstance3D mi) extra = $" [Mesh: {mi.Mesh?.ResourceName}]";
		if (node is Skeleton3D skel) extra = $" [Skeleton: {skel.GetBoneCount()} bones]";
		if (node is AnimationPlayer ap) extra = $" [AnimPlayer: {ap.GetAnimationList().Length}]";

		GD.Print($"{indent}- {node.Name} ({node.GetType().Name}){extra}");

		foreach (Node child in node.GetChildren())
		{
			PrintNodeRecursive(child, indent + "  ");
		}
	}

	private void SanitizeCustomModel(Node node)
	{
		// 1. Disable Physics Collisions
		if (node is CollisionObject3D colObj)
		{
			colObj.CollisionLayer = 0;
			colObj.CollisionMask = 0;
			colObj.ProcessMode = Node.ProcessModeEnum.Disabled;
			GD.Print($"[CharacterModelManager] Disabled collision on {node.Name} (Visual Model Sanitization)");
		}

		// 2. Disable CollisionShapes
		if (node is CollisionShape3D colShape)
		{
			colShape.Disabled = true;
		}

		// 3. Remove AI/Logic Scripts
		// We can't easily check "is MobaMinion" without reflection or type checking if we don't have the reference here easily,
		// but checking the Script property or node name helps.
		if (node.GetScript().Obj is CSharpScript cs)
		{
			string scriptName = cs.ResourcePath.ToLower();
			if (scriptName.Contains("mobaminion") || scriptName.Contains("summonedskeletonai") || scriptName.Contains("monster"))
			{
				node.SetScript(new Variant());
				node.ProcessMode = Node.ProcessModeEnum.Disabled;
				GD.Print($"[CharacterModelManager] Stripped script from {node.Name} (Visual Model Sanitization)");
			}
		}

		// 4. Remove specific AI nodes by name convention
		if (node.Name.ToString().Contains("SummonedSkeletonAI") || node.Name.ToString().Contains("MobaMinion"))
		{
			node.QueueFree();
			return; // Stop recursion for this branch if we deleted it
		}

		foreach (Node child in node.GetChildren())
		{
			SanitizeCustomModel(child);
		}
	}
}
