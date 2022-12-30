using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyBoardControl : MonoBehaviour
{
    public float m_MoveSpeed = 0.5f;
    private Transform m_Transform;
    void Start()
    {
        m_Transform =gameObject.GetComponent<Transform>();   
    }

    void Update()
    {
        MoveControl();
    }

    void MoveControl()
    {
        if (Input.GetKey(KeyCode.W))
        {
            //键盘控制物品前后左右移动，调用函数Translate
            m_Transform.Translate(Vector3.forward * m_MoveSpeed, Space.Self);
        }

        if (Input.GetKey(KeyCode.S))
        {
            m_Transform.Translate(Vector3.back * m_MoveSpeed, Space.Self);
        }

        if (Input.GetKey(KeyCode.A))
        {
            m_Transform.Translate(Vector3.left * m_MoveSpeed, Space.Self);
        }

        if (Input.GetKey(KeyCode.D))
        {
            m_Transform.Translate(Vector3.right * m_MoveSpeed, Space.Self);
        }
        //键盘控制物品旋转，调用函数Rotate	
        if (Input.GetKey(KeyCode.Q))
        {
        
            m_Transform.Rotate(Vector3.up, -m_MoveSpeed);
        }

        if (Input.GetKey(KeyCode.E))
        {
            m_Transform.Rotate(Vector3.up, m_MoveSpeed);
        }

        if (Input.GetKey(KeyCode.Space))
        {
            //键盘控制物品上下移动，调用函数Translate
            m_Transform.Translate(Vector3.up * 0.1f, Space.Self);
        }

        if (Input.GetKey(KeyCode.X))
        {
            m_Transform.Translate(Vector3.down * 0.1f, Space.Self);
        }

        //鼠标控制物品旋转，调用函数Rotate
        // m_Transform.Rotate(Vector3.up, Input.GetAxis("Mouse X"));
        //
        // m_Transform.Rotate(Vector3.left, Input.GetAxis("Mouse Y"));

    }

}
