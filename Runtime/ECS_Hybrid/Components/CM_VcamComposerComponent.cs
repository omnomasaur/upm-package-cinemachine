using Unity.Entities;
using Cinemachine.ECS;

namespace Cinemachine.ECS_Hybrid
{
    [UnityEngine.DisallowMultipleComponent]
    [CM_Pipeline(CinemachineCore.Stage.Aim)]
    [SaveDuringPlay]
    public class CM_VcamComposerComponent : ComponentDataWrapper<CM_VcamComposer> { }
}