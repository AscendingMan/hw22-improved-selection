using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ColorUtility = UnityEngine.ColorUtility;

[ExecuteAlways]
[ExecuteInEditMode]
public class SelectionBase : MonoBehaviour
{
    static class Styles
    {
        public static Color32 s_RootHoverColor;
        public static Color32 s_ChildHoverColor;

        static Styles()
        {
            s_RootHoverColor = Color.yellow;//new Color32(0x8c, 0x64, 0xcc, 0xFF);
            s_ChildHoverColor = Color.white; //new Color32(0xde, 0x58, 0xe0, 0xFF);
        }
    }
    
    private HoverState m_HoverState;
    private bool m_UsingModifier = false;
    private bool m_IsInContext = false;
    
    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUISelectionHighlight;
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowItemOnGUI;
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
        m_HoverState = new HoverState();
    }

    private void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
    {
        // Not working
        // if (m_HoverState.m_HoveredParentObject && instanceID == m_HoverState.m_HoveredParentObject.GetInstanceID() && !m_IsInContext)
        // {
        //     Color32 color = EditorColors.GetDefaultBackgroundColor(EditorUtils.IsHierarchyFocused, true);
        //     EditorGUI.DrawRect(selectionRect, color);
        //     
        //     color = EditorColors.GetDefaultTextColor(EditorUtils.IsHierarchyFocused, true);
        //     GUIStyle labelGUIStyle = new GUIStyle
        //     {
        //         normal = new GUIStyleState { textColor = color },
        //     };
        //     
        //     EditorGUI.LabelField(selectionRect, m_HoverState.m_HoveredParentObject.name, labelGUIStyle);
        // }
    }

    void OnSelectionChanged()
    {
        if (Selection.objects.Length == 0)
        {
            m_HoverState.Clear();
            m_IsInContext = false;
        }

        if (m_HoverState.m_SibilingObjects != null && !m_HoverState.m_SibilingObjects.Contains(Selection.activeGameObject))
        {
            m_IsInContext = false;
        }
    }
    
    void GetHoveredGameObject(Event currentEvent)
    {
        if(currentEvent.type == EventType.MouseMove)
        {
            var hoveredGO = HandleUtility.PickGameObject(currentEvent.mousePosition, true);
            if (hoveredGO == null)
                return;
            
            var isUsingModifier = IsUsingModifiers(currentEvent);
            if (isUsingModifier)
            {
                m_HoverState.m_HoveredChildObject = hoveredGO;
                m_HoverState.m_HoveredObjects.Clear();
                m_UsingModifier = true;
            }
            else
            {
                m_HoverState.m_HoveredParentObject = hoveredGO.transform.root.gameObject;
                m_HoverState.m_HoveredChildObject = null;
                m_HoverState.m_HoveredObjects = hoveredGO.transform.root.GetComponentsInChildren<Transform>().Select(x => x.gameObject).ToList();
                m_UsingModifier = false;
            }

            if (m_IsInContext && m_HoverState.m_SibilingObjects != null && m_HoverState.m_SibilingObjects.Contains(hoveredGO))
            {
                m_HoverState.m_HoveredSibilingObject = hoveredGO;
                m_HoverState.m_HoveredObjects.Clear();
            }
            else if (m_IsInContext)
            {
                m_HoverState.m_HoveredSibilingObject = null;
                m_HoverState.m_HoveredChildObject = null;
            }
        }
    }
    
    public void OnSceneGUISelectionHighlight(SceneView sceneView)
    {
        if (!sceneView.hasFocus)
            return;

        var currentEvent = Event.current;
        if (currentEvent.type == EventType.Repaint)
        {
            if (m_HoverState.m_HoveredSibilingObject != null) 
                Handles.DrawOutline(new []{m_HoverState.m_HoveredSibilingObject}, Color.cyan, 0.0f);
            if (m_HoverState.m_HoveredChildObject != null)
                Handles.DrawOutline(new []{m_HoverState.m_HoveredChildObject}, Styles.s_ChildHoverColor, 0.0f);
            if(m_HoverState.m_HoveredObjects?.Count > 0) 
                Handles.DrawOutline(m_HoverState.m_HoveredObjects, Styles.s_RootHoverColor, 0.0f);
        }
        
        GetHoveredGameObject(currentEvent);

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && currentEvent.isMouse && 
            (m_HoverState.m_HoveredParentObject || m_HoverState.m_HoveredChildObject))
        {
            if (m_UsingModifier)
            {
                m_IsInContext = true;
                m_HoverState.m_SibilingObjects = new List<GameObject>();
                Transform parent;
                int childCount;

                if (m_HoverState.m_HoveredChildObject != null)
                {
                    Selection.activeInstanceID = m_HoverState.m_HoveredChildObject.GetInstanceID();
                    parent = m_HoverState.m_HoveredChildObject.transform.parent;
                    childCount = parent.transform.childCount;
                }
                else
                {
                    parent = m_HoverState.m_HoveredParentObject.transform;
                    childCount = m_HoverState.m_HoveredParentObject.transform.childCount;
                }
                
                for (int i = 0; i < childCount; i++)
                {
                    m_HoverState.m_SibilingObjects.Add(parent.GetChild(i).gameObject);
                }
            }
            else
            {
                m_IsInContext = false;
                if (m_HoverState.m_HoveredParentObject)
                {
                    Selection.activeInstanceID = m_HoverState.m_HoveredParentObject.GetInstanceID();
                }
            }
        }
    }

    bool IsUsingModifiers(Event evt)
    {
        if (evt.modifiers == EventModifiers.Alt)
            return true;
        
        // Don't use CTRL/CMD for now
        // if (Application.platform == RuntimePlatform.OSXEditor && evt.modifiers == EventModifiers.Command)
        //     return true;
        // if (Application.platform != RuntimePlatform.OSXEditor && evt.modifiers == EventModifiers.Control)
        //     return true;

        return false;
    }
}
