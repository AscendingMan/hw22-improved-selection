using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoverState
{
    public List<GameObject> m_HoveredObjects;
    public List<GameObject> m_SiblingObjects;
    public GameObject m_HoveredChildObject;
    public GameObject m_HoveredSiblingObject;

    public HoverState()
    {
        m_SiblingObjects = new List<GameObject>();
        m_HoveredObjects = new List<GameObject>();
    }
    
    public void Clear()
    {
        m_HoveredObjects?.Clear();
        m_HoveredChildObject = null;
        m_HoveredSiblingObject = null;
    }
}
