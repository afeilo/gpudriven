# gpudriventerrain
learn gpu driven 
之前实现了CPU端的clipmap地形，这个demo是实现GPUDriven的地形系统，地形方案还是采用Clipmap的变形，CDLOD版本。
CDLOD+VirtualTexture
简单的画一个流程图：

```
graph TD
A[初始化四叉树数据] -->B[执行GPU端剔除]
B --> C[CPU回读剔除结果buffer]
C --> D[根据剔除结果加载资源]
D --> E[blit到VirtualTexture上,并生成LookupTable]
E --> F[间接绘制地形]
```

https://user-images.githubusercontent.com/11472358/210044055-fb4fe85e-3ac5-4729-a708-cf7b71aced97.mp4


https://user-images.githubusercontent.com/11472358/209794788-596a183e-a961-42ee-88cc-d8114dcccb10.mp4

# MeshClusterRender
- MeshClusterRender：精准剔除 模型cluster划分 + computeshader + 视锥体剔除 + HIZ剔除 + DrawProceduralIndirect [参考1](https://zhuanlan.zhihu.com/p/425263243)[参考2](https://zhuanlan.zhihu.com/p/44411827)

https://user-images.githubusercontent.com/11472358/216742085-ac58e899-dcac-46a0-ba8a-b50bf1c07f03.mp4

- 下一步计划实现
  - 继续完善gpudriven,加入三角面剔除[参考](https://blog.csdn.net/liran2019/article/details/115618046)
  - 实现Lod，参考Nanite
