﻿using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Cinemachine.Utility;

namespace Cinemachine.Editor
{
    internal class CinemachineStageEditor 
    {
        // Static state and caches - Call UpdateStaticData() to refresh this
        struct StageData
        {
            string ExpandedKey { get { return "Cinemachine_Vcam_Stage_Expanded_" + Name; } }
            public bool IsExpanded 
            {
                get { return EditorPrefs.GetBool(ExpandedKey, false); }
                set { EditorPrefs.SetBool(ExpandedKey, value); }
            }
            public string Name;
            public Type[] types;   // first entry is null
            public GUIContent[] PopupOptions;
        }
        static StageData[] sStageData = null;

        [InitializeOnLoad]
        class EditorInitialize 
        { 
            // This code dynamically discovers eligible classes and builds the menu
            // data for the various component pipeline stages.
            static EditorInitialize() 
            {
                sStageData = new StageData[Enum.GetValues(typeof(CinemachineCore.Stage)).Length];

                var stageTypes = new List<Type>[Enum.GetValues(typeof(CinemachineCore.Stage)).Length];
                for (int i = 0; i < stageTypes.Length; ++i)
                {
                    sStageData[i].Name = ((CinemachineCore.Stage)i).ToString();
                    stageTypes[i] = new List<Type>();
                }

                // Get all ICinemachineComponents
                var allTypes
                    = ReflectionHelpers.GetTypesInAllLoadedAssemblies(
                            (Type t) => t.IsSubclassOf(typeof(CinemachineComponentBase)));

                // GML todo: use class attribute instead
                // Create a temp game object so we can instance behaviours
                GameObject go = new GameObject("Cinemachine Temp Object");
                go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                foreach (Type t in allTypes)
                {
                    MonoBehaviour b = go.AddComponent(t) as MonoBehaviour;
                    CinemachineComponentBase c = b != null ? (CinemachineComponentBase)b : null;
                    if (c != null)
                    {
                        CinemachineCore.Stage stage = c.Stage;
                        stageTypes[(int)stage].Add(t);
                    }
                }
                GameObject.DestroyImmediate(go);

                // Create the static lists
                for (int i = 0; i < stageTypes.Length; ++i)
                {
                    stageTypes[i].Insert(0, null);  // first item is "none"
                    sStageData[i].types = stageTypes[i].ToArray();
                    GUIContent[] names = new GUIContent[sStageData[i].types.Length];
                    for (int n = 0; n < names.Length; ++n)
                    {
                        if (n == 0)
                        {
                            bool useSimple
                                = (i == (int)CinemachineCore.Stage.Aim)
                                    || (i == (int)CinemachineCore.Stage.Body);
                            names[n] = new GUIContent((useSimple) ? "Do nothing" : "none");
                        }
                        else
                            names[n] = new GUIContent(InspectorUtility.NicifyClassName(sStageData[i].types[n].Name));
                    }
                    sStageData[i].PopupOptions = names;
                }
            }
        }

        int mStageSelection = 0;
        bool mStageError = false;
        CinemachineCore.Stage mStage;
        CinemachineComponentBase mComponent;
        UnityEditor.Editor mComponentEditor;

        // Target game object
        public GameObject Target { get; private set; }

        // Call this from OnEnable()
        public CinemachineStageEditor(CinemachineCore.Stage stage, GameObject target)
        {
            mStage = stage;
            Target = target;
        }

        ~CinemachineStageEditor()
        {
            Shutdown();
        }

        // Call this from OnDisable()
        public void Shutdown()
        {
            if (mComponentEditor != null)
                UnityEngine.Object.DestroyImmediate(mComponentEditor);
            mComponentEditor = null;
            Target = null;
            mComponent = null;
        }

        // The current editor for the component (may be null)
        public UnityEditor.Editor ComponentEditor { get { return mComponentEditor; } }

        // Returns true if there are more than zero options for this pipeline stage
        public bool HasImplementation { get { return sStageData[(int)mStage].PopupOptions.Length > 1; } }

        // Can the component type be changed by the user?
        public bool TypeIsLocked { get; set; }

        public bool ShowDisabledCheckbox { get; set; }
        public bool ComponentSelectionDisabled { get; set; }
        public GUIContent ComponentSelectionDisableLabel { get; set; }

