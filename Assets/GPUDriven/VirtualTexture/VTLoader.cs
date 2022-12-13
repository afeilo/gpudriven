using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VTLoader : MonoBehaviour
{
    /// <summary>
    /// 每帧最大加载数量
    /// </summary>
    public int maxLoadCount = 16;

    public void AddRequest()
    {

    }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public class LoadRequest<T>
    {
        public string ResName;
        public int Priority;
        public Action<T> LoadCallback;
    }
    
    public class ImageLoadRequest : LoadRequest<Texture2D>
    {

    }
}
