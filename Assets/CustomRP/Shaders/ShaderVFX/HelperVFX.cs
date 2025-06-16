/**************************************************************************************
 *                                                                                    *
 *  Copyright (C) 2025 Aleksandra Arakcheeva                                          *
 *                                                                                    *
 *  This work is licensed under the Creative Commons                                  *
 *  Attribution-NonCommercial-ShareAlike 3.0 Unported License.                        *
 *                                                                                    *
 *  To view a copy of this license, visit:                                            *
 *  https://creativecommons.org/licenses/by-nc-sa/3.0/                                *
 *                                                                                    *
 *  Key terms:                                                                        *
 *  — Attribution:     You must credit the original author.                           *
 *  — NonCommercial:   Prohibits commercial use without explicit permission.          *
 *  — ShareAlike:      Derivatives must be licensed under identical terms.            *
 *                                                                                    *
 *  Permitted use:                                                                    *
 *    - Modify, distribute, and use privately.                                        *
 *    - Include attribution in derivative works (see details below).                  *
 *                                                                                    *
 *  Prohibited without written permission:                                            *
 *    - Commercial exploitation (SaaS, paid apps, internal corporate tools).          *
 *    - Removing license terms from derivatives.                                      *
 *                                                                                    *
 *  Attribution requirement:                                                          *
 *    Include this header in source files OR display prominently in UI/docs:          *
 *    "Contains code from [Project Name] by [Author], licensed under CC BY-NC-SA 3.0" *
 *                                                                                    *
 *  DISCLAIMER: This code is provided "AS IS" without warranties of any kind.         *
 *  The author accepts no liability for damages arising from its use.                 *
 *                                                                                    *
 *************************************************************************************/



using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NoesisRender.Particles
{
    /// <summary>
    /// Used for generating new meshes suitable for my particle system
    /// </summary>
    public class HelperVFX : MonoBehaviour
    {
        [SerializeField] private Mesh baseMesh;
        [SerializeField][Range(1, 255)][Tooltip("Due to precision errors better use up to 255 particles in one shader. But possible to adjust shader to support 1024 max particles")] private int particlesCount;

        [SerializeField] private string pathToSave;
        [SerializeField] private string assetName;

        [Space]
        [SerializeField] private MeshFilter meshFilter;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        [ContextMenu("Gen meshes")]
        public void GenMeshes()
        {
#if UNITY_EDITOR
            Mesh generated = CreateParticlesFromMesh(baseMesh, particlesCount);
            if (generated == null)
            {
                Debug.LogError("Cannot save null mesh");
                return;
            }
            if (!pathToSave.StartsWith("Assets/"))
            {
                Debug.LogError("Path must be in Assets folder. Path: " + pathToSave);
                return;
            }

            string cleanPath = pathToSave.TrimEnd('/');
            string fullPath = $"{cleanPath}/{assetName}.asset";

            // Create directory structure if needed
            string systemPath = Application.dataPath + cleanPath.Substring(6);
            if (!System.IO.Directory.Exists(systemPath))
            {
                System.IO.Directory.CreateDirectory(systemPath);
            }

            // Delete existing asset if found
            if (AssetDatabase.LoadAssetAtPath<Mesh>(fullPath) != null)
            {
                AssetDatabase.DeleteAsset(fullPath);
                Debug.Log($"Deleted existing mesh at: {fullPath}");
            }

            Mesh meshToSave = UnityEngine.Object.Instantiate(generated);
            AssetDatabase.CreateAsset(meshToSave, fullPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved mesh " + assetName + " to: " + pathToSave);

            meshFilter.mesh = generated;
#endif
        }

        public Mesh CreateParticlesFromMesh(Mesh inputMesh, int count)
        {
            if (count < 1)
            {
                throw new ArgumentException("Count must be at least 1");
            }

            int baseVertexCount = inputMesh.vertexCount;
            int baseIndexCount = inputMesh.triangles.Length;

            int totalVertexCount = baseVertexCount * count;
            int totalIndexCount = baseIndexCount * count;

            Vector3[] newVertices = new Vector3[totalVertexCount];
            int[] newTriangles = new int[totalIndexCount];
            Vector2[] newUV = new Vector2[totalVertexCount];
            Color[] newColors = new Color[totalVertexCount];

            Vector3[] newNormals = null;
            if (inputMesh.normals != null && inputMesh.normals.Length == baseVertexCount)
            {
                newNormals = new Vector3[totalVertexCount];
            }

            Vector4[] newTangents = null;
            if (inputMesh.tangents != null && inputMesh.tangents.Length == baseVertexCount)
            {
                newTangents = new Vector4[totalVertexCount];
            }

            Vector3[] baseVertices = inputMesh.vertices;
            int[] baseTriangles = inputMesh.triangles;
            Vector2[] baseUV = inputMesh.uv;
            Vector3[] baseNormals = inputMesh.normals;
            Vector4[] baseTangents = inputMesh.tangents;

            for (int i = 0; i < count; i++)
            {
                int vertexOffset = i * baseVertexCount;
                int indexOffset = i * baseIndexCount;

                Array.Copy(baseVertices, 0, newVertices, vertexOffset, baseVertexCount);
                Array.Copy(baseUV, 0, newUV, vertexOffset, baseVertexCount);

                for (int j = 0; j < baseIndexCount; j++)
                {
                    newTriangles[indexOffset + j] = baseTriangles[j] + vertexOffset;
                }

                float colorValue = (float)i / (float)(count);
                //Debug.Log(i + " = " + colorValue);
                Color copyColor = new Color(colorValue, colorValue, colorValue, 1f);
                for (int j = 0; j < baseVertexCount; j++)
                {
                    newColors[vertexOffset + j] = copyColor;
                }

                if (newNormals != null)
                {
                    Array.Copy(baseNormals, 0, newNormals, vertexOffset, baseVertexCount);
                }

                if (newTangents != null)
                {
                    Array.Copy(baseTangents, 0, newTangents, vertexOffset, baseVertexCount);
                }
            }


            for (int i = 0; i < count; i++)
            {
                Debug.Log(i + " " + newColors[i * baseVertexCount].r);
            }


            Mesh newMesh = new Mesh
            {
                vertices = newVertices,
                triangles = newTriangles,
                uv = newUV,
                colors = newColors
            };

            if (newNormals != null)
            {
                newMesh.normals = newNormals;
            }

            if (newTangents != null)
            {
                newMesh.tangents = newTangents;
            }

            newMesh.RecalculateBounds();
            return newMesh;
        }
    }
}