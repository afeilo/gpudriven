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




https://user-images.githubusercontent.com/11472358/209797733-0490d20f-1e10-45fb-9331-cf6d08e1842b.mp4



https://user-images.githubusercontent.com/11472358/209794788-596a183e-a961-42ee-88cc-d8114dcccb10.mp4

