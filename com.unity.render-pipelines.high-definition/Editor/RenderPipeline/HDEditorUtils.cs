using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.ShaderGraph;
using UnityEngine.UIElements;
using System.Runtime.CompilerServices;

namespace UnityEditor.Rendering.HighDefinition
{
    public class HDEditorUtils
    {
        internal const string FormatingPath =
            @"Packages/com.unity.render-pipelines.high-definition/Editor/USS/Formating";
        internal const string QualitySettingsSheetPath =
            @"Packages/com.unity.render-pipelines.high-definition/Editor/USS/QualitySettings";
        internal const string WizardSheetPath =
            @"Packages/com.unity.render-pipelines.high-definition/Editor/USS/Wizard";
        
        private static (StyleSheet baseSkin, StyleSheet professionalSkin, StyleSheet personalSkin) LoadStyleSheets(string basePath)
            => (
                AssetDatabase.LoadAssetAtPath<StyleSheet>($"{basePath}.uss"),
                AssetDatabase.LoadAssetAtPath<StyleSheet>($"{basePath}Light.uss"),
                AssetDatabase.LoadAssetAtPath<StyleSheet>($"{basePath}Dark.uss")
            );

        internal static void AddStyleSheets(VisualElement element, string baseSkinPath)
        {
            (StyleSheet @base, StyleSheet personal, StyleSheet professional) = LoadStyleSheets(baseSkinPath);
            element.styleSheets.Add(@base);
            if (EditorGUIUtility.isProSkin)
            {
                if (professional != null && !professional.Equals(null))
                    element.styleSheets.Add(professional);
            }
            else
            {
                if (personal != null && !personal.Equals(null))
                    element.styleSheets.Add(personal);
            }
        }


        static readonly Action<SerializedProperty, GUIContent> k_DefaultDrawer = (p, l) => EditorGUILayout.PropertyField(p, l);



        internal static T LoadAsset<T>(string relativePath) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(HDUtils.GetHDRenderPipelinePath() + relativePath);

        /// <summary>
        /// Reset the dedicated Keyword and Pass regarding the shader kind.
        /// Also re-init the drawers and set the material dirty for the engine.
        /// </summary>
        /// <param name="material">The material that nees to be setup</param>
        /// <returns>
        /// True: managed to do the operation.
        /// False: unknown shader used in material
        /// </returns>
        [Obsolete("Use HDShaderUtils.ResetMaterialKeywords instead")]
        public static bool ResetMaterialKeywords(Material material)
            => HDShaderUtils.ResetMaterialKeywords(material);
        
        static readonly GUIContent s_OverrideTooltip = EditorGUIUtility.TrTextContent("", "Override this setting in component.");
        internal static bool FlagToggle<TEnum>(TEnum v, SerializedProperty property)
            where TEnum : struct, IConvertible // restrict to ~enum
        {
            var intV = (int)(object)v;
            var isOn = (property.intValue & intV) != 0;
            var rect = ReserveAndGetFlagToggleRect();
            isOn = GUI.Toggle(rect, isOn, s_OverrideTooltip, CoreEditorStyles.smallTickbox);
            if (isOn)
                property.intValue |= intV;
            else
                property.intValue &= ~intV;

            return isOn;
        }

        internal static Rect ReserveAndGetFlagToggleRect()
        {
            var rect = GUILayoutUtility.GetRect(11, 17, GUILayout.ExpandWidth(false));
            rect.y += 4;
            return rect;
        }

        internal static bool IsAssetPath(string path)
        {
            var isPathRooted = Path.IsPathRooted(path);
            return isPathRooted && path.StartsWith(Application.dataPath)
                   || !isPathRooted && path.StartsWith("Assets");
        }

        // Copy texture from cache
        internal static bool CopyFileWithRetryOnUnauthorizedAccess(string s, string path)
        {
            UnauthorizedAccessException exception = null;
            for (var k = 0; k < 20; ++k)
            {
                try
                {
                    File.Copy(s, path, true);
                    exception = null;
                }
                catch (UnauthorizedAccessException e)
                {
                    exception = e;
                }
            }

            if (exception != null)
            {
                Debug.LogException(exception);
                // Abort the update, something else is preventing the copy
                return false;
            }

            return true;
        }

