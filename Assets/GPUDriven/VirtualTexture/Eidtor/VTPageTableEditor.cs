using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;


    [CustomEditor(typeof(VTPageTable))]
	public class VTPageTableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
			var table = (VTPageTable)target;
            base.OnInspectorGUI();
            DrawTexture(table.mLookupTexture, "Lookup Texture");
            DrawTexture(table.mHeightTileTexture, "Height Texture");
            DrawTexture(table.mBakeDiffuseTileTexture, "Diffuse Texture");
        }
        
        
        protected void DrawTexture(Texture texture, string label = null)
        {
            if(texture == null)
                return;

            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(label))
            {
                EditorGUILayout.LabelField(label);
                EditorGUILayout.LabelField(string.Format("    Size: {0} X {1}", texture.width, texture.height));
            }
            else
            {
                EditorGUILayout.LabelField(string.Format("Size: {0} X {1}", texture.width, texture.height));
            }

            EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect((float)texture.width / texture.height), texture);
        }

        private void DrawPreviewTexture(Texture texture)
        {
            if (texture == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(string.Format("Texture Size: {0} X {1}", texture.width, texture.height));
            EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect((float)texture.width / texture.height), texture);
        }
    }