        // Call this from Editor's OnInspectorGUI - returns new component if user changes type
        public void OnInspectorGUI(CinemachineComponentBase component)
        {
            if (component != mComponent)
            {
                if (mComponentEditor != null)
                    UnityEngine.Object.DestroyImmediate(mComponentEditor);
                mComponentEditor = null;
                mComponent = component;
            }
            if (mComponent != null && mComponentEditor == null)
                UnityEditor.Editor.CreateCachedEditor(mComponent, null, ref mComponentEditor);
            mStageSelection = GetPopupIndexForComponent(mComponent);
            mStageError = mComponent  == null ? false : !mComponent.IsValid;
            DrawComponentInspector();
        }

        private int GetPopupIndexForComponent(CinemachineComponentBase c)
        {
            if (c != null)
            {
                var types = sStageData[(int)mStage].types;
                for (int i = types.Length-1; i > 0; --i)
                    if (c.GetType() == types[i])
                        return i;
            }
            return 0; // none
        }

        private void DrawComponentInspector()
        {
            const float kBoxMargin = 4; // GML wtf get rid of this
            const float indentSize = 15; // GML wtf get rid of this

            int index = (int)mStage;

            GUIStyle stageBoxStyle = GUI.skin.box;
            EditorGUILayout.BeginVertical(stageBoxStyle);
            EditorGUIUtility.labelWidth -= kBoxMargin;

            Rect rect = EditorGUILayout.GetControlRect(true);

            // Don't use PrefixLabel() because it will link the enabled status of field and label
            GUIContent label = new GUIContent(InspectorUtility.NicifyClassName(mStage.ToString()));
            if (mStageError)
                label.image = EditorGUIUtility.IconContent("console.warnicon.sml").image;
            float labelWidth = EditorGUIUtility.labelWidth - EditorGUI.indentLevel * indentSize;
            Rect r = rect; r.width = labelWidth; r.x -= kBoxMargin;
            EditorGUI.LabelField(r, label);

            r = rect; r.width -= labelWidth; r.x += labelWidth;
            if (ShowDisabledCheckbox)
            {
                if (ComponentSelectionDisabled)
                {
                    label = ComponentSelectionDisableLabel;
                    if (label == null)
                        label = new GUIContent("Customize"); // GUIContent.none;
                    ComponentSelectionDisabled = !EditorGUI.ToggleLeft(r, label, !ComponentSelectionDisabled);
                }
                else
                {
                    ComponentSelectionDisabled 
                        = !EditorGUI.ToggleLeft(new Rect(r.x, r.y, r.height * 2, r.height), 
                            GUIContent.none, !ComponentSelectionDisabled);
                    r.x += r.height; r.width -= r.height;
                }
            }

            if (!ComponentSelectionDisabled)
            {
                bool wasEnabled = GUI.enabled;
                if (TypeIsLocked)
                    GUI.enabled = false;
                int newSelection = EditorGUI.Popup(r, mStageSelection, sStageData[index].PopupOptions);
                GUI.enabled = wasEnabled;

                Type type = sStageData[index].types[newSelection];
                if (newSelection != mStageSelection)
                {
                    if (newSelection == 0)
                    {
                        if (DestroyComponent != null)
                            DestroyComponent(mComponent);
                    }
                    else
                    {
                        sStageData[index].IsExpanded = true;
                        if (SetComponent != null)
                            SetComponent(type);
                    }
                    mComponent = null;
                    GUIUtility.ExitGUI();
                    return; // let the component editor be recreated
                }

                // Draw the embedded editor
                if (type != null)
                {
                    r = new Rect(rect.x - kBoxMargin, rect.y, labelWidth, rect.height);
                    sStageData[index].IsExpanded = EditorGUI.Foldout(
                            r, sStageData[index].IsExpanded, GUIContent.none, true);
                    if (sStageData[index].IsExpanded)
                    {
                        // Make the editor for that stage
                        if (mComponentEditor != null)
                        {
                            ++EditorGUI.indentLevel;
                            EditorGUILayout.Separator();
                            mComponentEditor.OnInspectorGUI();
                            EditorGUILayout.Separator();
                            --EditorGUI.indentLevel;
                        }
                    }
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUIUtility.labelWidth += kBoxMargin;
        }

        public delegate void DestroyComponentDelegate(CinemachineComponentBase component);
        public DestroyComponentDelegate DestroyComponent;

        public delegate void SetComponentDelegate(Type type);
        public SetComponentDelegate SetComponent;
    }
}