        internal static void PropertyFieldWithOptionalFlagToggle<TEnum>(
            TEnum v, SerializedProperty property, GUIContent label,
            SerializedProperty @override, bool showOverrideButton,
            Action<SerializedProperty, GUIContent> drawer = null, int indent = 0
        )
            where TEnum : struct, IConvertible // restrict to ~enum
        {
            EditorGUILayout.BeginHorizontal();

            var i = EditorGUI.indentLevel;
            var l = EditorGUIUtility.labelWidth;
            EditorGUI.indentLevel = 0;
            EditorGUIUtility.labelWidth = 0;

            if (showOverrideButton)
                GUI.enabled = GUI.enabled && FlagToggle(v, @override);
            else
                ReserveAndGetFlagToggleRect();
            EditorGUI.indentLevel = indent;
            (drawer ?? k_DefaultDrawer)(property, label);

            GUI.enabled = true;
            EditorGUI.indentLevel = i;
            EditorGUIUtility.labelWidth = l;

            EditorGUILayout.EndHorizontal();
        }

        internal static void PropertyFieldWithFlagToggleIfDisplayed<TEnum>(
            TEnum v, SerializedProperty property, GUIContent label,
            SerializedProperty @override,
            TEnum displayed, TEnum overrideable,
            Action<SerializedProperty, GUIContent> drawer = null,
            int indent = 0
        )
            where TEnum : struct, IConvertible // restrict to ~enum
        {
            var intDisplayed = (int)(object)displayed;
            var intV = (int)(object)v;
            if ((intDisplayed & intV) == intV)
            {
                var intOverridable = (int)(object)overrideable;
                var isOverrideable = (intOverridable & intV) == intV;
                PropertyFieldWithOptionalFlagToggle(v, property, label, @override, isOverrideable, drawer, indent);
            }
        }

        internal static bool DrawSectionFoldout(string title, bool isExpanded)
        {
            CoreEditorUtils.DrawSplitter(false);
            return CoreEditorUtils.DrawHeaderFoldout(title, isExpanded, false);
        }

        internal static void DrawToolBarButton<TEnum>(
            TEnum button, Editor owner,
            Dictionary<TEnum, EditMode.SceneViewEditMode> toolbarMode,
            Dictionary<TEnum, GUIContent> toolbarContent,
            params GUILayoutOption[] options
        )
            where TEnum : struct, IConvertible
        {
            var intButton = (int)(object)button;
            bool enabled = toolbarMode[button] == EditMode.editMode;
            EditorGUI.BeginChangeCheck();
            enabled = GUILayout.Toggle(enabled, toolbarContent[button], EditorStyles.miniButton, options);
            if (EditorGUI.EndChangeCheck())
            {
                EditMode.SceneViewEditMode targetMode = EditMode.editMode == toolbarMode[button] ? EditMode.SceneViewEditMode.None : toolbarMode[button];
                EditMode.ChangeEditMode(targetMode, GetBoundsGetter(owner)(), owner);
            }
        }

        internal static Func<Bounds> GetBoundsGetter(Editor o)
        {
            return () =>
            {
                var bounds = new Bounds();
                var rp = ((Component)o.target).transform;
                var b = rp.position;
                bounds.Encapsulate(b);
                return bounds;
            };
        }

        /// <summary>
        /// Give a human readable string representing the inputed weight given in byte.
        /// </summary>
        /// <param name="weightInByte">The weigth in byte</param>
        /// <returns>Human readable weight</returns>
        internal static string HumanizeWeight(long weightInByte)
        {
            if (weightInByte < 500)
            {
                return weightInByte + " B";
            }
            else if (weightInByte < 500000L)
            {
                float res = weightInByte / 1000f;
                return res.ToString("n2") + " KB";
            }
            else if (weightInByte < 500000000L)
            {
                float res = weightInByte / 1000000f;
                return res.ToString("n2") + " MB";
            }
            else
            {
                float res = weightInByte / 1000000000f;
                return res.ToString("n2") + " GB";
            }
        }

