using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LifeEngine.Editor
{
    /// <summary>
    /// Validates FBX runtime assets after Unity's native ModelImporter has imported them.
    /// Blender-side validation remains a separate required stage.
    /// </summary>
    public static class AssetPipelineFbxValidator
    {
        private const string ManifestFileName = "unity_validation_manifest.json";

        [Serializable]
        private sealed class PackManifest
        {
            public string pack_id;
            public string runtime_format;
            public string unity_report_path;
            public AssetContract[] assets;
        }

        [Serializable]
        private sealed class AssetContract
        {
            public string id;
            public string asset_path;
            public float[] dimensions_blender_xyz_m;
            public string origin;
            public float origin_tolerance_m;
            public float dimension_tolerance_m;
            public int preferred_max_triangles;
            public int hard_max_triangles;
            public bool material_required;
        }

        [Serializable]
        private sealed class AssetResult
        {
            public string id;
            public string asset_path;
            public bool passed;
            public string[] failures;
            public string[] warnings;
            public int mesh_count;
            public int triangle_count;
            public Vector3 expected_unity_dimensions_m;
            public Vector3 measured_unity_dimensions_m;
            public bool root_transform_identity;
            public bool bottom_center_origin;
            public bool material_present;
        }

        [Serializable]
        private sealed class PackReport
        {
            public string pack_id;
            public string generated_at_utc;
            public bool all_passed;
            public AssetResult[] assets;
        }

        [MenuItem("Life Engine/Asset Pipeline/Validate FBX Runtime Assets")]
        public static void ValidateAllFromMenu()
        {
            bool passed = ValidateAllInternal();
            if (passed)
                Debug.Log("Asset pipeline FBX validation passed for all discovered manifests.");
            else
                Debug.LogError("Asset pipeline FBX validation failed. Inspect the generated Unity validation report(s).");
        }

        /// <summary>
        /// Batch-mode entry point. Throws on failure so Unity exits with a failing status.
        /// Example: -executeMethod LifeEngine.Editor.AssetPipelineFbxValidator.ValidateAll
        /// </summary>
        public static void ValidateAll()
        {
            if (!ValidateAllInternal())
                throw new InvalidOperationException("One or more Unity FBX asset-pipeline validations failed.");
        }

        private static bool ValidateAllInternal()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("Could not resolve Unity project root.");
            string artSourceRoot = Path.Combine(projectRoot, "ArtSource");
            if (!Directory.Exists(artSourceRoot))
            {
                Debug.LogWarning("No ArtSource directory exists; no asset-pipeline Unity manifests were found.");
                return true;
            }

            string[] manifestPaths = Directory.GetFiles(artSourceRoot, ManifestFileName, SearchOption.AllDirectories);
            if (manifestPaths.Length == 0)
            {
                Debug.LogWarning("No asset-pipeline Unity validation manifests were found.");
                return true;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            bool allPacksPassed = true;
            foreach (string manifestPath in manifestPaths)
                allPacksPassed &= ValidateManifest(projectRoot, manifestPath);

            AssetDatabase.Refresh();
            return allPacksPassed;
        }

        private static bool ValidateManifest(string projectRoot, string manifestPath)
        {
            PackManifest manifest = JsonUtility.FromJson<PackManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.pack_id))
                throw new InvalidDataException($"Invalid Unity validation manifest: {manifestPath}");
            if (!string.Equals(manifest.runtime_format, "fbx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unity FBX validator received unsupported runtime format '{manifest.runtime_format}' in {manifestPath}.");
            if (manifest.assets == null || manifest.assets.Length == 0)
                throw new InvalidDataException($"Manifest contains no assets: {manifestPath}");

            List<AssetResult> results = new List<AssetResult>();
            foreach (AssetContract contract in manifest.assets)
                results.Add(ValidateAsset(contract));

            bool allPassed = results.All(result => result.passed);
            PackReport report = new PackReport
            {
                pack_id = manifest.pack_id,
                generated_at_utc = DateTime.UtcNow.ToString("O"),
                all_passed = allPassed,
                assets = results.ToArray()
            };

            string reportPath = EnsureInsideProject(projectRoot, manifest.unity_report_path);
            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
                Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));

            if (!allPassed)
            {
                foreach (AssetResult failed in results.Where(result => !result.passed))
                    Debug.LogError($"[{manifest.pack_id}] {failed.id}: {string.Join("; ", failed.failures)}");
            }
            else
            {
                Debug.Log($"[{manifest.pack_id}] Unity FBX validation PASS ({results.Count}/{results.Count}).");
            }

            return allPassed;
        }

        private static AssetResult ValidateAsset(AssetContract contract)
        {
            List<string> failures = new List<string>();
            List<string> warnings = new List<string>();
            AssetResult result = new AssetResult
            {
                id = contract.id,
                asset_path = contract.asset_path,
                failures = Array.Empty<string>(),
                warnings = Array.Empty<string>()
            };

            if (string.IsNullOrWhiteSpace(contract.id))
            {
                failures.Add("Asset contract is missing id.");
                return Finish(result, failures, warnings);
            }

            string assetPath = NormalizeAssetPath(contract.asset_path);
            result.asset_path = assetPath;
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                failures.Add($"Unity asset path must live under Assets/: {assetPath}");
                return Finish(result, failures, warnings);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (root == null)
            {
                failures.Add("Unity did not import the FBX as a GameObject asset.");
                return Finish(result, failures, warnings);
            }

            if (!string.Equals(root.name, contract.id, StringComparison.Ordinal))
                failures.Add($"Imported root identity mismatch: expected '{contract.id}', got '{root.name}'.");

            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (cameras.Length > 0)
                failures.Add($"Unexpected camera components imported: {cameras.Length}.");
            if (lights.Length > 0)
                failures.Add($"Unexpected light components imported: {lights.Length}.");
            if (skinned.Length > 0)
                failures.Add($"Unexpected skinned mesh renderers imported for static asset: {skinned.Length}.");

            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null)
                .ToArray();
            result.mesh_count = meshFilters.Length;
            if (meshFilters.Length != 1)
            {
                failures.Add($"Expected exactly one imported mesh, found {meshFilters.Length}.");
                return Finish(result, failures, warnings);
            }

            Transform rootTransform = root.transform;
            // Unity's ModelImporter converts right-handed Blender coordinates to left-handed Unity coordinates.
            // Depending on import settings (e.g. bakeAxisConversion) and FBX version:
            // - If axis conversion is baked into mesh geometry, root localRotation is identity.
            // - If axis conversion is left on the root transform, Unity applies an orientation change to align axes
            //   (pitch rotation around X to map Blender Z-up to Unity Y-up, with no yaw or roll drift).
            // Validate semantically: root must have zero translation offset, unit scale, and its rotation must
            // represent either pure identity or a canonical axis-alignment conversion (no yaw/roll rotation).
            bool translationIdentity = Approximately(rootTransform.localPosition, Vector3.zero, 1e-4f);
            bool scaleIdentity = Approximately(rootTransform.localScale, Vector3.one, 1e-4f);

            // A valid Blender-to-Unity axis conversion preserves the X axis direction (right) while mapping Z-up to Y-up.
            // Semantically, local forward (Z) and local up (Y) should align with coordinate cardinal axes with zero yaw/roll drift:
            Vector3 localRight = rootTransform.localRotation * Vector3.right;
            Vector3 localUp = rootTransform.localRotation * Vector3.up;
            Vector3 localForward = rootTransform.localRotation * Vector3.forward;
            bool xPreserved = Mathf.Abs(Vector3.Dot(localRight, Vector3.right) - 1.0f) <= 0.01f;
            bool yCardinal = Mathf.Abs(Mathf.Abs(Vector3.Dot(localUp, Vector3.up)) - 1.0f) <= 0.01f || Mathf.Abs(Mathf.Abs(Vector3.Dot(localUp, Vector3.forward)) - 1.0f) <= 0.01f;
            bool zCardinal = Mathf.Abs(Mathf.Abs(Vector3.Dot(localForward, Vector3.forward)) - 1.0f) <= 0.01f || Mathf.Abs(Mathf.Abs(Vector3.Dot(localForward, Vector3.up)) - 1.0f) <= 0.01f;
            bool rotationAcceptable = xPreserved && yCardinal && zCardinal;

            result.root_transform_identity = translationIdentity && scaleIdentity && rotationAcceptable;
            if (!result.root_transform_identity)
            {
                failures.Add($"Imported root transform is not a valid canonical FBX placement: position={rootTransform.localPosition}, rotation={rootTransform.localEulerAngles}, scale={rootTransform.localScale}.");
            }

            MeshFilter meshFilter = meshFilters[0];
            Mesh mesh = meshFilter.sharedMesh;
            int triangleCount = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                MeshTopology topology = mesh.GetTopology(subMesh);
                if (topology != MeshTopology.Triangles)
                {
                    failures.Add($"Submesh {subMesh} uses unsupported topology {topology}; runtime mesh is expected to be triangulated.");
                    continue;
                }
                triangleCount += checked((int)(mesh.GetIndexCount(subMesh) / 3));
            }
            result.triangle_count = triangleCount;
            if (triangleCount > contract.hard_max_triangles)
                failures.Add($"Triangle count {triangleCount} exceeds hard maximum {contract.hard_max_triangles}.");
            else if (triangleCount > contract.preferred_max_triangles)
                warnings.Add($"Triangle count {triangleCount} exceeds preferred maximum {contract.preferred_max_triangles}.");

            if (contract.dimensions_blender_xyz_m == null || contract.dimensions_blender_xyz_m.Length != 3)
            {
                failures.Add("Expected dimensions must contain Blender X/Y/Z values.");
            }
            else
            {
                // Blender X/Y/Z maps to Unity X/Z/Y for -Z-forward, Y-up FBX export.
                result.expected_unity_dimensions_m = new Vector3(contract.dimensions_blender_xyz_m[0], contract.dimensions_blender_xyz_m[2], contract.dimensions_blender_xyz_m[1]);
                Bounds rootLocalBounds = TransformBoundsToRootLocal(meshFilter, rootTransform);
                result.measured_unity_dimensions_m = rootLocalBounds.size;

                if (!Approximately(result.measured_unity_dimensions_m, result.expected_unity_dimensions_m, contract.dimension_tolerance_m))
                {
                    failures.Add($"Imported dimensions exceed tolerance {contract.dimension_tolerance_m} m. Expected Unity XYZ {result.expected_unity_dimensions_m}, got {result.measured_unity_dimensions_m}.");
                }

                if (string.Equals(contract.origin, "bottom_center", StringComparison.OrdinalIgnoreCase))
                {
                    float tolerance = contract.origin_tolerance_m;
                    result.bottom_center_origin =
                        Mathf.Abs(rootLocalBounds.center.x) <= tolerance &&
                        Mathf.Abs(rootLocalBounds.center.z) <= tolerance &&
                        Mathf.Abs(rootLocalBounds.min.y) <= tolerance;
                    if (!result.bottom_center_origin)
                        failures.Add($"Bottom-center pivot failed. Root-local bounds center={rootLocalBounds.center}, min={rootLocalBounds.min}, tolerance={tolerance}.");
                }
                else
                {
                    failures.Add($"Unity validator does not yet implement origin mode '{contract.origin}'.");
                }
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            result.material_present = renderers.Any(renderer => renderer.sharedMaterials != null && renderer.sharedMaterials.Any(material => material != null));
            if (contract.material_required && !result.material_present)
                failures.Add("Required material assignment did not survive Unity FBX import.");

            return Finish(result, failures, warnings);
        }

        private static AssetResult Finish(AssetResult result, List<string> failures, List<string> warnings)
        {
            result.failures = failures.ToArray();
            result.warnings = warnings.ToArray();
            result.passed = failures.Count == 0;
            return result;
        }

        private static Bounds TransformBoundsToRootLocal(MeshFilter meshFilter, Transform root)
        {
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            // When meshFilter is on the root GameObject, meshFilter.transform.localToWorldMatrix == root.localToWorldMatrix,
            // so root.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix is Matrix4x4.identity.
            // However, in Unity FBX import without bakeAxisConversion, the root transform has an axis-conversion rotation
            // (e.g. 90 or 270 deg around X), while the raw sharedMesh vertices still reside in Blender's unbaked coordinate frame.
            // Transforming mesh vertices by root.localRotation produces the oriented geometry in Unity's local coordinate frame
            // (where Y is up, Z is depth, matching expected_unity_dimensions_m and bottom_center on min.y).
            Matrix4x4 matrix = root.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
            if (meshFilter.transform == root && root.localRotation != Quaternion.identity)
            {
                matrix = Matrix4x4.Rotate(root.localRotation) * matrix;
            }
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };

            Vector3 first = matrix.MultiplyPoint3x4(corners[0]);
            Bounds transformed = new Bounds(first, Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
                transformed.Encapsulate(matrix.MultiplyPoint3x4(corners[i]));
            return transformed;
        }

        private static bool Approximately(Vector3 a, Vector3 b, float tolerance)
        {
            return Mathf.Abs(a.x - b.x) <= tolerance &&
                   Mathf.Abs(a.y - b.y) <= tolerance &&
                   Mathf.Abs(a.z - b.z) <= tolerance;
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private static string EnsureInsideProject(string projectRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException("Unity validation report path is missing.");
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
            string rootWithSeparator = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unity validation report path escapes project root: {relativePath}");
            return fullPath;
        }
    }
}
