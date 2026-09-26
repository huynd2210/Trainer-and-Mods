using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace NuclearOptionAMRAAM
{
    internal static class ModelAsset
    {
        internal static GameObject Load(string path, Transform storage)
        {
            var root = new GameObject("AMRAAM model");
            root.transform.SetParent(storage, false);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No supported lit shader found");
            var paint = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!paint.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), "amraam-paint.png"))))
                throw new InvalidDataException("Cannot decode AMRAAM paint texture");
            paint.anisoLevel = 4;
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (r.ReadInt32() != 0x414D5232) throw new InvalidDataException("Invalid AMRAAM mesh");
                int groups = r.ReadInt32();
                for (int g = 0; g < groups; g++)
                {
                    var color = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    float metal = r.ReadSingle(), rough = r.ReadSingle();
                    int count = r.ReadInt32();
                    var vertices = new Vector3[count]; var normals = new Vector3[count]; var indices = new int[count];
                    var uv = new Vector2[count];
                    for (int i = 0; i < count; i++)
                    {
                        vertices[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        normals[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); indices[i] = i;
                        uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    }
                    var mesh = new Mesh { name = "AMRAAM material " + g, indexFormat = IndexFormat.UInt32 };
                    mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = indices; mesh.RecalculateBounds();
                    var material = new Material(shader) { name = "AMRAAM finish " + g, color = color };
                    material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metal);
                    material.SetFloat("_Smoothness", 1f - rough); material.SetFloat("_Glossiness", 1f - rough);
                    material.SetTexture("_BaseMap", paint); material.SetTexture("_MainTex", paint);
                    var part = new GameObject("AMRAAM surface " + g);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
            }
            return root;
        }

        internal static void Replace(GameObject target, GameObject model)
        {
            // Leave exhaust particles, trails, launch rails and colliders functional.
            foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
            foreach (var renderer in target.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.enabled = false;
            foreach (var lod in target.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
            var visual = UnityEngine.Object.Instantiate(model, target.transform, false);
            visual.name = "AMRAAM custom visual";
            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = target.layer;
        }
    }
}