        /// <summary>Provide a specific property drawer for LightLayer</summary>
        /// <param name="label">The desired label</param>
        /// <param name="property">The SerializedProperty (representing an int that should be displayed as a LightLayer)</param>
        internal static void LightLayerMaskPropertyDrawer(GUIContent label, SerializedProperty property)
        {
            var renderingLayerMask = property.intValue;
            int lightLayer;
            if (property.hasMultipleDifferentValues)
            {
                EditorGUI.showMixedValue = true;
                lightLayer = 0;
            }
            else
                lightLayer = HDAdditionalLightData.RenderingLayerMaskToLightLayer(renderingLayerMask);
            EditorGUI.BeginChangeCheck();
            lightLayer = System.Convert.ToInt32(EditorGUILayout.EnumFlagsField(label, (LightLayerEnum)lightLayer));
            if (EditorGUI.EndChangeCheck())
            {
                lightLayer = HDAdditionalLightData.LightLayerToRenderingLayerMask(lightLayer, renderingLayerMask);
                property.intValue = lightLayer;
            }
            EditorGUI.showMixedValue = false;
        }

        /// <summary>Provide a specific property drawer for LightLayer (without label)</summary>
        /// <param name="rect">The rect where to draw</param>
        /// <param name="value">The value to display (representing an int that should be displayed as a LightLayer)</param>
        /// <return> The value after modification</return>
        internal static int LightLayerMaskPropertyDrawer(Rect rect, int value)
        {
            int lightLayer = HDAdditionalLightData.RenderingLayerMaskToLightLayer(value);
            EditorGUI.BeginChangeCheck();
            lightLayer = System.Convert.ToInt32(EditorGUI.EnumFlagsField(rect, (LightLayerEnum)lightLayer));
            if (EditorGUI.EndChangeCheck())
            {
                lightLayer = HDAdditionalLightData.LightLayerToRenderingLayerMask(lightLayer, value);
            }
            return lightLayer;
        }
    }

    internal static partial class SerializedPropertyExtention
    {
        //[TODO: check example]
        /// <summary>
        /// Helper to get an enum value from a SerializedProperty.
        /// This handle case where index do not correspond to enum value.
        /// <example>
        /// <code>
        /// enum MyEnum
        /// {
        ///     A = 2,
        ///     B = 4,
        /// }
        /// public class MyObject : MonoBehavior
        /// {
        ///     public MyEnum theEnum = MyEnum.A;
        /// }    
        /// #if UNITY_EDITOR
        /// [CustomEditor(typeof(MyObject))]
        /// class MyObjectEditor : Editor
        /// {
        ///     public override void OnInspectorGUI()
        ///     {
        ///         Debug.Log($"By enumValueIndex: {(MyEnum)serializedObject.FindProperty("theEnum").enumValueIndex}");         //write the value (MyEnum)(0)
        ///         Debug.Log($"By GetEnumValue: {(MyEnum)serializedObject.FindProperty("theEnum").GetEnumValue<MyEnum>()}");   //write the value MyEnum.A
        ///     }
        /// }
        /// #endif
        /// </code>
        /// </example>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetEnumValue<T>(this SerializedProperty property)
            // intValue actually is the value underlying beside the enum
            => (T)(object)property.intValue;

        /// <summary>
        /// Helper to get an enum name from a SerializedProperty
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetEnumName<T>(this SerializedProperty property)
            => property.hasMultipleDifferentValues
            ? "MultipleDifferentValues"
            : property.enumNames[property.enumValueIndex];

        /// <summary>
        /// Helper to set an enum value to a SerializedProperty
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEnumValue<T>(this SerializedProperty property, T value)
            // intValue actually is the value underlying beside the enum
            => property.intValue = (int)(object)value;

