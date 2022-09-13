using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoverState
{
    public List<GameObject> m_HoveredObjects;
    public List<GameObject> m_SibilingObjects;
    public GameObject m_HoveredParentObject;
    public GameObject m_HoveredChildObject;
    public GameObject m_HoveredSibilingObject;
    public bool m_IsContext;

    public void Clear()
    {
        m_HoveredObjects?.Clear();
        m_SibilingObjects?.Clear();
        m_HoveredParentObject = null;
        m_HoveredChildObject = null;
        m_HoveredSibilingObject = null;
    }
}
