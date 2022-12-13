# gpudriventerrain
learn gpu driven 
之前实现了CPU端的clipmap地形，这个demo是实现GPUDriven的地形系统，地形方案还是采用Clipmap的变形，CDLOD版本。
CDLOD+VirtualTexture
简单的画一个流程图：
  ```flow
  start=>start: 初始化四叉树数据
  gpucull=>operation: 执行GPU端剔除：包括视锥体剔除以及Hiz剔除
  cpufeedback=>operation: CPU回读剔除结果buffer
  cpuCache=>operation: 根据剔除结果加载资源，并blit到LookupTable以及VirtualTexture上
  indirectdraw=>operation: 间接绘制地形
  end=>end: 结束
  start->gpucull->cpufeedback->cpuCache->end