        /// <summary>
        /// Get the value of a <see cref="SerializedProperty"/>.
        ///
        /// This function will be inlined by the compiler.
        /// Caution: The case of Enum is not handled here.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the value to get.
        ///
        /// It is expected to be a supported type by the <see cref="SerializedProperty"/>.
        /// </typeparam>
        /// <param name="serializedProperty">The property to get.</param>
        /// <returns>The value of the property.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetInline<T>(this SerializedProperty serializedProperty)
            where T : struct
        {
            if (typeof(T) == typeof(Color))
                return (T)(object)serializedProperty.colorValue;
            if (typeof(T) == typeof(string))
                return (T)(object)serializedProperty.stringValue;
            if (typeof(T) == typeof(double))
                return (T)(object)serializedProperty.doubleValue;
            if (typeof(T) == typeof(float))
                return (T)(object)serializedProperty.floatValue;
            if (typeof(T) == typeof(long))
                return (T)(object)serializedProperty.longValue;
            if (typeof(T) == typeof(int))
                return (T)(object)serializedProperty.intValue;
            if (typeof(T) == typeof(bool))
                return (T)(object)serializedProperty.boolValue;
            if (typeof(T) == typeof(BoundsInt))
                return (T)(object)serializedProperty.boundsIntValue;
            if (typeof(T) == typeof(Bounds))
                return (T)(object)serializedProperty.boundsValue;
            if (typeof(T) == typeof(RectInt))
                return (T)(object)serializedProperty.rectIntValue;
            if (typeof(T) == typeof(Rect))
                return (T)(object)serializedProperty.rectValue;
            if (typeof(T) == typeof(Quaternion))
                return (T)(object)serializedProperty.quaternionValue;
            if (typeof(T) == typeof(Vector2Int))
                return (T)(object)serializedProperty.vector2IntValue;
            if (typeof(T) == typeof(Vector4))
                return (T)(object)serializedProperty.vector4Value;
            if (typeof(T) == typeof(Vector3))
                return (T)(object)serializedProperty.vector3Value;
            if (typeof(T) == typeof(Vector2))
                return (T)(object)serializedProperty.vector2Value;
            if (typeof(T).IsEnum)
                return serializedProperty.GetEnumValue<T>();
            throw new ArgumentOutOfRangeException($"<{typeof(T)}> is not a valid type for a serialized property.");
        }

        /// <summary>
        /// Set the value of a <see cref="SerializedProperty"/>.
        ///
        /// This function will be inlined by the compiler.
        /// Caution: The case of Enum is not handled here.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the value to set.
        ///
        /// It is expected to be a supported type by the <see cref="SerializedProperty"/>.
        /// </typeparam>
        /// <param name="serializedProperty">The property to set.</param>
        /// <param name="value">The value to set.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetInline<T>(this SerializedProperty serializedProperty, T value)
            where T : struct
        {
            if (typeof(T) == typeof(Color))
            {
                serializedProperty.colorValue = (Color)(object)value;
                return;
            }
            if (typeof(T) == typeof(string))
            {
                serializedProperty.stringValue = (string)(object)value;
                return;
            }
            if (typeof(T) == typeof(double))
            {
                serializedProperty.doubleValue = (double)(object)value;
                return;
            }
            if (typeof(T) == typeof(float))
            {
                serializedProperty.floatValue = (float)(object)value;
                return;
            }
            if (typeof(T) == typeof(long))
            {
                serializedProperty.longValue = (long)(object)value;
                return;
            }
            if (typeof(T) == typeof(int))
            {
                serializedProperty.intValue = (int)(object)value;
                return;
            }
            if (typeof(T) == typeof(bool))
            {
                serializedProperty.boolValue = (bool)(object)value;
                return;
            }
            if (typeof(T) == typeof(BoundsInt))
            {
                serializedProperty.boundsIntValue = (BoundsInt)(object)value;
                return;
            }
            if (typeof(T) == typeof(Bounds))
            {
                serializedProperty.boundsValue = (Bounds)(object)value;
                return;
            }
            if (typeof(T) == typeof(RectInt))
            {
                serializedProperty.rectIntValue = (RectInt)(object)value;
                return;
            }
            if (typeof(T) == typeof(Rect))
            {
                serializedProperty.rectValue = (Rect)(object)value;
                return;
            }
            if (typeof(T) == typeof(Quaternion))
            {
                serializedProperty.quaternionValue = (Quaternion)(object)value;
                return;
            }
            if (typeof(T) == typeof(Vector2Int))
            {
                serializedProperty.vector2IntValue = (Vector2Int)(object)value;
                return;
            }
            if (typeof(T) == typeof(Vector4))
            {
                serializedProperty.vector4Value = (Vector4)(object)value;
                return;
            }
            if (typeof(T) == typeof(Vector3))
            {
                serializedProperty.vector3Value = (Vector3)(object)value;
                return;
            }
            if (typeof(T) == typeof(Vector2))
            {
                serializedProperty.vector2Value = (Vector2)(object)value;
                return;
            }
            if (typeof(T).IsEnum)
            { 
                serializedProperty.SetEnumValue(value);
                return;
            }
            throw new ArgumentOutOfRangeException($"<{typeof(T)}> is not a valid type for a serialized property.");
        }
    }
}
